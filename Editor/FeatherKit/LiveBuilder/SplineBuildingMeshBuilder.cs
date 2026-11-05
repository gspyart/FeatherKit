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


        // Геометрия каждой части здания: стены и пол каждого этажа, крыша. Стены этажа — от низа его плиты до низа плиты следующего,
        // у верхнего — до верха крыши; так спрятанный этаж сверху закрыт срезом стен.
        public static List<SplineBuildingPartGeometry> BuildParts(SplineBuilding building, IReadOnlyList<SplineWallPath> paths,
            IReadOnlyList<List<SplineWallHole>> wallHolesByPath, IReadOnlyList<SplineBuildingSlabHole> slabHoles)
        {
            var parts = new List<SplineBuildingPartGeometry>();
            var storeyCount = building.StoreyCount;

            for (var storey = 0; storey < storeyCount; storey++)
            {
                parts.Add(new SplineBuildingPartGeometry(SplineBuildingPartKind.Walls, storey));

                if (storey > 0 || building.HasFloor)
                    parts.Add(new SplineBuildingPartGeometry(SplineBuildingPartKind.Floor, storey));
            }

            if (building.HasRoof)
                parts.Add(new SplineBuildingPartGeometry(SplineBuildingPartKind.Roof, 0));

            var isContourComplete = true;

            for (var index = 0; index < paths.Count; index++)
            {
                var path = paths[index];
                var insideSign = InsideSignOf(path);
                GetWallOffsets(building, insideSign, out var leftOffset, out var rightOffset);

                for (var storey = 0; storey < storeyCount; storey++)
                {
                    var isTopStorey = storey == storeyCount - 1;
                    var submeshes = new SplineWallFaceSubmeshes(insideSign > 0f ? InnerWallSubmesh : OuterWallSubmesh,
                        insideSign > 0f ? OuterWallSubmesh : InnerWallSubmesh, isTopStorey ? RoofSubmesh : OuterWallSubmesh, OuterWallSubmesh);
                    var bottom = storey == 0 ? 0f : FloorTopOf(building, storey) - building.SlabThickness;
                    var top = isTopStorey ? building.TotalHeight : FloorTopOf(building, storey + 1) - building.SlabThickness;
                    SplineWallMeshBuilder.AddWalls(Find(parts, SplineBuildingPartKind.Walls, storey).Geometry, path, wallHolesByPath[index],
                        leftOffset, rightOffset, bottom, top, submeshes);
                }

                var innerOffset = insideSign > 0f ? rightOffset : leftOffset;
                isContourComplete &= AddSlabs(parts, building, path, innerOffset, slabHoles);
            }

            ReportBrokenContour(building, isContourComplete);
            return parts;
        }

        // Контур не разрезался на треугольники целиком — часть плит пропала: сказать один раз, со ссылкой на здание.
        private static void ReportBrokenContour(SplineBuilding building, bool isContourComplete)
        {
            var hasSlabs = building.HasFloor || building.HasRoof || building.StoreyCount > 1;

            if (isContourComplete || !hasSlabs)
            {
                BuildingsWarnedAboutContour.Remove(building.GetEntityId());
                return;
            }

            if (BuildingsWarnedAboutContour.Add(building.GetEntityId()))
                Debug.LogWarning("Live Builder: контур здания «" + building.name + "» пересекает сам себя — часть пола и крыши не построена.", building);
        }

        // Верх пола этажа над контуром, м.
        private static float FloorTopOf(SplineBuilding building, int storey)
        {
            return building.FloorTopHeight + storey * building.StoreyHeight;
        }

        private static SplineBuildingPartGeometry Find(List<SplineBuildingPartGeometry> parts, SplineBuildingPartKind kind, int storey)
        {
            return parts.Find(part => part.Kind == kind && (kind == SplineBuildingPartKind.Roof || part.Storey == storey));
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

        // Пол снизу, перекрытия между этажами, крыша сверху — плиты по внутренней стороне стен, каждая в свою часть.
        // Возвращает, разрезался ли контур на треугольники целиком.
        private static bool AddSlabs(List<SplineBuildingPartGeometry> parts, SplineBuilding building, SplineWallPath path, float innerOffset,
            IReadOnlyList<SplineBuildingSlabHole> slabHoles)
        {
            var outline = ContourOf(path, innerOffset);
            var baseHeight = path.BaseHeightAt(0f);
            var triangles = PlanPolygons.Triangulate(outline, out var isContourComplete);
            var slab = building.SlabThickness;

            if (building.HasFloor)
                AddSlab(Find(parts, SplineBuildingPartKind.Floor, 0).Geometry, outline, triangles, slabHoles, baseHeight, baseHeight + building.FloorTopHeight,
                    FloorSubmesh, -1);

            for (var storey = 1; storey < building.StoreyCount; storey++)
            {
                var top = baseHeight + FloorTopOf(building, storey);
                AddSlab(Find(parts, SplineBuildingPartKind.Floor, storey).Geometry, outline, triangles, slabHoles, top - slab, top, FloorSubmesh, CeilingSubmesh);
            }

            if (building.HasRoof)
            {
                var roofTop = baseHeight + building.TotalHeight;
                AddSlab(Find(parts, SplineBuildingPartKind.Roof, 0).Geometry, outline, triangles, slabHoles, roofTop - slab, roofTop, RoofSubmesh, CeilingSubmesh);
            }

            return isContourComplete;
        }

        // Плита: верх и низ — треугольники контура за вычетом дыр, которые проходят через эту плиту, и торцы по краям дыр.
        // Низ −1 — без нижней стороны.
        private static void AddSlab(SplineWallGeometry geometry, List<Vector2> outline, List<Vector2[]> triangles,
            IReadOnlyList<SplineBuildingSlabHole> slabHoles, float bottom, float top, int topSubmesh, int bottomSubmesh)
        {
            var pieces = new List<List<Vector2>>();
            var holesThrough = new List<List<Vector2>>();

            foreach (var triangle in triangles)
                pieces.Add(new List<Vector2>(triangle));

            foreach (var hole in slabHoles)
                // С допуском в миллиметр: вырез вровень с плитой (до потолка) не пробивает соседнее перекрытие из-за погрешности.
                if (hole.Bottom < top - 0.001f && hole.Top > bottom + 0.001f)
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

        // Торцы плиты по краям дыры holeIndex — там, где край идёт по плите: не по пустоте за стенами и не по соседней дыре,
        // которая заходит на эту. Лицом внутрь дыры.
        private static void AddHoleEdges(SplineWallGeometry geometry, List<Vector2> outline, List<List<Vector2>> holes, int holeIndex, float bottom, float top)
        {
            var corners = new List<Vector2>(holes[holeIndex]);
            var otherHoles = new List<List<Vector2>>(holes);
            otherHoles.RemoveAt(holeIndex);

            if (PlanPolygons.SignedArea(corners) < 0f)
                corners.Reverse();

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
            geometry.AddTriangle(FlatVertex(first, height, normal), FlatVertex(second, height, normal), FlatVertex(third, height, normal), submesh);
        }

        private static SplineWallVertex FlatVertex(Vector2 point, float height, Vector3 normal)
        {
            return new SplineWallVertex(new Vector3(point.x, height, point.y), normal, point);
        }


        // ── Многоугольник ────────────────────────────────────────────────

        // Контур в плане (X, Z) со смещением вбок от линии; последняя точка замкнутого пути совпадает с первой — без неё.
        private static List<Vector2> ContourOf(SplineWallPath path, float sideOffset)
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
