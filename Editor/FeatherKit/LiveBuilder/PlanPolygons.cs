using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Многоугольники в плане (X, Z → x, y): внутри ли точка, расстояние до контура, треугольники многоугольника,
    /// вычитание выпуклой дыры, куски отрезка внутри многоугольника. Чистая геометрия без Unity-объектов — ею собирают
    /// перекрытия здания (<see cref="SplineBuildingMeshBuilder"/>) и ищут стены здания под мышью (<see cref="LiveBuilderBuildingContext"/>).
    /// </summary>
    public static class PlanPolygons
    {
        // Кусок плиты меньше этой площади после вычитания дыры — уже не кусок, а погрешность, м².
        private const float MinPieceArea = 0.0001f;


        // ── Точка и контур ───────────────────────────────────────────────

        // Внутри ли замкнутого многоугольника — по числу пересечений луча.
        public static bool IsInside(Vector2 point, List<Vector2> polygon)
        {
            var isInside = false;

            for (int index = 0, previous = polygon.Count - 1; index < polygon.Count; previous = index++)
            {
                var a = polygon[index];
                var b = polygon[previous];

                if (a.y > point.y != b.y > point.y && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    isInside = !isInside;
            }

            return isInside;
        }


        // Как далеко точка от ближайшего из замкнутых контуров, м.
        public static float DistanceToContours(Vector2 point, List<List<Vector2>> contours)
        {
            var best = float.MaxValue;

            foreach (var contour in contours)
                for (var index = 0; index < contour.Count; index++)
                    best = Mathf.Min(best, Vector2.Distance(point, ClosestOnSegment(point, contour[index], contour[(index + 1) % contour.Count])));

            return best;
        }


        // Ближайшая к точке точка отрезка.
        public static Vector2 ClosestOnSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            var along = end - start;
            var fraction = along.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - start, along) / along.sqrMagnitude) : 0f;
            return start + along * fraction;
        }


        // Куски отрезка внутри многоугольника: режем отрезок по пересечениям со сторонами, оставляем куски с серединой внутри.
        public static List<(Vector2 From, Vector2 To)> PartsInside(Vector2 start, Vector2 end, List<Vector2> polygon)
        {
            var cuts = new List<float> { 0f, 1f };
            var segment = end - start;

            for (var index = 0; index < polygon.Count; index++)
            {
                var edgeStart = polygon[index];
                var edge = polygon[(index + 1) % polygon.Count] - edgeStart;
                var denominator = Cross(segment, edge);

                if (Mathf.Abs(denominator) < 1e-6f)
                    continue;

                var offset = edgeStart - start;
                var alongSegment = Cross(offset, edge) / denominator;
                var alongEdge = Cross(offset, segment) / denominator;

                if (alongSegment > 0f && alongSegment < 1f && alongEdge >= 0f && alongEdge <= 1f)
                    cuts.Add(alongSegment);
            }

            cuts.Sort();
            var parts = new List<(Vector2 From, Vector2 To)>();

            for (var index = 0; index < cuts.Count - 1; index++)
            {
                if (cuts[index + 1] - cuts[index] < 1e-4f)
                    continue;

                var middle = start + segment * ((cuts[index] + cuts[index + 1]) / 2f);

                if (IsInside(middle, polygon))
                    parts.Add((start + segment * cuts[index], start + segment * cuts[index + 1]));
            }

            return parts;
        }


        // ── Треугольники ─────────────────────────────────────────────────

        // Треугольники многоугольника отрезанием «ушей»: выпуклый угол, внутри которого нет других точек, — треугольник.
        public static List<Vector2[]> Triangulate(List<Vector2> outline)
        {
            var points = new List<Vector2>(outline);
            var triangles = new List<Vector2[]>();

            if (SignedArea(points) < 0f)
                points.Reverse();

            var guard = points.Count * points.Count;

            while (points.Count > 3 && guard-- > 0)
            {
                var earFound = false;

                for (var index = 0; index < points.Count; index++)
                {
                    var previous = points[(index + points.Count - 1) % points.Count];
                    var current = points[index];
                    var next = points[(index + 1) % points.Count];

                    if (!IsEar(points, previous, current, next))
                        continue;

                    triangles.Add(new[] { previous, current, next });
                    points.RemoveAt(index);
                    earFound = true;
                    break;
                }

                if (!earFound)
                    break;
            }

            if (points.Count == 3)
                triangles.Add(points.ToArray());

            return triangles;
        }

        private static bool IsEar(List<Vector2> points, Vector2 previous, Vector2 current, Vector2 next)
        {
            if (Cross(current - previous, next - current) <= 0f)
                return false;

            foreach (var point in points)
            {
                if (point == previous || point == current || point == next)
                    continue;

                if (IsInsideTriangle(point, previous, current, next))
                    return false;
            }

            return true;
        }

        private static bool IsInsideTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            return Cross(b - a, point - a) >= 0f && Cross(c - b, point - b) >= 0f && Cross(a - c, point - c) >= 0f;
        }


        // ── Вычитание дыры ───────────────────────────────────────────────

        // Из каждого выпуклого куска убирает выпуклую дыру: снаружи от каждой стороны дыры — новый кусок, что внутри всех сторон — выброс.
        public static List<List<Vector2>> SubtractConvex(List<List<Vector2>> pieces, Vector2[] hole)
        {
            var holePoints = new List<Vector2>(hole);

            if (SignedArea(holePoints) < 0f)
                holePoints.Reverse();

            var result = new List<List<Vector2>>();

            foreach (var piece in pieces)
            {
                var rest = piece;

                for (var index = 0; index < holePoints.Count && rest.Count >= 3; index++)
                {
                    var edgeStart = holePoints[index];
                    var edgeEnd = holePoints[(index + 1) % holePoints.Count];
                    var outside = ClipByLine(rest, edgeStart, edgeEnd, false);

                    if (outside.Count >= 3 && Mathf.Abs(SignedArea(outside)) > MinPieceArea)
                        result.Add(outside);

                    rest = ClipByLine(rest, edgeStart, edgeEnd, true);
                }
            }

            return result;
        }

        // Часть выпуклого многоугольника слева от линии (внутрь дыры против часовой) или справа.
        private static List<Vector2> ClipByLine(List<Vector2> polygon, Vector2 lineStart, Vector2 lineEnd, bool keepLeft)
        {
            var result = new List<Vector2>();
            var direction = lineEnd - lineStart;

            for (var index = 0; index < polygon.Count; index++)
            {
                var current = polygon[index];
                var next = polygon[(index + 1) % polygon.Count];
                var currentSide = Cross(direction, current - lineStart) * (keepLeft ? 1f : -1f);
                var nextSide = Cross(direction, next - lineStart) * (keepLeft ? 1f : -1f);

                if (currentSide >= 0f)
                    result.Add(current);

                if (currentSide >= 0f != nextSide >= 0f)
                    result.Add(Vector2.Lerp(current, next, currentSide / (currentSide - nextSide)));
            }

            return result;
        }


        // ── Общее ────────────────────────────────────────────────────────

        // Площадь со знаком: плюс — обход против часовой стрелки.
        public static float SignedArea(List<Vector2> polygon)
        {
            var area = 0f;

            for (var index = 0; index < polygon.Count; index++)
            {
                var current = polygon[index];
                var next = polygon[(index + 1) % polygon.Count];
                area += current.x * next.y - next.x * current.y;
            }

            return area / 2f;
        }


        public static float Cross(Vector2 first, Vector2 second)
        {
            return first.x * second.y - first.y * second.x;
        }
    }
}
