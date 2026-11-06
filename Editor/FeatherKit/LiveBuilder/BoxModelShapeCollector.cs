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
    /// лестницы и вырезы его интерьера — коробки этого объекта. Привязка к этажам на самом объекте (вложенный объект в здании —
    /// шахта, лестничная клетка) — все его коробки ещё раз на каждом этаже повтора, как у группы повтора.
    /// Привязка внутри уже привязанного (коробка в привязанном объекте или группе) копий не добавляет: этажи уже повторены снаружи,
    /// иначе копии перемножились бы — три этажа на три этажа.
    /// </summary>
    public static class BoxModelShapeCollector
    {
        // Толщина ската у лестницы из отдельных ступеней, м: тонкая полоса — под маршем остаётся место.
        private const float SeparateStepsRampThickness = 0.1f;

        // Больше коробок расчёта у одного объекта не берём. Вложенные повторы перемножаются (200 × 200 — 40 000 коробок) и вешают редактор;
        // 5000 — с запасом даже для большого уровня: повтор на 200 копий по 25 коробок в каждой.
        private const int MaxSolverBoxes = 5000;

        // Объекты, где уже предупредили о пределе: одно предупреждение, а не на каждую пересборку.
        private static readonly HashSet<EntityId> ModelsWarnedAboutLimit = new();


        // Коробки расчёта и скаты лестниц объекта. Сам объект привязан к этажам здания (шахта в здании) — всё его содержимое на каждом этаже повтора, одним мешем.
        public static List<BoxModelSolverBox> Collect(BoxModel model, List<BoxModelRampShape> ramps)
        {
            var boxes = new List<BoxModelSolverBox>();
            var count = 1;
            var step = Vector3.zero;
            var isModelLinked = TryGetStoreyCopies(model, model.transform, ref count, ref step);

            for (var copy = 0; copy < count && boxes.Count < MaxSolverBoxes; copy++)
                CollectChildren(model, model.transform, step * copy, isModelLinked, boxes, ramps);

            WarnIfOverLimit(model, boxes.Count);
            return boxes;
        }

        // Упёрлись в предел — предупредить один раз; стало меньше — снова предупредим, если упрётся опять.
        private static void WarnIfOverLimit(BoxModel model, int boxCount)
        {
            var modelId = model.GetEntityId();

            if (boxCount < MaxSolverBoxes)
            {
                ModelsWarnedAboutLimit.Remove(modelId);
                return;
            }

            if (ModelsWarnedAboutLimit.Add(modelId))
                Debug.LogWarning("Live Builder: у объекта «" + model.name + "» больше " + MaxSolverBoxes +
                                 " коробок с учётом повторов — лишние не строятся. Уменьши число копий во вложенных повторах.", model);
        }


        // Коробки расчёта одной группы (например, повтора) так, как в её первой копии: сама группа и всё внутри, повторы внутри — со всеми копиями.
        public static List<BoxModelSolverBox> CollectFirstCopy(BoxModel model, Transform group)
        {
            var boxes = new List<BoxModelSolverBox>();

            if (!group.gameObject.activeInHierarchy)
                return boxes;

            var ramps = new List<BoxModelRampShape>();
            CollectOwn(model, group, Vector3.zero, boxes, ramps);
            CollectChildren(model, group, Vector3.zero, IsInsideStoreyCopies(model, group), boxes, ramps);
            return boxes;
        }


        // Дети узла по порядку; у повтора — его содержимое на каждый шаг. Дошли до предела коробок — дальше не идём.
        // isInsideStoreyCopies — узел или кто-то над ним уже повторён по этажам: привязки детей копий не добавляют.
        private static void CollectChildren(BoxModel model, Transform node, Vector3 offset, bool isInsideStoreyCopies, List<BoxModelSolverBox> boxes,
            List<BoxModelRampShape> ramps)
        {
            foreach (Transform child in node)
            {
                if (!IsOwnPiece(child))
                    continue;

                var repeat = child.GetComponent<BoxModelRepeat>();
                var count = repeat != null ? repeat.Count : 1;
                var step = repeat != null ? repeat.Step : Vector3.zero;
                var isChildLinked = repeat == null && !isInsideStoreyCopies && TryGetStoreyCopies(model, child, ref count, ref step);

                for (var copy = 0; copy < count; copy++)
                {
                    if (boxes.Count >= MaxSolverBoxes)
                        return;

                    var copyOffset = offset + step * copy;
                    CollectOwn(model, child, copyOffset, boxes, ramps);
                    CollectChildren(model, child, copyOffset, isInsideStoreyCopies || isChildLinked, boxes, ramps);
                }
            }
        }

        // Привязан к этажам здания — копии на этажах выше, шаг — этаж здания в осях объекта. У группы повтора копии считает сама группа.
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


        // ── Привязки к этажам ────────────────────────────────────────────

        // Узел или кто-то над ним в этом объекте, вместе с самим объектом, привязан к этажам: этажи ему уже повторяет та привязка.
        public static bool IsInsideStoreyCopies(BoxModel model, Transform node)
        {
            for (var current = node; current != null; current = current.parent)
            {
                if (IsLinkedToStoreys(current))
                    return true;

                if (current == model.transform)
                    break;
            }

            return false;
        }


        // Коробка, комната или лестница — кусок, из которого объект собирает коробки расчёта. Одна проверка на всех, кто спрашивает «это кусок?».
        public static bool IsPiece(GameObject gameObject)
        {
            return gameObject.TryGetComponent<BoxModelPart>(out _) || gameObject.TryGetComponent<BoxModelRoom>(out _) || gameObject.TryGetComponent<BoxModelStairs>(out _);
        }


        // Коробку, комнату или лестницу уже повторяет по этажам объект или группа, в которой она стоит.
        public static bool IsPieceInsideStoreyCopies(Transform piece)
        {
            var parent = piece.parent;
            var ownerModel = parent != null ? parent.GetComponentInParent<BoxModel>() : null;
            return ownerModel != null && IsInsideStoreyCopies(ownerModel, parent);
        }


        // Под узлом есть привязанные к этажам коробки, комнаты, лестницы или группы — свои, не вложенных объектов и стен.
        // Спрятанные из-за пропавшего этажа — тоже: привязка на них осталась.
        public static bool HasStoreyLinksInside(Transform node)
        {
            foreach (Transform child in node)
                if (!IsNestedConstruction(child) && (IsLinkedToStoreys(child) || HasStoreyLinksInside(child)))
                    return true;

            return false;
        }

        private static bool IsLinkedToStoreys(Transform node)
        {
            return node.TryGetComponent<SplineBuildingStoreyLink>(out var link) && link.IsValid;
        }


        // Включённый кусок этого объекта — его коробки расчёта наши.
        private static bool IsOwnPiece(Transform child)
        {
            return child.gameObject.activeSelf && !IsNestedConstruction(child);
        }

        // Вложенный объект из коробок или стена по линии — сами по себе, их содержимое не наше.
        private static bool IsNestedConstruction(Transform child)
        {
            return child.GetComponent<BoxModel>() != null || child.GetComponent<SplineWall>() != null;
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
        // Без пола или потолка у стен вокруг нет плиты с той стороны: вырез комнаты выходит наружу — там открыто.
        private static void AddRoom(BoxModelRoom room, Vector3 min, List<BoxModelSolverBox> boxes)
        {
            if (room.HasWalls)
            {
                var shell = RoomShellOf(min, room.Size, room.WallThickness, room.SlabThickness, room.HasFloor, room.HasCeiling);
                boxes.Add(new BoxModelSolverBox(shell, BoxModelPartMode.Add, room, default));
            }

            var materials = new BoxModelSideMaterials(null, room.FloorMaterial, room.CeilingMaterial, room.WallMaterial);
            boxes.Add(new BoxModelSolverBox(MillimeterBox.FromMeters(min, room.Size), BoxModelPartMode.Cut, room, materials));
        }

        // Стены вокруг комнаты: на толщину стен по бокам, на толщину перекрытия снизу и сверху — если там есть пол и потолок.
        // Общее у комнаты и её предпросмотра при рисовании.
        public static MillimeterBox RoomShellOf(Vector3 roomMin, Vector3 roomSize, float wallThickness, float slabThickness, bool hasFloor, bool hasCeiling)
        {
            var below = hasFloor ? slabThickness : 0f;
            var above = hasCeiling ? slabThickness : 0f;
            var shellMin = roomMin - new Vector3(wallThickness, below, wallThickness);
            var shellSize = roomSize + new Vector3(wallThickness * 2f, below + above, wallThickness * 2f);
            return MillimeterBox.FromMeters(shellMin, shellSize);
        }


        // ── Лестница ─────────────────────────────────────────────────────

        // Один марш лестницы: где он поперёк подъёма, откуда (низ марша — расстояние от начала лестницы по подъёму) и куда идёт (+1 — от начала, −1 — к нему),
        // с какой высоты и сколько в нём ступеней.
        private readonly struct StairsFlight
        {
            public StairsFlight(float crossMin, float width, float lowEndRun, float runSign, float baseHeight, int stepCount)
            {
                CrossMin = crossMin;
                Width = width;
                LowEndRun = lowEndRun;
                RunSign = runSign;
                BaseHeight = baseHeight;
                StepCount = stepCount;
            }


            public float CrossMin { get; }
            public float Width { get; }
            public float LowEndRun { get; }
            public float RunSign { get; }
            public float BaseHeight { get; }
            public int StepCount { get; }
        }

        // Вырез над лестницей (если нужен), марши ступенями снизу вверх и скат через носики ступеней каждого марша.
        // Вырез — во весь габарит лестницы, но только там, где достаёт голова: от HeadroomHeight над полом до верха. Ниже — стены и всё вокруг целы.
        // Вырез идёт раньше ступеней — свои ступени он не режет. У лестницы с площадкой: первый марш к дальнему концу, площадка на всю ширину,
        // второй марш обратно, рядом с первым.
        private static void AddStairs(BoxModelStairs stairs, Vector3 min, List<BoxModelSolverBox> boxes, List<BoxModelRampShape> ramps)
        {
            var materials = new BoxModelSideMaterials(stairs.Material);
            var size = stairs.Size;
            var riser = stairs.Height / stairs.StepCount;
            var crossAxis = stairs.RunAxis == 0 ? 2 : 0;

            if (stairs.CutsAbove && stairs.Height > BoxModelStairs.HeadroomHeight)
            {
                var cutMin = min + Vector3.up * BoxModelStairs.HeadroomHeight;
                var cutSize = size;
                cutSize.y = stairs.Height - BoxModelStairs.HeadroomHeight;
                boxes.Add(new BoxModelSolverBox(MillimeterBox.FromMeters(cutMin, cutSize), BoxModelPartMode.Cut, stairs, materials));
            }

            if (!stairs.IsSwitchback)
            {
                var flight = new StairsFlight(min[crossAxis], stairs.Width, 0f, 1f, 0f, stairs.StepCount);
                AddFlight(stairs, min, flight, riser, materials, boxes, ramps);
                return;
            }

            var flightWidth = stairs.FlightWidth;
            var firstCross = stairs.IsFirstFlightOnOtherSide ? min[crossAxis] + stairs.Width - flightWidth : min[crossAxis];
            var secondCross = stairs.IsFirstFlightOnOtherSide ? min[crossAxis] : min[crossAxis] + stairs.Width - flightWidth;
            var landingHeight = stairs.FirstFlightStepCount * riser;

            var first = new StairsFlight(firstCross, flightWidth, stairs.FlightLength - stairs.FirstFlightStepCount * stairs.StepDepth, 1f, 0f, stairs.FirstFlightStepCount);
            AddFlight(stairs, min, first, riser, materials, boxes, ramps);
            AddLanding(stairs, min, landingHeight, materials, boxes);
            var second = new StairsFlight(secondCross, flightWidth, stairs.FlightLength, -1f, landingHeight, stairs.SecondFlightStepCount);
            AddFlight(stairs, min, second, riser, materials, boxes, ramps);
        }

        // Ступени одного марша снизу вверх и его скат.
        private static void AddFlight(BoxModelStairs stairs, Vector3 min, StairsFlight flight, float riser, BoxModelSideMaterials materials,
            List<BoxModelSolverBox> boxes, List<BoxModelRampShape> ramps)
        {
            var runAxis = stairs.RunAxis;
            var crossAxis = runAxis == 0 ? 2 : 0;
            var tread = stairs.StepDepth;
            var isSeparate = stairs.Style == BoxModelStairsStyle.SeparateSteps;

            for (var step = 0; step < flight.StepCount; step++)
            {
                var stepTop = flight.BaseHeight + (step + 1) * riser;
                var stepBottom = isSeparate ? Mathf.Max(0f, stepTop - stairs.StepThickness) : 0f;
                var runA = flight.LowEndRun + flight.RunSign * step * tread;
                var runB = flight.LowEndRun + flight.RunSign * (step + 1) * tread;
                var stepMin = min;
                var stepSize = Vector3.zero;
                stepMin[runAxis] = RunToAxis(stairs, min, Mathf.Min(runA, runB), Mathf.Max(runA, runB), out var stepLength);
                stepMin[crossAxis] = flight.CrossMin;
                stepMin.y = min.y + stepBottom;
                stepSize[runAxis] = stepLength;
                stepSize[crossAxis] = flight.Width;
                stepSize.y = stepTop - stepBottom;
                boxes.Add(new BoxModelSolverBox(MillimeterBox.FromMeters(stepMin, stepSize), BoxModelPartMode.Add, stairs, materials));
            }

            ramps.Add(RampOf(stairs, min, flight, riser, tread));
        }

        // Площадка между маршами — на всю ширину у дальнего конца, верх — на верху первого марша; у отдельных ступеней — плита толщиной в ступень.
        private static void AddLanding(BoxModelStairs stairs, Vector3 min, float landingHeight, BoxModelSideMaterials materials, List<BoxModelSolverBox> boxes)
        {
            var bottom = stairs.Style == BoxModelStairsStyle.SeparateSteps ? Mathf.Max(0f, landingHeight - stairs.StepThickness) : 0f;
            var landingMin = min;
            var landingSize = stairs.Size;
            landingMin[stairs.RunAxis] = RunToAxis(stairs, min, stairs.FlightLength, stairs.Length, out var landingLength);
            landingMin.y = min.y + bottom;
            landingSize[stairs.RunAxis] = landingLength;
            landingSize.y = landingHeight - bottom;
            boxes.Add(new BoxModelSolverBox(MillimeterBox.FromMeters(landingMin, landingSize), BoxModelPartMode.Add, stairs, materials));
        }

        // Скат марша: от пола за ступень до первой ступени, через носики до верха.
        // У сплошной — клин до пола и ровно до конца марша; у отдельных ступеней — тонкая полоса вдоль носиков, под лестницей можно пройти.
        private static BoxModelRampShape RampOf(BoxModelStairs stairs, Vector3 min, StairsFlight flight, float riser, float tread)
        {
            var stepCount = flight.StepCount;
            var bottomStart = new Vector2(-tread, 0f);
            var lastNose = new Vector2((stepCount - 1) * tread, stepCount * riser);
            var profile = stairs.Style == BoxModelStairsStyle.SeparateSteps
                ? new[] { bottomStart, lastNose, lastNose + Vector2.down * SeparateStepsRampThickness, bottomStart + Vector2.down * SeparateStepsRampThickness }
                : new[] { bottomStart, lastNose, new Vector2(stepCount * tread, stepCount * riser), new Vector2(stepCount * tread, -flight.BaseHeight) };

            var crossAxis = stairs.RunAxis == 0 ? 2 : 0;
            var corners = new Vector3[8];

            for (var edge = 0; edge < 2; edge++)
                for (var index = 0; index < profile.Length; index++)
                {
                    var corner = min;
                    corner[stairs.RunAxis] = RunPointToAxis(stairs, min, flight.LowEndRun + flight.RunSign * profile[index].x);
                    corner.y = min.y + flight.BaseHeight + profile[index].y;
                    corner[crossAxis] = flight.CrossMin + edge * flight.Width;
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
