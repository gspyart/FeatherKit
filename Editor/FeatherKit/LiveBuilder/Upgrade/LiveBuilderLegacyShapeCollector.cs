using System.Collections.Generic;
using FeatherKit.LevelDesign;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Сборщик коробок старых данных (версия 0): по порядку в иерархии, со старыми повторами — для старого правила и снимка «как было» миграции.
    /// - группа повтора (<see cref="BoxModelRepeat"/>), привязка к этажам (<see cref="SplineBuildingStoreyLink"/>), скрытые копии Array FeatherKit — сразу за оригиналом
    /// - читается вместо <see cref="BoxModelShapeCollector"/> у объекта версии 0; удалится вместе со старыми классами, когда все данные обновлены
    /// </summary>
    public static class LiveBuilderLegacyShapeCollector
    {
        // ── Сбор ─────────────────────────────────────────────────────────

        // Коробки расчёта и скаты лестниц объекта по старому правилу. Сам объект привязан к этажам здания — всё его содержимое в каждой копии.
        public static List<BoxModelSolverBox> Collect(BoxModel model, List<BoxModelRampShape> ramps)
        {
            var boxes = new BoxModelCollectedBoxes();
            var constructions = new[] { model.transform };
            var count = 1;
            var step = Vector3.zero;
            var isModelLinked = TryGetStoreyCopies(model, model.transform, ref count, ref step);

            foreach (var arrayOffset in BoxModelShapeCollector.ArrayOffsetsIn(model, LiveBuilderArrayCopyPoses.CopiesOf(model.transform)))
                for (var copy = 0; copy < count && boxes.Count < BoxModelShapeCollector.MaxSolverBoxes; copy++)
                    CollectChildren(model, model.transform, arrayOffset + step * copy, isModelLinked, constructions, boxes, ramps);

            BoxModelShapeCollector.WarnIfOverLimit(model, boxes.Count);
            return boxes.Boxes;
        }


        // Дети узла по порядку; скрытые копии Array FeatherKit — сразу за своим оригиналом. isInsideStoreyCopies — этажи уже повторяет кто-то выше.
        private static void CollectChildren(BoxModel model, Transform node, Vector3 offset, bool isInsideStoreyCopies,
            IReadOnlyList<Transform> constructions, BoxModelCollectedBoxes boxes, List<BoxModelRampShape> ramps)
        {
            foreach (Transform child in node)
            {
                if (IsArrayModifierCopyOfSibling(child))
                    continue;

                CollectChild(model, child, offset, isInsideStoreyCopies, constructions, boxes, ramps);

                foreach (var arrayModifierCopy in ArrayModifierCopiesOf(child))
                    CollectChild(model, arrayModifierCopy, offset, isInsideStoreyCopies, constructions, boxes, ramps);

                if (boxes.Count >= BoxModelShapeCollector.MaxSolverBoxes)
                    return;
            }
        }

        // Ребёнок со всем, что под ним, — на каждую копию своего повтора и старого повтора или привязки.
        private static void CollectChild(BoxModel model, Transform child, Vector3 offset, bool isInsideStoreyCopies,
            IReadOnlyList<Transform> constructions, BoxModelCollectedBoxes boxes, List<BoxModelRampShape> ramps)
        {
            if (!BoxModelShapeCollector.IsOwnPiece(child))
                return;

            var childConstructions = child.GetComponent<SplineBuilding>() != null ? BoxModelShapeCollector.WithConstruction(constructions, child) : constructions;

            var repeat = child.GetComponent<BoxModelRepeat>();
            var count = repeat != null ? repeat.Count : 1;
            var step = repeat != null ? repeat.Step : Vector3.zero;
            var isChildLinked = repeat == null && !isInsideStoreyCopies && TryGetStoreyCopies(model, child, ref count, ref step);

            foreach (var arrayOffset in BoxModelShapeCollector.OwnArrayOffsetsIn(model, child))
                for (var copy = 0; copy < count; copy++)
                {
                    if (boxes.Count >= BoxModelShapeCollector.MaxSolverBoxes)
                        return;

                    var copyOffset = offset + arrayOffset + step * copy;
                    CollectOwn(model, child, copyOffset, childConstructions, boxes, ramps);
                    CollectChildren(model, child, copyOffset, isInsideStoreyCopies || isChildLinked, childConstructions, boxes, ramps);
                }
        }

        // Коробка, комната или лестница на самом узле — каждая своим куском. Вырезов-коробок на старом правиле нет: вырезы режут вычитанием.
        private static void CollectOwn(BoxModel model, Transform node, Vector3 offset, IReadOnlyList<Transform> constructions,
            BoxModelCollectedBoxes boxes, List<BoxModelRampShape> ramps)
        {
            if (node.TryGetComponent<BoxModelPart>(out var part))
            {
                var min = BoxModelShapeCollector.LocalMinOf(model, node) + offset;
                boxes.BeginPiece(constructions);
                boxes.Add(MillimeterBox.FromMeters(min, part.Size), part.Mode, part, BoxModelSideMaterials.Of(part), part.Chamfers);
            }

            if (node.TryGetComponent<BoxModelRoom>(out var room))
            {
                boxes.BeginPiece(constructions);
                BoxModelRoomShell.Add(room, model, BoxModelShapeCollector.LocalMinOf(model, node) + offset, boxes);
            }

            if (node.TryGetComponent<BoxModelStairs>(out var stairs))
            {
                boxes.BeginPiece(constructions);
                BoxModelStairsLayout.Add(stairs, BoxModelShapeCollector.LocalMinOf(model, node) + offset, boxes, ramps);
            }
        }


        // ── Скрытые копии Array FeatherKit ───────────────────────────────

        // Порядок копий разных массивов при открытии сцены каждый раз свой — копия собирается сразу за своим оригиналом, а не по месту в иерархии.
        private static bool IsArrayModifierCopyOfSibling(Transform child)
        {
            return child.TryGetComponent<ArrayPreviewCopy>(out var marker) && marker.Owner != null && marker.Owner.transform.parent == child.parent
                   && marker.Owner.PreviewCopies.Contains(child.gameObject);
        }

        public static List<Transform> ArrayModifierCopiesOf(Transform original)
        {
            var copies = new List<Transform>();

            if (!original.TryGetComponent<ArrayModifier>(out var array))
                return copies;

            foreach (var copy in array.PreviewCopies)
                if (copy != null && copy.transform.parent == original.parent && copy.TryGetComponent<ArrayPreviewCopy>(out _))
                    copies.Add(copy.transform);

            copies.Sort((first, second) => first.GetComponent<ArrayPreviewCopy>().Number.CompareTo(second.GetComponent<ArrayPreviewCopy>().Number));
            return copies;
        }


        // ── Привязки к этажам ────────────────────────────────────────────

        // Привязан к этажам здания — копии на этажах выше, шаг — этаж здания в осях объекта.
        private static bool TryGetStoreyCopies(BoxModel model, Transform node, ref int count, ref Vector3 step)
        {
            if (!IsLinkedToStoreys(node))
                return false;

            var link = node.GetComponent<SplineBuildingStoreyLink>();
            var building = link.Building;
            count = link.CopiesFrom(link.Storey);
            step = model.transform.InverseTransformVector(building.transform.up * building.StoreyHeight);
            return true;
        }


        private static bool IsLinkedToStoreys(Transform node)
        {
            return node.TryGetComponent<SplineBuildingStoreyLink>(out var link) && link.IsValid;
        }
    }
}
