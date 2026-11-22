using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Строит геометрию здания по контуру по частям — стены и пол каждого этажа, крыша: стены по замкнутому пути (тем же кодом,
    /// что стену по линии) и перекрытия — плиты точно по форме контура, до внутренней стороны стен.
    /// Высоты — от линии контура: на ней низ плиты пола (здание стоит на точке опоры), над ней — этажи и крыша.
    /// Дыра в перекрытии — прямоугольник вырезающей коробки в плане: треугольники плиты обрезаются по нему; торцы плиты — по краям
    /// общей пустоты дыр, внутри соседней дыры торца нет.
    /// Части меша — по материалам: стены снаружи (и откосы), стены изнутри, пол, потолок, крыша.
    /// Контур, который не разрезался на треугольники целиком (пересекает сам себя), — одно предупреждение в консоли на здание.
    /// </summary>
    public static class SplineBuildingMeshBuilder
    {
        // Номера частей меша — в этом порядке и материалы у рендерера.
        public const int OuterWallSubmesh = 0;
        public const int InnerWallSubmesh = 1;
        public const int FloorSubmesh = 2;
        public const int CeilingSubmesh = 3;
        public const int RoofSubmesh = 4;
        public const int SubmeshCount = 5;

        // На столько от края дыры смотрим наружу, есть ли там плита или уже соседняя дыра, м.
        private const float HoleEdgeProbeOffset = 0.001f;

        // Здания, о чьём сломанном контуре уже предупредили: не на каждую пересборку. Починили контур — забываем.
        private static readonly HashSet<EntityId> BuildingsWarnedAboutContour = new();


        // Геометрия частей здания, которые просит isPartNeeded (null — всех): стены и пол каждого этажа, крыша.
        // Стены этажа — от низа его плиты до низа плиты следующего, у верхнего — до верха крыши; так спрятанный этаж сверху закрыт срезом стен.
        public static List<SplineBuildingPartGeometry> BuildParts(SplineBuilding building, IReadOnlyList<SplineWallPath> paths,
            IReadOnlyList<List<SplineWallHole>> wallHolesByPath, IReadOnlyList<SplineBuildingSlabHole> slabHoles,
            System.Func<SplineBuildingPartKind, int, bool> isPartNeeded = null)
        {
            var parts = new List<SplineBuildingPartGeometry>();

            foreach (var (kind, storey) in PartsOf(building))
                if (isPartNeeded == null || isPartNeeded(kind, storey))
                    parts.Add(new SplineBuildingPartGeometry(kind, storey));

            var slabParts = parts.FindAll(part => part.Kind != SplineBuildingPartKind.Walls);
            var isContourComplete = true;

            for (var index = 0; index < paths.Count; index++)
            {
                var path = paths[index];
                var insideSign = InsideSignOf(path);
                GetWallOffsets(building, insideSign, out var leftOffset, out var rightOffset);

                foreach (var part in parts)
                {
                    if (part.Kind != SplineBuildingPartKind.Walls)
                        continue;

                    var isTopStorey = part.Storey == building.StoreyCount - 1;
                    var submeshes = new SplineWallFaceSubmeshes(insideSign > 0f ? InnerWallSubmesh : OuterWallSubmesh,
                        insideSign > 0f ? OuterWallSubmesh : InnerWallSubmesh, isTopStorey ? RoofSubmesh : OuterWallSubmesh, OuterWallSubmesh);
                    SplineWallMeshBuilder.AddWalls(part.Geometry, path, wallHolesByPath[index], leftOffset, rightOffset,
                        WallsBottomOf(building, part.Storey), WallsTopOf(building, part.Storey), submeshes);
                }

                var innerOffset = insideSign > 0f ? rightOffset : leftOffset;

                if (slabParts.Count > 0)
                    isContourComplete &= AddSlabs(slabParts, building, path, innerOffset, slabHoles);
            }

            // Плиты у здания есть, но их не строили — контур не разрезали: прежнее предупреждение остаётся как было.
            if (slabParts.Count > 0 || !HasSlabs(building))
                ReportBrokenContour(building, isContourComplete);

            return parts;
        }


        // Контур не разрезался на треугольники целиком — часть плит пропала: сказать один раз, со ссылкой на здание.
        private static void ReportBrokenContour(SplineBuilding building, bool isContourComplete)
        {
            if (isContourComplete || !HasSlabs(building))
            {
                BuildingsWarnedAboutContour.Remove(building.GetEntityId());
                return;
            }

            if (BuildingsWarnedAboutContour.Add(building.GetEntityId()))
                Debug.LogWarning("Live Builder: the contour of building '" + building.name + "' crosses itself, part of the floor and roof is not built.", building);
        }

        private static bool HasSlabs(SplineBuilding building)
        {
            return building.HasFloor || building.HasRoof || building.StoreyCount > 1;
        }


        // Какие части должны быть у здания по его числам, по порядку: стены и пол каждого этажа, крыша.
        public static IEnumerable<(SplineBuildingPartKind Kind, int Storey)> PartsOf(SplineBuilding building)
        {
            for (var storey = 0; storey < building.StoreyCount; storey++)
            {
                yield return (SplineBuildingPartKind.Walls, storey);

                if (storey > 0 || building.HasFloor)
                    yield return (SplineBuildingPartKind.Slab, storey);
            }

            if (building.HasRoof)
                yield return (SplineBuildingPartKind.Roof, 0);
        }


        // Контур здания пересекает сам себя — по его точкам, сразу, или пол не разрезался целиком при сборке. Для предупреждения в инспекторе.
        public static bool IsContourBroken(SplineBuilding building)
        {
            if (building == null)
                return false;

            if (BuildingsWarnedAboutContour.Contains(building.GetEntityId()))
                return true;

            if (!building.TryGetComponent<UnityEngine.Splines.SplineContainer>(out var container))
                return false;

            foreach (var spline in container.Splines)
            {
                var knots = new List<Vector2>();

                foreach (var knot in spline.Knots)
                    knots.Add(new Vector2(knot.Position.x, knot.Position.z));

                if (knots.Count >= 4 && PlanPolygons.IsSelfCrossing(knots))
                    return true;
            }

            return false;
        }


        // Низ стен этажа над контуром, м: у первого — от линии контура, у остальных — от низа плиты этажа.
        public static float WallsBottomOf(SplineBuilding building, int storey)
        {
            return storey == 0 ? 0f : FloorTopOf(building, storey) - building.SlabThickness;
        }


        // Верх стен этажа над контуром, м: у верхнего — до крыши, а с парапетом стены продолжаются над ней бортиком.
        public static float WallsTopOf(SplineBuilding building, int storey)
        {
            return storey == building.StoreyCount - 1 ? building.TotalHeight + building.ParapetRise : FloorTopOf(building, storey + 1) - building.SlabThickness;
        }


        // Верх пола этажа над контуром, м.
        private static float FloorTopOf(SplineBuilding building, int storey)
        {
            return building.FloorTopHeight + storey * building.StoreyHeight;
        }


        // Материалы по частям меша; пустые — как стены снаружи, а те — серый материал набора.
        public static Material[] MaterialsOf(SplineBuilding building, Material fallback)
        {
            var outer = building.OuterWallMaterial != null ? building.OuterWallMaterial : fallback;
            return new[]
            {
                outer,
                building.InnerWallMaterial != null ? building.InnerWallMaterial : outer,
                building.FloorMaterial != null ? building.FloorMaterial : outer,
                building.CeilingMaterial != null ? building.CeilingMaterial : outer,
                building.RoofMaterial != null ? building.RoofMaterial : outer
            };
        }


        // ── Стены ────────────────────────────────────────────────────────

        // С какой стороны пути внутренность здания: +1 — справа по ходу линии, −1 — слева.
        public static float InsideSignOf(SplineWallPath path)
        {
            var middle = path.Distances[1] / 2f;
            var probe = path.PointAt(middle, 0.05f, 0f);
            return PlanPolygons.IsInside(Flat(probe), ContourOf(path, 0f)) ? 1f : -1f;
        }

        // Внутренняя сторона стен здания в плане (X, Z здания) — по ней встают перегородки комнат.
        public static List<Vector2> InnerContourOf(SplineBuilding building, SplineWallPath path)
        {
            return ContourOf(path, InnerOffsetOf(building, path));
        }


        // То же по всем сплайнам здания — по контуру на каждый.
        public static List<List<Vector2>> InnerContoursOf(SplineBuilding building)
        {
            var contours = new List<List<Vector2>>();

            foreach (var path in SplineWallPath.FromContainer(building.GetComponent<UnityEngine.Splines.SplineContainer>(), building.CurveStep))
                contours.Add(InnerContourOf(building, path));

            return contours;
        }


        // Где стороны стен здания относительно этого контура: смещения влево и вправо по ходу линии.
        public static void GetWallOffsets(SplineBuilding building, SplineWallPath path, out float leftOffset, out float rightOffset)
        {
            GetWallOffsets(building, InsideSignOf(path), out leftOffset, out rightOffset);
        }


        // Внутренняя сторона стен — смещение от контура по ходу линии (плюс — вправо): по ней встают перегородки.
        public static float InnerOffsetOf(SplineBuilding building, SplineWallPath path)
        {
            var insideSign = InsideSignOf(path);
            GetWallOffsets(building, insideSign, out var leftOffset, out var rightOffset);
            return insideSign > 0f ? rightOffset : leftOffset;
        }


        // Стены от контура внутрь (контур снаружи), наружу (контур внутри) или поровну.
        private static void GetWallOffsets(SplineBuilding building, float insideSign, out float leftOffset, out float rightOffset)
        {
            var thickness = building.WallThickness;
            var inward = insideSign * thickness;

            switch (building.ContourSide)
            {
                case SplineBuildingContourSide.Outside:
                    leftOffset = Mathf.Min(0f, inward);
                    rightOffset = Mathf.Max(0f, inward);
                    break;

                case SplineBuildingContourSide.Inside:
                    leftOffset = Mathf.Min(0f, -inward);
                    rightOffset = Mathf.Max(0f, -inward);
                    break;

                default:
                    leftOffset = -thickness / 2f;
                    rightOffset = thickness / 2f;
                    break;
            }
        }


        // ── Перекрытия ───────────────────────────────────────────────────

        // Плиты частей: пол снизу, перекрытия между этажами, крыша сверху — по внутренней стороне стен. Возвращает, разрезался ли контур на треугольники целиком.
        private static bool AddSlabs(List<SplineBuildingPartGeometry> slabParts, SplineBuilding building, SplineWallPath path, float innerOffset,
            IReadOnlyList<SplineBuildingSlabHole> slabHoles)
        {
            var outline = ContourOf(path, innerOffset);
            var baseHeight = path.BaseHeightAt(0f);
            var triangles = PlanPolygons.Triangulate(outline, out var isContourComplete);

            foreach (var part in slabParts)
            {
                var (bottom, top) = SlabHeightsOf(building, part.Kind, part.Storey, baseHeight);
                var bottomSubmesh = part.Kind == SplineBuildingPartKind.Slab && part.Storey == 0 ? -1 : CeilingSubmesh;
                AddSlab(part.Geometry, outline, triangles, slabHoles, bottom, top, part.Kind == SplineBuildingPartKind.Roof ? RoofSubmesh : FloorSubmesh, bottomSubmesh);
            }

            return isContourComplete;
        }


        // Низ и верх плиты части — пола, перекрытия или крыши — над нулём здания, если линия контура на высоте baseHeight.
        public static (float Bottom, float Top) SlabHeightsOf(SplineBuilding building, SplineBuildingPartKind kind, int storey, float baseHeight)
        {
            if (kind == SplineBuildingPartKind.Roof)
            {
                var roofTop = baseHeight + building.TotalHeight;
                return (roofTop - building.SlabThickness, roofTop);
            }

            if (storey == 0)
                return (baseHeight, baseHeight + building.FloorTopHeight);

            var top = baseHeight + FloorTopOf(building, storey);
            return (top - building.SlabThickness, top);
        }


        // Плита: верх и низ — треугольники контура за вычетом дыр, которые проходят через эту плиту, и торцы по краям дыр (частью меша 0).
        // Низ −1 — без нижней стороны. Общая у перекрытий здания и пола с потолком замкнутой стены (<see cref="SplineWallMeshBuilder"/>).
        public static void AddSlab(SplineWallGeometry geometry, List<Vector2> outline, List<Vector2[]> triangles,
            IReadOnlyList<SplineBuildingSlabHole> slabHoles, float bottom, float top, int topSubmesh, int bottomSubmesh)
        {
            var pieces = new List<List<Vector2>>();
            var holesThrough = new List<List<Vector2>>();

            foreach (var triangle in triangles)
                pieces.Add(new List<Vector2>(triangle));

            foreach (var hole in slabHoles)
                if (IsHoleThrough(hole, bottom, top))
                {
                    pieces = PlanPolygons.SubtractConvex(pieces, hole.Corners);
                    holesThrough.Add(new List<Vector2>(hole.Corners));
                }

            for (var index = 0; index < holesThrough.Count; index++)
                AddHoleEdges(geometry, outline, holesThrough, index, bottom, top);

            foreach (var piece in pieces)
                for (var index = 1; index < piece.Count - 1; index++)
                {
                    AddFlatTriangle(geometry, piece[0], piece[index], piece[index + 1], top, Vector3.up, topSubmesh);

                    if (bottomSubmesh >= 0)
                        AddFlatTriangle(geometry, piece[0], piece[index], piece[index + 1], bottom, Vector3.down, bottomSubmesh);
                }
        }

        // С допуском в миллиметр: вырез вровень с плитой (до потолка) не пробивает соседнее перекрытие из-за погрешности.
        public static bool IsHoleThrough(SplineBuildingSlabHole hole, float bottom, float top)
        {
            return hole.Bottom < top - 0.001f && hole.Top > bottom + 0.001f;
        }

        // Торцы плиты по краям дыры holeIndex — там, где край идёт по плите: не по пустоте за стенами и не по соседней дыре,
        // которая заходит на эту. Лицом внутрь дыры.
        private static void AddHoleEdges(SplineWallGeometry geometry, List<Vector2> outline, List<List<Vector2>> holes, int holeIndex, float bottom, float top)
        {
            var corners = new List<Vector2>(holes[holeIndex]);
            var otherHoles = new List<List<Vector2>>(holes);
            otherHoles.RemoveAt(holeIndex);

            if (PlanPolygons.SignedArea(corners) < 0f)
                corners.Reverse();

            geometry.SetTextureAxes(SplineWallTextureAxes.HoleEdge());

            for (var index = 0; index < corners.Count; index++)
            {
                var start = corners[index];
                var end = corners[(index + 1) % corners.Count];
                var direction = (end - start).normalized;
                var intoHole = new Vector3(-direction.y, 0f, direction.x);
                var awayFromHole = new Vector2(direction.y, -direction.x) * HoleEdgeProbeOffset;

                foreach (var (insideFrom, insideTo) in PlanPolygons.PartsInside(start, end, outline))
                foreach (var (from, to) in PlanPolygons.PartsClearAside(insideFrom, insideTo, otherHoles, awayFromHole))
                    geometry.AddQuad(
                        new SplineWallVertex(new Vector3(from.x, bottom, from.y), intoHole, new Vector2(Vector2.Distance(start, from), bottom)),
                        new SplineWallVertex(new Vector3(to.x, bottom, to.y), intoHole, new Vector2(Vector2.Distance(start, to), bottom)),
                        new SplineWallVertex(new Vector3(to.x, top, to.y), intoHole, new Vector2(Vector2.Distance(start, to), top)),
                        new SplineWallVertex(new Vector3(from.x, top, from.y), intoHole, new Vector2(Vector2.Distance(start, from), top)),
                        OuterWallSubmesh);
            }
        }

        private static void AddFlatTriangle(SplineWallGeometry geometry, Vector2 first, Vector2 second, Vector2 third, float height, Vector3 normal, int submesh)
        {
            geometry.SetTextureAxes(SplineWallTextureAxes.Plan(SplineWallFaceKind.Flat));
            geometry.AddTriangle(FlatVertex(first, height, normal), FlatVertex(second, height, normal), FlatVertex(third, height, normal), submesh);
        }

        private static SplineWallVertex FlatVertex(Vector2 point, float height, Vector3 normal)
        {
            return new SplineWallVertex(new Vector3(point.x, height, point.y), normal, point);
        }


        // ── Многоугольник ────────────────────────────────────────────────

        // Контур в плане (X, Z) со смещением вбок от линии; последняя точка замкнутого пути совпадает с первой — без неё.
        public static List<Vector2> ContourOf(SplineWallPath path, float sideOffset)
        {
            var outline = new List<Vector2>();
            var count = path.IsClosed ? path.Points.Count - 1 : path.Points.Count;

            for (var index = 0; index < count; index++)
                outline.Add(Flat(path.PointAt(path.Distances[index], sideOffset, 0f)));

            return outline;
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static Vector2 Flat(Vector3 point)
        {
            return new Vector2(point.x, point.z);
        }
    }


    /// <summary>Геометрия одной части здания, пока её собирают: что за часть, какой этаж и копилка вершин.</summary>
    public sealed class SplineBuildingPartGeometry
    {
        public SplineBuildingPartGeometry(SplineBuildingPartKind kind, int storey)
        {
            Kind = kind;
            Storey = storey;
        }


        public SplineBuildingPartKind Kind { get; }
        public int Storey { get; }
        public SplineWallGeometry Geometry { get; } = new(SplineBuildingMeshBuilder.SubmeshCount);
    }


    /// <summary>Дыра в перекрытиях здания: выпуклый четырёхугольник в плане (X, Z здания) и от какой до какой высоты над контуром, м.</summary>
    public readonly struct SplineBuildingSlabHole
    {
        public SplineBuildingSlabHole(Vector2[] corners, float bottom, float top)
        {
            Corners = corners;
            Bottom = bottom;
            Top = top;
        }


        public Vector2[] Corners { get; }
        public float Bottom { get; }
        public float Top { get; }
    }
}
