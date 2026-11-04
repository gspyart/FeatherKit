using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Собирает коробки расчёта объекта из коробок по порядку в иерархии: что ниже, то главнее.
    /// - коробка (<see cref="BoxModelPart"/>) — одна коробка расчёта
    /// - комната (<see cref="BoxModelRoom"/>) — стены вокруг, если нужны, и вырезающая коробка самой комнаты
    /// - повтор (<see cref="BoxModelRepeat"/>) — всё внутри группы ещё раз на каждый шаг, копия за копией; повторы вкладываются
    /// - привязка к этажам здания (<see cref="SplineBuildingStoreyLink"/>) на коробке, комнате или лестнице — то же, шаг — этаж здания,
    ///   копий — сколько этажей задано в привязке; один и тот же повтор по этажам, что у стен и проёмов
    /// - лестница (<see cref="BoxModelStairs"/>) — вырез над маршем и ступени (сплошные до пола или отдельными плитами), плюс невидимый скат для столкновений
    /// Вложенный объект из коробок и стена по линии — сами по себе, их содержимое не наше. В здание по контуру заходим:
    /// лестницы и вырезы его интерьера — коробки этого объекта.
    /// </summary>
    public static class BoxModelShapeCollector
    {
        // Толщина ската у лестницы из отдельных ступеней, м: тонкая полоса — под маршем остаётся место.
        private const float SeparateStepsRampThickness = 0.1f;


        // Коробки расчёта и скаты лестниц объекта.
        public static List<BoxModelSolverBox> Collect(BoxModel model, List<BoxModelRampShape> ramps)
        {
            var boxes = new List<BoxModelSolverBox>();
            CollectChildren(model, model.transform, Vector3.zero, boxes, ramps);
            return boxes;
        }


        // Дети узла по порядку; у повтора — его содержимое на каждый шаг.
        private static void CollectChildren(BoxModel model, Transform node, Vector3 offset, List<BoxModelSolverBox> boxes, List<BoxModelRampShape> ramps)
        {
            foreach (Transform child in node)
            {
                if (!child.gameObject.activeSelf || child.GetComponent<BoxModel>() != null || child.GetComponent<SplineWall>() != null)
                    continue;

                var repeat = child.GetComponent<BoxModelRepeat>();
                var count = repeat != null ? repeat.Count : 1;
                var step = repeat != null ? repeat.Step : Vector3.zero;

                if (repeat == null)
                    TryGetStoreyCopies(model, child, ref count, ref step);

                for (var copy = 0; copy < count; copy++)
                {
                    var copyOffset = offset + step * copy;
                    CollectOwn(model, child, copyOffset, boxes, ramps);
                    CollectChildren(model, child, copyOffset, boxes, ramps);
                }
            }
        }

        // Привязан к этажам здания — копии на этажах выше, шаг — этаж здания в осях объекта. У группы повтора копии считает сама группа.
        private static void TryGetStoreyCopies(BoxModel model, Transform node, ref int count, ref Vector3 step)
        {
            if (!node.TryGetComponent<SplineBuildingStoreyLink>(out var link) || !link.IsValid)
                return;

            var building = link.Building;
            count = link.CopiesFrom(link.Storey);
            step = model.transform.InverseTransformVector(building.transform.up * building.StoreyHeight);
        }

        // Коробка или лестница на самом узле.
        private static void CollectOwn(BoxModel model, Transform node, Vector3 offset, List<BoxModelSolverBox> boxes, List<BoxModelRampShape> ramps)
        {
            if (node.TryGetComponent<BoxModelPart>(out var part))
            {
                var min = LocalMinOf(model, node) + offset;
                boxes.Add(new BoxModelSolverBox(MillimeterBox.FromMeters(min, part.Size), part.Mode, part, BoxModelSideMaterials.Of(part), part.Chamfers));
            }

            if (node.TryGetComponent<BoxModelRoom>(out var room))
                AddRoom(room, LocalMinOf(model, node) + offset, boxes);

            if (node.TryGetComponent<BoxModelStairs>(out var stairs))
                AddStairs(stairs, LocalMinOf(model, node) + offset, boxes, ramps);
        }


        // ── Комната ──────────────────────────────────────────────────────

        // Стены вокруг (если нужны) и сама комната — вырезающая коробка с материалами пола, стен и потолка.
        private static void AddRoom(BoxModelRoom room, Vector3 min, List<BoxModelSolverBox> boxes)
        {
            if (room.HasWalls)
            {
                var shell = new Vector3(room.WallThickness, room.SlabThickness, room.WallThickness);
                boxes.Add(new BoxModelSolverBox(MillimeterBox.FromMeters(min - shell, room.Size + shell * 2f), BoxModelPartMode.Add, room, default));
            }

            var materials = new BoxModelSideMaterials(null, room.FloorMaterial, room.CeilingMaterial, room.WallMaterial);
            boxes.Add(new BoxModelSolverBox(MillimeterBox.FromMeters(min, room.Size), BoxModelPartMode.Cut, room, materials));
        }


        // ── Лестница ─────────────────────────────────────────────────────

        // Вырез над маршем (если нужен), ступени снизу вверх и скат через носики ступеней.
        // Вырез — только там, где достаёт голова: от HeadroomHeight над полом до верха лестницы. Ниже — стены и всё вокруг целы.
        private static void AddStairs(BoxModelStairs stairs, Vector3 min, List<BoxModelSolverBox> boxes, List<BoxModelRampShape> ramps)
        {
            var materials = new BoxModelSideMaterials(stairs.Material);
            var size = stairs.Size;
            var stepCount = stairs.StepCount;
            var riser = stairs.Height / stepCount;
            var tread = stairs.StepDepth;
            var runAxis = stairs.RunAxis;

            if (stairs.CutsAbove && stairs.Height > BoxModelStairs.HeadroomHeight)
            {
                var cutMin = min + Vector3.up * BoxModelStairs.HeadroomHeight;
                var cutSize = size;
                cutSize.y = stairs.Height - BoxModelStairs.HeadroomHeight;
                boxes.Add(new BoxModelSolverBox(MillimeterBox.FromMeters(cutMin, cutSize), BoxModelPartMode.Cut, stairs, materials));
            }

            var isSeparate = stairs.Style == BoxModelStairsStyle.SeparateSteps;

            for (var step = 0; step < stepCount; step++)
            {
                var stepTop = (step + 1) * riser;
                var stepBottom = isSeparate ? Mathf.Max(0f, stepTop - stairs.StepThickness) : 0f;
                var stepMin = min;
                var stepSize = size;
                stepMin[runAxis] = RunToAxis(stairs, min, step * tread, (step + 1) * tread, out var stepLength);
                stepMin.y = min.y + stepBottom;
                stepSize[runAxis] = stepLength;
                stepSize.y = stepTop - stepBottom;
                boxes.Add(new BoxModelSolverBox(MillimeterBox.FromMeters(stepMin, stepSize), BoxModelPartMode.Add, stairs, materials));
            }

            ramps.Add(RampOf(stairs, min, riser, tread));
        }

        // Скат: от пола за ступень до первой ступени, через носики до верха.
        // У сплошной — клин до пола и ровно до конца марша; у отдельных ступеней — тонкая полоса вдоль носиков, под лестницей можно пройти.
        private static BoxModelRampShape RampOf(BoxModelStairs stairs, Vector3 min, float riser, float tread)
        {
            var stepCount = stairs.StepCount;
            var bottomStart = new Vector2(-tread, 0f);
            var lastNose = new Vector2((stepCount - 1) * tread, stepCount * riser);
            var profile = stairs.Style == BoxModelStairsStyle.SeparateSteps
                ? new[] { bottomStart, lastNose, lastNose + Vector2.down * SeparateStepsRampThickness, bottomStart + Vector2.down * SeparateStepsRampThickness }
                : new[] { bottomStart, lastNose, new Vector2(stepCount * tread, stepCount * riser), new Vector2(stepCount * tread, 0f) };

            var crossAxis = stairs.RunAxis == 0 ? 2 : 0;
            var corners = new Vector3[8];

            for (var edge = 0; edge < 2; edge++)
                for (var index = 0; index < profile.Length; index++)
                {
                    var corner = min;
                    corner[stairs.RunAxis] = RunPointToAxis(stairs, min, profile[index].x);
                    corner.y = min.y + profile[index].y;
                    corner[crossAxis] = min[crossAxis] + edge * stairs.Width;
                    corners[edge * profile.Length + index] = corner;
                }

            return new BoxModelRampShape(corners);
        }

        // Отрезок марша [from, to] от начала подъёма — в координату по оси объекта: возвращает меньший край и длину.
        private static float RunToAxis(BoxModelStairs stairs, Vector3 min, float from, float to, out float length)
        {
            length = to - from;
            return Mathf.Min(RunPointToAxis(stairs, min, from), RunPointToAxis(stairs, min, to));
        }

        private static float RunPointToAxis(BoxModelStairs stairs, Vector3 min, float run)
        {
            var axisMin = min[stairs.RunAxis];
            return stairs.RunsTowardsPlus ? axisMin + run : axisMin + stairs.Length - run;
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Угол с наименьшими координатами в осях объекта, м.
        public static Vector3 LocalMinOf(BoxModel model, Transform node)
        {
            return model.transform.InverseTransformPoint(node.position);
        }
    }
}
