using FeatherKit.LiveBuilder;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Секция эталонного уровня: здание 24 × 12 м на 9 этажей по 3 м в объекте из коробок «Section», как его ставит инструмент «Здание».
    /// - окна 1,5 × 1,5 — по одному на длинную сторону в «Storey 1», повтор 8 вдоль стены и до верха здания
    /// - в «Storey 1» с повтором до верха: лестница с площадкой (без верхнего этажа), шахта лифта 2 × 2 с дверью, 4 балкона на фасаде
    /// - на первом этаже две квартиры: перегородки 0,1 и 4 двери 0,9 × 2,1 — дети перегородок
    /// </summary>
    public static class LiveBuilderReferenceSection
    {
        // Размеры — в осях здания, м: контур от (0, 0) до (Length, Depth), фасад с балконами — сторона Z = 0.
        private const float Length = 24f;
        private const float Depth = 12f;
        private const int StoreyCount = 9;
        private const float WallThickness = 0.3f;
        private const float SlabThickness = 0.2f;
        private const float ParapetHeight = 1f;

        private const int WindowsPerSide = 8;
        private const float WindowSize = 1.5f;
        private const float WindowSill = 0.9f;
        private const float WindowDepth = 0.6f;

        private const float StairCellWidth = 3f;
        private const float StairCellLength = 6f;
        private const float StairsLandingDepth = 1.4f;
        private const float StairsFlightGap = 0.2f;
        private const float StairsStepDepth = 0.3f;
        private const float StairsStepThickness = 0.2f;

        private const float ShaftSize = 2f;
        private const float ShaftWall = 0.2f;
        private const float ShaftDoorWidth = 1f;

        // Дыра шахты на этаже — от метра под его полом до метра над ним: задевает только перекрытие под этажом.
        private const float ShaftOpeningReach = 1f;

        private const float PartitionThickness = 0.1f;
        private const float DoorWidth = 0.9f;
        private const float DoorHeight = 2.1f;
        private const float DoorDepth = 0.3f;

        private const float BalconyWidth = 2.4f;
        private const float BalconyDepth = 1.2f;
        private const float RailThickness = 0.1f;
        private const float RailHeight = 1f;

        private static readonly Vector2 StairCellMin = new(9.1f, 5.7f);
        private static readonly Vector2 ShaftMin = new(12.6f, 9.7f);
        private static readonly float[] BalconyCenters = { 4.5f, 10.5f, 13.5f, 19.5f };


        // Секция в root; measuredWindow — ряд окон фасада, который двигает замер.
        public static SplineBuilding Build(Transform root, out GeometryCutter measuredWindow)
        {
            var section = BoxModelFactory.CreateModel(root.position);
            section.name = "Section";
            section.transform.SetParent(root, true);

            var building = CreateBuilding(section);
            var firstStorey = SplineBuildingStoreys.ContainerOf(building, 0).transform;

            measuredWindow = AddWindows(building, firstStorey);
            AddStairs(section, building, firstStorey);
            AddElevatorShaft(building, firstStorey);
            AddApartments(section, building, firstStorey);
            AddBalconies(section, building, firstStorey);
            return building;
        }


        // ── Здание ───────────────────────────────────────────────────────

        // Числа — свои, не из настроек инструмента: эталон не зависит от того, что выбрано в окне.
        private static SplineBuilding CreateBuilding(BoxModel section)
        {
            var building = BoxModelFactory.CreateBuilding(section, section.transform.position, section.transform.rotation);
            building.name = "Building";
            building.LevelPlacement = new SplineBuildingLevelPlacement();
            building.StoreyCount = StoreyCount;
            building.StoreyHeight = LiveBuilderReferenceLevel.StoreyHeight;
            building.WallType = null;
            building.WallThickness = WallThickness;
            building.ContourSide = SplineBuildingContourSide.Outside;
            building.SlabThickness = SlabThickness;
            building.HasFloor = true;
            building.HasRoof = true;
            building.HasParapet = true;
            building.ParapetHeight = ParapetHeight;
            building.OuterWallMaterial = null;
            building.InnerWallMaterial = null;
            building.FloorMaterial = null;
            building.CeilingMaterial = null;
            building.RoofMaterial = null;

            var spline = building.GetComponent<SplineContainer>().Spline;
            spline.Clear();
            AddKnots(spline, Vector3.zero, new Vector3(Length, 0f, 0f), new Vector3(Length, 0f, Depth), new Vector3(0f, 0f, Depth));
            spline.Closed = true;
            return building;
        }


        // Окно на каждой длинной стене — в первом этаже, остальные даёт его повтор: вдоль стены и до верха здания.
        private static GeometryCutter AddWindows(SplineBuilding building, Transform firstStorey)
        {
            var step = Length / WindowsPerSide;
            var front = AddWindowRow(building, firstStorey, "Front Windows", WallThickness - WindowDepth / 2f, step);
            AddWindowRow(building, firstStorey, "Back Windows", Depth - WallThickness + WindowDepth / 2f, step);
            return front;
        }

        // Окно вровень с внутренней стороной стены, лишнее — наружу: внутри оно вырезало бы и площадку лестницы, стоящую вплотную к стене.
        private static GeometryCutter AddWindowRow(SplineBuilding building, Transform firstStorey, string name, float depthMiddle, float step)
        {
            var bottomCenter = new Vector3(step / 2f, building.FloorTopHeight + WindowSill, depthMiddle);
            var window = BoxModelFactory.CreateCutter(firstStorey, building.transform.TransformPoint(bottomCenter), building.transform.rotation,
                new Vector2(WindowSize, WindowSize), WindowDepth);
            window.name = name;

            var array = RepeatOnStoreys(window.transform, building, false);
            array.CountX = WindowsPerSide;
            array.StepX = step;
            return window;
        }


        // ── Подъезд ──────────────────────────────────────────────────────

        // Лестница — в этаже здания, то есть в объекте самого здания: вырез над маршами пробивает перекрытия, только если он в нём.
        // Марши у дальнего конца клетки, у ближнего — этажная площадка: так клетку размечает инструмент «Лестница».
        private static void AddStairs(BoxModel section, SplineBuilding building, Transform firstStorey)
        {
            var stairs = BoxModelFactory.CreateStairs(section, Vector3.zero, BoxModelStairsDirection.PositiveZ, StairCellWidth, LiveBuilderReferenceLevel.StoreyHeight,
                StairsStepDepth, true);
            stairs.Style = BoxModelStairsStyle.Open;
            stairs.StepThickness = StairsStepThickness;
            stairs.Shape = BoxModelStairsShape.Switchback;
            stairs.LandingDepth = StairsLandingDepth;
            stairs.FlightGap = StairsFlightGap;
            stairs.IsFirstFlightOnOtherSide = false;

            var min = new Vector3(StairCellMin.x, building.FloorTopHeight, StairCellMin.y + StairCellLength - stairs.Length);
            stairs.transform.position = building.transform.TransformPoint(min);
            stairs.transform.SetParent(firstStorey, true);
            RepeatOnStoreys(stairs.transform, building, true);
        }


        // Шахта — вложенный объект на всех этажах: стенки и дверь. Дыра в перекрытиях — отдельно, со второго этажа: пол первого этажа и крыша целы.
        private static void AddElevatorShaft(SplineBuilding building, Transform firstStorey)
        {
            var shaft = BoxModelFactory.CreateGroup(firstStorey);
            shaft.name = "Elevator Shaft";
            shaft.transform.position = building.transform.TransformPoint(new Vector3(ShaftMin.x, building.FloorTopHeight, ShaftMin.y));

            var height = LiveBuilderReferenceLevel.StoreyHeight;
            var inner = ShaftSize - 2f * ShaftWall;
            var doorFrom = (ShaftSize - ShaftDoorWidth) / 2f;
            BoxModelFactory.CreatePart(shaft, Vector3.zero, new Vector3(ShaftSize, height, ShaftSize), BoxModelPartMode.Add);
            BoxModelFactory.CreateCutterBox(shaft, new Vector3(ShaftWall, 0f, ShaftWall), new Vector3(ShaftWall + inner, height, ShaftWall + inner));
            BoxModelFactory.CreateCutterBox(shaft, new Vector3(doorFrom, 0f, -ShaftWall), new Vector3(doorFrom + ShaftDoorWidth, DoorHeight, 2f * ShaftWall));
            RepeatOnStoreys(shaft.transform, building, false);

            AddShaftOpening(building, inner);
        }

        // Дыра — в «Storey 2», повтор до верха: перекрытие под каждым этажом, кроме первого. Внутри шахты она жила бы и на верхнем этаже — пробила бы крышу.
        private static void AddShaftOpening(SplineBuilding building, float inner)
        {
            var secondStorey = SplineBuildingStoreys.ContainerOf(building, 1).transform;
            var middle = ShaftMin + Vector2.one * (ShaftSize / 2f);
            var bottom = building.FloorTopHeight + LiveBuilderReferenceLevel.StoreyHeight - ShaftOpeningReach;
            var opening = BoxModelFactory.CreateCutter(secondStorey, building.transform.TransformPoint(new Vector3(middle.x, bottom, middle.y)),
                building.transform.rotation, new Vector2(inner, 2f * ShaftOpeningReach), inner);
            opening.name = "Shaft Opening";
            opening.Scope = GeometryCutterScope.EverythingItTouches;
            opening.CutTargets = LiveBuilderCutTargets.SlabsOnly;
            RepeatOnStoreys(opening.transform, building, false);
        }


        // ── Квартиры первого этажа ───────────────────────────────────────

        // Перегородки поперёк здания отделяют подъезд, вдоль — делят каждую квартиру на две комнаты; в каждой — дверь.
        private static void AddApartments(BoxModel section, SplineBuilding building, Transform firstStorey)
        {
            var hallWest = AddPartition(section, building, firstStorey, "Partition Hall West", new Vector3(9f, 0f, WallThickness), new Vector3(9f, 0f, Depth - WallThickness));
            var hallEast = AddPartition(section, building, firstStorey, "Partition Hall East", new Vector3(15f, 0f, WallThickness), new Vector3(15f, 0f, Depth - WallThickness));
            var roomsWest = AddPartition(section, building, firstStorey, "Partition Rooms West", new Vector3(WallThickness, 0f, 6f),
                new Vector3(9f - PartitionThickness / 2f, 0f, 6f));
            var roomsEast = AddPartition(section, building, firstStorey, "Partition Rooms East", new Vector3(15f + PartitionThickness / 2f, 0f, 6f),
                new Vector3(Length - WallThickness, 0f, 6f));

            var acrossX = Quaternion.Euler(0f, 90f, 0f);
            AddDoor(hallWest, building, new Vector3(9f, 0f, 3f), acrossX);
            AddDoor(hallEast, building, new Vector3(15f, 0f, 3f), acrossX);
            AddDoor(roomsWest, building, new Vector3(4.5f, 0f, 6f), Quaternion.identity);
            AddDoor(roomsEast, building, new Vector3(19.5f, 0f, 6f), Quaternion.identity);
        }

        // Точки — в осях здания над полом первого этажа. Перегородка — в первом этаже здания, только на нём: так её ставит «Стена».
        private static SplineWall AddPartition(BoxModel section, SplineBuilding building, Transform firstStorey, string name, Vector3 from, Vector3 to)
        {
            var floor = building.transform.TransformPoint(new Vector3(0f, building.FloorTopHeight, 0f));
            var wall = BoxModelFactory.CreateWall(section, floor, building.transform.rotation);
            wall.name = name;
            wall.WallType = null;
            wall.Material = null;
            wall.Thickness = PartitionThickness;
            wall.Height = LiveBuilderReferenceLevel.StoreyHeight - SlabThickness;
            wall.Alignment = SplineWallAlignment.Center;
            AddKnots(wall.GetComponent<SplineContainer>().Spline, from, to);

            wall.transform.SetParent(firstStorey, true);
            return wall;
        }

        private static void AddDoor(SplineWall wall, SplineBuilding building, Vector3 bottomCenter, Quaternion rotation)
        {
            var worldBottom = building.transform.TransformPoint(bottomCenter + Vector3.up * building.FloorTopHeight);
            var door = BoxModelFactory.CreateCutter(wall.transform, worldBottom, building.transform.rotation * rotation, new Vector2(DoorWidth, DoorHeight), DoorDepth);
            door.name = "Door";
        }


        // ── Балконы ──────────────────────────────────────────────────────

        // Плита вровень с перекрытием и три бортика — в группе «Balconies» первого этажа, повтор группы — до верха здания.
        private static void AddBalconies(BoxModel section, SplineBuilding building, Transform firstStorey)
        {
            var balconies = new GameObject("Balconies") { tag = BoxModelPieceTidying.EditorOnlyTag }.transform;
            Undo.RegisterCreatedObjectUndo(balconies.gameObject, "Build Reference Level");
            balconies.SetParent(firstStorey, false);

            var slabTop = building.FloorTopHeight;
            var railTop = slabTop + RailHeight;
            var railInner = -BalconyDepth + RailThickness;

            foreach (var center in BalconyCenters)
            {
                var left = center - BalconyWidth / 2f;
                var right = center + BalconyWidth / 2f;
                AddBalconyBox(section, building, balconies, new Vector3(left, slabTop - SlabThickness, -BalconyDepth), new Vector3(right, slabTop, 0f));
                AddBalconyBox(section, building, balconies, new Vector3(left, slabTop, -BalconyDepth), new Vector3(right, railTop, railInner));
                AddBalconyBox(section, building, balconies, new Vector3(left, slabTop, railInner), new Vector3(left + RailThickness, railTop, 0f));
                AddBalconyBox(section, building, balconies, new Vector3(right - RailThickness, slabTop, railInner), new Vector3(right, railTop, 0f));
            }

            RepeatOnStoreys(balconies, building, false);
        }

        // Углы — в осях здания.
        private static void AddBalconyBox(BoxModel section, SplineBuilding building, Transform balconies, Vector3 min, Vector3 max)
        {
            var modelMin = section.transform.InverseTransformPoint(building.transform.TransformPoint(min));
            var modelMax = section.transform.InverseTransformPoint(building.transform.TransformPoint(max));
            var box = BoxModelFactory.CreatePart(section, modelMin, modelMax, BoxModelPartMode.Add);
            box.transform.SetParent(balconies, true);
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Повтор по этажам — как «И этажи выше» у инструментов: Y до верха здания, шаг — этаж.
        private static LiveBuilderArray RepeatOnStoreys(Transform node, SplineBuilding building, bool skipsTopStorey)
        {
            SplineBuildingInteriorPlacement.RepeatOnStoreysAbove(node, building, skipsTopStorey);
            return node.GetComponent<LiveBuilderArray>();
        }


        private static void AddKnots(Spline spline, params Vector3[] points)
        {
            foreach (var point in points)
                spline.Add(new BezierKnot((float3)point), TangentMode.Linear);
        }
    }
}
