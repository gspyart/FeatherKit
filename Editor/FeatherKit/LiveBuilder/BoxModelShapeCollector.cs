using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Собирает коробки расчёта объекта из коробок по порядку в иерархии; у каждой — её кусок и постройки над ней (<see cref="BoxModelCollectedBoxes"/>).
    /// - коробка, вырез-коробка по осям (<see cref="GeometryCutterSolverBoxes"/>), комната (<see cref="BoxModelRoomShell"/>), лестница (<see cref="BoxModelStairsLayout"/>)
    /// - повтор (<see cref="LiveBuilderArray"/>) на объекте, над ним и внутри — всё под ним ещё раз на каждую копию; вариант узора — в копиях своей буквы
    /// - вложенный объект и стена по линии — сами по себе; в здание заходим; старые данные — <see cref="LiveBuilderLegacyShapeCollector"/>
    /// </summary>
    public static class BoxModelShapeCollector
    {
        // Больше коробок расчёта у одного объекта не берём. Вложенные повторы перемножаются (200 × 200 — 40 000 коробок) и вешают редактор;
        // 5000 — с запасом даже для большого уровня: повтор на 200 копий по 25 коробок в каждой.
        internal const int MaxSolverBoxes = 5000;

        // Объекты, где уже предупредили о пределе: одно предупреждение, а не на каждую пересборку.
        private static readonly HashSet<EntityId> ModelsWarnedAboutLimit = new();

        // Узел без повтора — одна копия, на месте.
        private static readonly Vector3[] OnlyOriginal = { Vector3.zero };


        // Коробки расчёта и скаты лестниц объекта. Сам объект под повтором (шахта в этаже здания) — всё его содержимое в каждой копии, одним мешем.
        // Старые данные (версия 0) собирает старый сборщик — с прежними повторами и привязками.
        public static List<BoxModelSolverBox> Collect(BoxModel model, List<BoxModelRampShape> ramps)
        {
            if (LiveBuilderCutRules.IsLegacy(model))
                return LiveBuilderLegacyShapeCollector.Collect(model, ramps);

            var boxes = new BoxModelCollectedBoxes();
            var constructions = new[] { model.transform };

            var patternArray = PatternArrayOf(model.transform);

            if (patternArray == null)
            {
                foreach (var arrayOffset in ArrayOffsetsIn(model, LiveBuilderArrayCopyPoses.CopiesOf(model.transform)))
                    if (boxes.Count < MaxSolverBoxes)
                        CollectChildren(model, model.transform, arrayOffset, constructions, boxes, ramps);
            }
            else
            {
                var copiesAbove = LiveBuilderArrayCopyPoses.CopiesAboveOf(model.transform);

                foreach (var aboveOffset in ArrayOffsetsIn(model, copiesAbove))
                    foreach (var patternCopy in PatternCopiesOf(patternArray, copiesAbove.Count))
                        if (boxes.Count < MaxSolverBoxes)
                            CollectChildren(model, model.transform, aboveOffset + model.transform.InverseTransformVector(patternCopy.Shift), constructions, boxes, ramps,
                                patternArray, patternCopy);
            }

            WarnIfOverLimit(model, boxes.Count);
            return boxes.Boxes;
        }

        // Упёрлись в предел — предупредить один раз; стало меньше — снова предупредим, если упрётся опять.
        internal static void WarnIfOverLimit(BoxModel model, int boxCount)
        {
            var modelId = model.GetEntityId();

            if (boxCount < MaxSolverBoxes)
            {
                ModelsWarnedAboutLimit.Remove(modelId);
                return;
            }

            if (ModelsWarnedAboutLimit.Add(modelId))
                Debug.LogWarning("Live Builder: '" + model.name + "' has more than " + MaxSolverBoxes +
                                 " boxes counting repeats — the extra ones are not built. Lower the copy counts of nested repeats.", model);
        }


        // Дети узла по порядку. Дошли до предела коробок — дальше не идём. Узел — повтор с узором: дети-варианты — только в копиях своей буквы.
        private static void CollectChildren(BoxModel model, Transform node, Vector3 offset, IReadOnlyList<Transform> constructions,
            BoxModelCollectedBoxes boxes, List<BoxModelRampShape> ramps, LiveBuilderArray patternArray = null, LiveBuilderArrayCopy patternCopy = default)
        {
            foreach (Transform child in node)
            {
                if (patternArray != null && !patternCopy.Shows(LiveBuilderArrayPattern.VariantIndexOf(patternArray, child)))
                    continue;

                CollectChild(model, child, offset, constructions, boxes, ramps);

                if (boxes.Count >= MaxSolverBoxes)
                    return;
            }
        }

        // Ребёнок со всем, что под ним, — на каждую копию своего повтора.
        private static void CollectChild(BoxModel model, Transform child, Vector3 offset, IReadOnlyList<Transform> constructions,
            BoxModelCollectedBoxes boxes, List<BoxModelRampShape> ramps)
        {
            if (!IsOwnPiece(child))
                return;

            var childConstructions = child.GetComponent<SplineBuilding>() != null ? WithConstruction(constructions, child) : constructions;
            var patternArray = PatternArrayOf(child);

            if (patternArray != null)
            {
                CollectPatternCopies(model, child, patternArray, offset, childConstructions, boxes, ramps);
                return;
            }

            foreach (var arrayOffset in OwnArrayOffsetsIn(model, child))
            {
                if (boxes.Count >= MaxSolverBoxes)
                    return;

                CollectOwn(model, child, offset + arrayOffset, childConstructions, boxes, ramps);
                CollectChildren(model, child, offset + arrayOffset, childConstructions, boxes, ramps);
            }
        }


        // Узел с повтором и узором: его свои куски — в каждой копии, дети-варианты — в копиях своей буквы.
        private static void CollectPatternCopies(BoxModel model, Transform node, LiveBuilderArray patternArray, Vector3 offset, IReadOnlyList<Transform> constructions,
            BoxModelCollectedBoxes boxes, List<BoxModelRampShape> ramps)
        {
            foreach (var patternCopy in PatternCopiesOf(patternArray, LiveBuilderArrayCopyPoses.CopiesOf(node.parent).Count))
            {
                if (boxes.Count >= MaxSolverBoxes)
                    return;

                var copyOffset = offset + model.transform.InverseTransformVector(patternCopy.Shift);
                CollectOwn(model, node, copyOffset, constructions, boxes, ramps);
                CollectChildren(model, node, copyOffset, constructions, boxes, ramps, patternArray, patternCopy);
            }
        }


        // ── Повтор ───────────────────────────────────────────────────────

        // Повтор узла с вариантами узора; без вариантов — null: копируется всё, как обычно.
        private static LiveBuilderArray PatternArrayOf(Transform node)
        {
            return node.TryGetComponent<LiveBuilderArray>(out var array) && array.Variants.Count > 0 ? array : null;
        }

        // Копии повтора с узором, которые строит расчёт: повёрнутые на изгибе стены — только настоящие копии.
        private static List<LiveBuilderArrayCopy> PatternCopiesOf(LiveBuilderArray array, int copiesAbove)
        {
            return LiveBuilderArrayCopyPoses.PlacedCopiesOf(array, copiesAbove).FindAll(copy => !copy.IsTurned);
        }

        // Копии повтора на самом узле в осях объекта; повтора нет — одна, на месте. Предел копий — вместе с повторами над узлом.
        internal static IReadOnlyList<Vector3> OwnArrayOffsetsIn(BoxModel model, Transform node)
        {
            var array = node.GetComponent<LiveBuilderArray>();

            if (array == null)
                return OnlyOriginal;

            var copiesAbove = LiveBuilderArrayCopyPoses.CopiesOf(node.parent).Count;
            return ArrayOffsetsIn(model, LiveBuilderArrayCopyPoses.OwnCopiesOf(array, copiesAbove));
        }

        // Сдвиги копий из мира — в оси объекта.
        internal static IReadOnlyList<Vector3> ArrayOffsetsIn(BoxModel model, IReadOnlyList<Vector3> worldOffsets)
        {
            if (worldOffsets.Count == 1 && worldOffsets[0] == Vector3.zero)
                return OnlyOriginal;

            var offsets = new Vector3[worldOffsets.Count];

            for (var index = 0; index < offsets.Length; index++)
                offsets[index] = model.transform.InverseTransformVector(worldOffsets[index]);

            return offsets;
        }


        // Здание для сборщика прозрачно, но для вырезов — граница «Own object»: его куски помнят его над собой.
        internal static Transform[] WithConstruction(IReadOnlyList<Transform> constructions, Transform construction)
        {
            var extended = new Transform[constructions.Count + 1];

            for (var index = 0; index < constructions.Count; index++)
                extended[index] = constructions[index];

            extended[constructions.Count] = construction;
            return extended;
        }


        // ── Куски ────────────────────────────────────────────────────────

        // Коробка, комната или лестница — кусок, из которого объект собирает коробки расчёта. Одна проверка на всех, кто спрашивает «это кусок?».
        public static bool IsPiece(GameObject gameObject)
        {
            return gameObject.TryGetComponent<BoxModelPart>(out _) || gameObject.TryGetComponent<BoxModelRoom>(out _) || gameObject.TryGetComponent<BoxModelStairs>(out _);
        }


        // Включённый кусок этого объекта — его коробки расчёта наши.
        internal static bool IsOwnPiece(Transform child)
        {
            return child.gameObject.activeSelf && !IsNestedConstruction(child);
        }

        // Вложенный объект из коробок или стена по линии — сами по себе, их содержимое не наше.
        internal static bool IsNestedConstruction(Transform child)
        {
            return child.GetComponent<BoxModel>() != null || child.GetComponent<SplineWall>() != null;
        }

        // Коробка, комната, лестница или вырез-коробка на самом узле — каждая своим куском.
        private static void CollectOwn(BoxModel model, Transform node, Vector3 offset, IReadOnlyList<Transform> constructions,
            BoxModelCollectedBoxes boxes, List<BoxModelRampShape> ramps)
        {
            if (node.TryGetComponent<BoxModelPart>(out var part))
            {
                var min = LocalMinOf(model, node) + offset;
                boxes.BeginPiece(constructions);
                boxes.Add(MillimeterBox.FromMeters(min, part.Size), part.Mode, part, BoxModelSideMaterials.Of(part), part.Chamfers);
            }

            if (node.TryGetComponent<BoxModelRoom>(out var room))
            {
                boxes.BeginPiece(constructions);
                BoxModelRoomShell.Add(room, model, LocalMinOf(model, node) + offset, boxes);
            }

            if (node.TryGetComponent<BoxModelStairs>(out var stairs))
            {
                boxes.BeginPiece(constructions);
                BoxModelStairsLayout.Add(stairs, LocalMinOf(model, node) + offset, boxes, ramps);
            }

            if (node.TryGetComponent<GeometryCutter>(out var cutter) && GeometryCutterSolverBoxes.IsCollectedBy(model, cutter))
            {
                boxes.BeginPiece(constructions);
                var materials = GeometryCutterSolverBoxes.SideMaterialsIn(cutter, model);

                foreach (var box in GeometryCutterSolverBoxes.BoxesOf(cutter, model, offset))
                    boxes.AddCutter(box, cutter, materials);
            }
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Угол с наименьшими координатами в осях объекта, м.
        public static Vector3 LocalMinOf(BoxModel model, Transform node)
        {
            return model.transform.InverseTransformPoint(node.position);
        }
    }

    /// <summary>
    /// Коробки расчёта по ходу сбора объекта и кусок, который собирается сейчас: его номер и постройки над ним.
    /// Номер куска — место его первой коробки в списке: одинаковый на каждом сборе, у каждой копии повтора свой.
    /// </summary>
    internal sealed class BoxModelCollectedBoxes
    {
        private int pieceKey;
        private IReadOnlyList<Transform> pieceConstructions;


        public List<BoxModelSolverBox> Boxes { get; } = new();

        public int Count => Boxes.Count;


        public void BeginPiece(IReadOnlyList<Transform> constructions)
        {
            pieceKey = Boxes.Count + 1;
            pieceConstructions = constructions;
        }


        public void Add(MillimeterBox box, BoxModelPartMode mode, Component owner, BoxModelSideMaterials materials,
            IReadOnlyList<BoxModelEdgeChamfer> chamfers = null)
        {
            Boxes.Add(new BoxModelSolverBox(box, mode, owner, materials, chamfers, pieceKey, pieceConstructions));
        }


        public void AddCutter(MillimeterBox box, GeometryCutter cutter, BoxModelSideMaterials materials)
        {
            Boxes.Add(BoxModelSolverBox.ForCutter(box, cutter, materials, pieceKey, pieceConstructions));
        }
    }
}
