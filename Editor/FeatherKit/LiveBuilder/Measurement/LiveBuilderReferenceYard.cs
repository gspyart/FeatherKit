using FeatherKit.LiveBuilder;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Двор эталонного уровня: отдельно стоящий объект ровно на 300 коробок с общей фаской рёбер — на нём меряют пересборку большого объекта.
    /// - цоколь 30 м с 28 нишами (вырез-коробка с повтором), ограда поверху — повтор на 150 столбиков, колоннада — 120 коробок сеткой
    /// - пандус, 4 колонны Live Builder, стенка с аркой — вне 300 коробок: колонна и стена — свои постройки
    /// - рядом навес — здание 6 × 4 в один этаж с двускатной крышей
    /// </summary>
    public static class LiveBuilderReferenceYard
    {
        private const float BevelSize = 0.02f;

        private const float PlinthLength = 30f;
        private const float PlinthHeight = 0.6f;
        private const float PlinthDepth = 0.6f;
        private const int NicheCount = 28;
        private const float NicheStep = 1.05f;
        private const int FencePostCount = 150;
        private const float FencePostStep = 0.2f;

        private const int ColumnsPerRow = 12;
        private const int ColumnRows = 10;
        private const float ColumnStep = 2.5f;
        private const int MeasuredColumnRow = 4;
        private const int MeasuredColumnInRow = 5;

        // Пандус поднимается к +Z.
        private const int RampRiseFace = 4;

        private const int RoundColumnCount = 4;
        private const int RoundColumnSides = 12;
        private const float RoundColumnStep = 2.5f;

        private const float ArchWallHeight = 3f;
        private const float ArchWallThickness = 0.3f;
        private const float ArchWidth = 2f;
        private const float ArchHeight = 2.5f;
        private const float ArchDepth = 0.6f;

        private const float CanopyLength = 6f;
        private const float CanopyWidth = 4f;
        private const float CanopyHeight = 3f;
        private const float CanopyWallThickness = 0.2f;
        private const float CanopySlabThickness = 0.2f;
        private const float CanopyRoofPitch = 30f;
        private const float CanopyRoofOverhang = 0.3f;

        // Углы коробок — в осях двора, м.
        private static readonly Vector3 NicheMin = new(0.6f, 0.15f, -0.1f);
        private static readonly Vector3 NicheMax = new(1.2f, 0.45f, 0.2f);
        private static readonly Vector3 FencePostMin = new(0.05f, PlinthHeight, 0.25f);
        private static readonly Vector3 FencePostMax = new(0.15f, 1.8f, 0.35f);
        private static readonly Vector3 ColonnadeStart = new(1f, 0f, 3f);
        private static readonly Vector3 ColumnSize = new(0.4f, 3f, 0.4f);
        private static readonly Vector3 RampMin = new(31f, 0f, 0f);
        private static readonly Vector3 RampMax = new(33f, 1f, 6f);
        private static readonly Vector3 RoundColumnStart = new(35f, 0f, 0f);
        private static readonly Vector3 RoundColumnSize = new(0.5f, 3f, 0.5f);
        private static readonly Vector3 ArchWallStart = new(30.5f, 0f, 9f);
        private static readonly Vector3 ArchWallEnd = new(38f, 0f, 9f);
        private static readonly Vector3 CanopyCorner = new(40f, 0f, 0f);


        // Двор в root, угол — в worldPosition; measuredColumn — колонна, которую двигает замер.
        public static BoxModel Build(Transform root, Vector3 worldPosition, out BoxModelPart measuredColumn)
        {
            var yard = BoxModelFactory.CreateModel(worldPosition);
            yard.name = "Yard";
            yard.transform.SetParent(root, true);
            yard.BevelSize = BevelSize;
            yard.BevelSegments = 1;

            AddPlinthWithFence(yard);
            measuredColumn = AddColonnade(yard);
            AddRamp(yard);
            AddRoundColumns(yard);
            AddArchWall(yard);
            AddCanopy(root, yard.transform.TransformPoint(CanopyCorner));
            return yard;
        }


        // ── Части двора ──────────────────────────────────────────────────

        private static void AddPlinthWithFence(BoxModel yard)
        {
            BoxModelFactory.CreatePart(yard, Vector3.zero, new Vector3(PlinthLength, PlinthHeight, PlinthDepth), BoxModelPartMode.Add);

            var niches = CreateRepeatGroup(yard, "Niches", NicheCount, NicheStep);
            BoxModelFactory.CreateCutterBox(yard, NicheMin, NicheMax).transform.SetParent(niches, true);

            var fence = CreateRepeatGroup(yard, "Fence", FencePostCount, FencePostStep);
            BoxModelFactory.CreatePart(yard, FencePostMin, FencePostMax, BoxModelPartMode.Add).transform.SetParent(fence, true);
        }

        // Группа с повтором по X двора: коробка или вырез в ней — столько раз с этим шагом.
        private static Transform CreateRepeatGroup(BoxModel yard, string name, int count, float stepX)
        {
            var groupObject = new GameObject(name) { tag = BoxModelPieceTidying.EditorOnlyTag };
            Undo.RegisterCreatedObjectUndo(groupObject, "Build Reference Level");
            groupObject.transform.SetParent(yard.transform, false);

            var array = groupObject.AddComponent<LiveBuilderArray>();
            array.CountX = count;
            array.CountY = 1;
            array.CountZ = 1;
            array.StepX = stepX;
            return groupObject.transform;
        }


        private static BoxModelPart AddColonnade(BoxModel yard)
        {
            BoxModelPart measuredColumn = null;

            for (var row = 0; row < ColumnRows; row++)
            for (var index = 0; index < ColumnsPerRow; index++)
            {
                var min = ColonnadeStart + new Vector3(index * ColumnStep, 0f, row * ColumnStep);
                var column = BoxModelFactory.CreatePart(yard, min, min + ColumnSize, BoxModelPartMode.Add);

                if (row == MeasuredColumnRow && index == MeasuredColumnInRow)
                    measuredColumn = column;
            }

            return measuredColumn;
        }


        // Скат во всю длину: от низа ближней стороны до верха дальней.
        private static void AddRamp(BoxModel yard)
        {
            var ramp = BoxModelFactory.CreatePart(yard, RampMin, RampMax, BoxModelPartMode.Add);
            BoxModelRamps.MakeRamp(ramp, RampRiseFace);
        }


        // ── Постройки вне 300 коробок ────────────────────────────────────

        // Ряд колонн за пандусом, в осях двора; стороны — свои, не из настроек окна.
        private static void AddRoundColumns(BoxModel yard)
        {
            for (var index = 0; index < RoundColumnCount; index++)
            {
                var min = RoundColumnStart + new Vector3(0f, 0f, index * RoundColumnStep);
                var column = LiveBuilderColumnFactory.CreateInBox(yard, yard.transform.localToWorldMatrix, min, min + RoundColumnSize);
                column.Sides = RoundColumnSides;
            }
        }


        // Стенка по линии в объекте двора; арка — вырез со скруглённым верхом, режет стену вычитанием.
        private static void AddArchWall(BoxModel yard)
        {
            var wall = BoxModelFactory.CreateWall(yard, yard.transform.position, yard.transform.rotation);
            wall.name = "Arch Wall";
            wall.WallType = null;
            wall.Material = null;
            wall.Thickness = ArchWallThickness;
            wall.Height = ArchWallHeight;
            wall.Alignment = SplineWallAlignment.Center;
            AddKnots(wall.GetComponent<SplineContainer>().Spline, ArchWallStart, ArchWallEnd);

            var bottomCenter = yard.transform.TransformPoint((ArchWallStart + ArchWallEnd) / 2f);
            var arch = BoxModelFactory.CreateCutter(wall.transform, bottomCenter, yard.transform.rotation, new Vector2(ArchWidth, ArchHeight), ArchDepth);
            arch.name = "Arch";
            arch.TopCornerRadius = ArchWidth / 2f;
        }


        // Навес — свой объект рядом с двором: здание в один этаж и двускатная крыша над ним.
        private static void AddCanopy(Transform root, Vector3 worldCorner)
        {
            var canopy = BoxModelFactory.CreateModel(worldCorner);
            canopy.name = "Canopy";
            canopy.transform.SetParent(root, true);

            var building = BoxModelFactory.CreateBuilding(canopy, worldCorner, canopy.transform.rotation);
            building.name = "Canopy Building";
            building.LevelPlacement = new SplineBuildingLevelPlacement();
            building.StoreyCount = 1;
            building.StoreyHeight = CanopyHeight;
            building.WallType = null;
            building.WallThickness = CanopyWallThickness;
            building.ContourSide = SplineBuildingContourSide.Outside;
            building.SlabThickness = CanopySlabThickness;
            building.HasFloor = true;
            building.HasRoof = true;
            building.HasParapet = false;
            building.OuterWallMaterial = null;
            building.InnerWallMaterial = null;
            building.FloorMaterial = null;
            building.CeilingMaterial = null;
            building.RoofMaterial = null;

            var spline = building.GetComponent<SplineContainer>().Spline;
            spline.Clear();
            AddKnots(spline, Vector3.zero, new Vector3(CanopyLength, 0f, 0f), new Vector3(CanopyLength, 0f, CanopyWidth), new Vector3(0f, 0f, CanopyWidth));
            spline.Closed = true;

            SplineBuildingGableRoof.Build(building, CanopyRoofPitch, CanopyRoofOverhang);
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static void AddKnots(Spline spline, params Vector3[] points)
        {
            foreach (var point in points)
                spline.Add(new BezierKnot((float3)point), TangentMode.Linear);
        }
    }
}
