using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Как было» перед обновлением старых данных и сверка «как стало» после.
    /// - тело каждого объекта из коробок по старому правилу — клетками солвера, и коробки по порядку списка (их читает правка порядка вырезов)
    /// - грани мешей стен, частей зданий и объектов (<see cref="LiveBuilderUpgradeMeshFaces"/>) — сохранённые старой сборкой; после новой — то же
    /// - сверка тела «как стало» — <see cref="LiveBuilderUpgradeBody"/>; расхождения — строками в итог
    /// </summary>
    public sealed class LiveBuilderUpgradeSnapshot
    {
        private readonly Dictionary<BoxModel, List<BoxModelSolverBox>> legacyBoxes = new();
        private readonly Dictionary<BoxModel, List<GameObject>> legacyOwners = new();
        private readonly Dictionary<BoxModel, List<BoxModelCell>> legacyCells = new();
        private readonly Dictionary<Component, LiveBuilderUpgradeMeshFaces> meshFaces = new();


        // ── Снимок ───────────────────────────────────────────────────────

        // Объекты старых данных под корнями: коробки и тело по старому правилу; меши — какие сейчас собраны.
        public static LiveBuilderUpgradeSnapshot Take(IReadOnlyList<GameObject> roots)
        {
            var snapshot = new LiveBuilderUpgradeSnapshot();

            foreach (var root in roots)
            {
                foreach (var model in root.GetComponentsInChildren<BoxModel>(true))
                {
                    if (!LiveBuilderCutRules.IsLegacy(model))
                        continue;

                    var boxes = LiveBuilderLegacyShapeCollector.Collect(model, new List<BoxModelRampShape>());
                    snapshot.legacyBoxes[model] = boxes;
                    snapshot.legacyOwners[model] = boxes.ConvertAll(box => OwnerObjectOf(box.Owner));
                    snapshot.legacyCells[model] = BoxModelSolver.Solve(boxes.ConvertAll(box => box.Shape), BoxModelCutRule.LaterBoxesCut);
                    snapshot.meshFaces[model] = LiveBuilderUpgradeMeshFaces.Of(model);
                }

                foreach (var wall in root.GetComponentsInChildren<SplineWall>(true))
                    snapshot.meshFaces[wall] = LiveBuilderUpgradeMeshFaces.Of(wall);

                foreach (var part in root.GetComponentsInChildren<SplineBuildingPart>(true))
                    snapshot.meshFaces[part] = LiveBuilderUpgradeMeshFaces.Of(part);
            }

            return snapshot;
        }


        public IEnumerable<BoxModel> LegacyModels => legacyBoxes.Keys;


        // Коробки объекта по старому правилу, по порядку списка, с копиями.
        public IReadOnlyList<BoxModelSolverBox> LegacyBoxesOf(BoxModel model)
        {
            return legacyBoxes.TryGetValue(model, out var boxes) ? boxes : new List<BoxModelSolverBox>();
        }


        // Объект сцены каждой коробки из LegacyBoxesOf: у скрытой копии Array FeatherKit — её оригинал. Компонент коробки обновление может заменить, объект — остаётся.
        public IReadOnlyList<GameObject> LegacyOwnersOf(BoxModel model)
        {
            return legacyOwners.TryGetValue(model, out var owners) ? owners : new List<GameObject>();
        }

        private static GameObject OwnerObjectOf(Component owner)
        {
            if (owner == null)
                return null;

            return owner.TryGetComponent<FeatherKit.LevelDesign.ArrayPreviewCopy>(out var copy) && copy.Owner != null ? copy.Owner.gameObject : owner.gameObject;
        }


        // ── Сверка ───────────────────────────────────────────────────────

        // После обновления и новой пересборки: тела объектов, меши стен и частей зданий — те же, что были. Не сошлось — строка в итог.
        public void CompareAfter(LiveBuilderCopyConflictUpgrade copyConflicts, LiveBuilderUpgradeReport report)
        {
            var checkedCount = 0;

            foreach (var pair in legacyCells)
            {
                if (pair.Key == null)
                    continue;

                checkedCount++;
                var difference = LiveBuilderUpgradeBody.DifferenceFrom(pair.Key, pair.Value, copyConflicts.UnrolledCopiesOf(pair.Key));

                if (difference != null)
                    report.AddMayLookDifferent(pair.Key, difference);
            }

            foreach (var pair in meshFaces)
            {
                // Меш объекта с «Kept From Cuts» или развёрнутого поделён с ними, касания их граней — лишние грани; тело сверено выше клетками.
                if (pair.Key == null || pair.Key is BoxModel model && (LiveBuilderUpgradeBody.KeptFromCutsOf(model).Count > 0 || copyConflicts.UnrolledCopiesOf(model).Count > 0))
                    continue;

                checkedCount++;
                var difference = pair.Value.DifferenceFrom(LiveBuilderUpgradeMeshFaces.Of(pair.Key));

                if (difference != null)
                    report.AddMayLookDifferent(pair.Key, difference);
            }

            report.CheckedCount += checkedCount;
        }
    }
}
