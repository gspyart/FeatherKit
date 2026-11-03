using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Прямоугольник наружной поверхности объекта из коробок: на какой грани, в какой плоскости, где и от какой коробки.
    /// Area — в миллиметрах по осям плоскости грани (<see cref="BoxFaces.SurfaceAxisU"/>, <see cref="BoxFaces.SurfaceAxisV"/>).
    /// </summary>
    public sealed class BoxModelSurface
    {
        public BoxModelSurface(int face, int plane, RectInt area, int sourcePart)
        {
            Face = face;
            Plane = plane;
            Area = area;
            SourcePart = sourcePart;
        }

        public int Face { get; }
        public int Plane { get; }
        public RectInt Area { get; }
        public int SourcePart { get; }
    }


    /// <summary>
    /// Находит наружные поверхности объекта из коробок: часть грани, к которой вплотную прижат другой кусок, внутри и не рисуется —
    /// на стыках нет лишних граней и мерцания. Соседние прямоугольники одной плоскости от одной коробки сливаются в один.
    /// </summary>
    public static class BoxModelSurfaceFinder
    {
        public static List<BoxModelSurface> FindOuterSurfaces(IReadOnlyList<BoxModelCell> cells)
        {
            var facesByPlane = new Dictionary<(int face, int plane), List<RectInt>>();

            foreach (var cell in cells)
                for (var face = 0; face < BoxFaces.Count; face++)
                {
                    var key = (face, PlaneOf(cell.Box, face));

                    if (!facesByPlane.TryGetValue(key, out var areas))
                        facesByPlane[key] = areas = new List<RectInt>();

                    areas.Add(AreaOf(cell.Box, face));
                }

            var surfaces = new List<BoxModelSurface>();

            foreach (var cell in cells)
                for (var face = 0; face < BoxFaces.Count; face++)
                {
                    var plane = PlaneOf(cell.Box, face);
                    var visible = new List<RectInt> { AreaOf(cell.Box, face) };

                    if (facesByPlane.TryGetValue((BoxFaces.Opposite(face), plane), out var covering))
                        foreach (var cover in covering)
                            visible = Subtract(visible, cover);

                    foreach (var area in visible)
                        surfaces.Add(new BoxModelSurface(face, plane, area, cell.FaceSources[face]));
                }

            return MergeNeighbours(surfaces);
        }


        // ── Грань куска ──────────────────────────────────────────────────

        private static int PlaneOf(MillimeterBox box, int face)
        {
            var axis = BoxFaces.Axis(face);
            return BoxFaces.IsPositive(face) ? box.Max[axis] : box.Min[axis];
        }


        private static RectInt AreaOf(MillimeterBox box, int face)
        {
            var axisU = BoxFaces.SurfaceAxisU(face);
            var axisV = BoxFaces.SurfaceAxisV(face);
            return new RectInt(box.Min[axisU], box.Min[axisV], box.Max[axisU] - box.Min[axisU], box.Max[axisV] - box.Min[axisV]);
        }


        // ── Вычитание прямоугольников ────────────────────────────────────

        // Из каждого прямоугольника убирает cover: остаётся до четырёх полос вокруг него.
        private static List<RectInt> Subtract(List<RectInt> areas, RectInt cover)
        {
            var result = new List<RectInt>(areas.Count);

            foreach (var area in areas)
            {
                if (!Overlaps(area, cover))
                {
                    result.Add(area);
                    continue;
                }

                var middleMinX = Mathf.Max(area.xMin, cover.xMin);
                var middleMaxX = Mathf.Min(area.xMax, cover.xMax);

                if (area.xMin < cover.xMin)
                    result.Add(new RectInt(area.xMin, area.yMin, cover.xMin - area.xMin, area.height));

                if (area.xMax > cover.xMax)
                    result.Add(new RectInt(cover.xMax, area.yMin, area.xMax - cover.xMax, area.height));

                if (area.yMin < cover.yMin)
                    result.Add(new RectInt(middleMinX, area.yMin, middleMaxX - middleMinX, cover.yMin - area.yMin));

                if (area.yMax > cover.yMax)
                    result.Add(new RectInt(middleMinX, cover.yMax, middleMaxX - middleMinX, area.yMax - cover.yMax));
            }

            return result;
        }


        private static bool Overlaps(RectInt a, RectInt b)
        {
            return a.xMin < b.xMax && b.xMin < a.xMax && a.yMin < b.yMax && b.yMin < a.yMax;
        }


        // ── Слияние ──────────────────────────────────────────────────────

        // Сливает соседние прямоугольники одной плоскости от одной коробки, пока есть что сливать: меньше граней — легче меш.
        private static List<BoxModelSurface> MergeNeighbours(List<BoxModelSurface> surfaces)
        {
            var groups = new Dictionary<(int face, int plane, int source), List<RectInt>>();

            foreach (var surface in surfaces)
            {
                var key = (surface.Face, surface.Plane, surface.SourcePart);

                if (!groups.TryGetValue(key, out var areas))
                    groups[key] = areas = new List<RectInt>();

                areas.Add(surface.Area);
            }

            var merged = new List<BoxModelSurface>(surfaces.Count);

            foreach (var group in groups)
            {
                var areas = group.Value;

                while (TryMergeAnyPair(areas))
                {
                }

                foreach (var area in areas)
                    merged.Add(new BoxModelSurface(group.Key.face, group.Key.plane, area, group.Key.source));
            }

            return merged;
        }


        private static bool TryMergeAnyPair(List<RectInt> areas)
        {
            for (var first = 0; first < areas.Count; first++)
                for (var second = first + 1; second < areas.Count; second++)
                {
                    if (!TryMerge(areas[first], areas[second], out var union))
                        continue;

                    areas[first] = union;
                    areas.RemoveAt(second);
                    return true;
                }

            return false;
        }


        // Сливаются, если стоят вплотную и совпадают по всей длине общей стороны.
        private static bool TryMerge(RectInt a, RectInt b, out RectInt union)
        {
            var sameColumn = a.xMin == b.xMin && a.xMax == b.xMax && (a.yMax == b.yMin || b.yMax == a.yMin);
            var sameRow = a.yMin == b.yMin && a.yMax == b.yMax && (a.xMax == b.xMin || b.xMax == a.xMin);

            if (!sameColumn && !sameRow)
            {
                union = default;
                return false;
            }

            var minX = Mathf.Min(a.xMin, b.xMin);
            var minY = Mathf.Min(a.yMin, b.yMin);
            union = new RectInt(minX, minY, Mathf.Max(a.xMax, b.xMax) - minX, Mathf.Max(a.yMax, b.yMax) - minY);
            return true;
        }
    }
}
