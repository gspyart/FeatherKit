using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Прилипание точки к внутренней стороне стен здания по контуру (<see cref="SplineBuilding"/>): перегородка доходит до стены здания без щели,
    /// у угла — ровно в угол. Только точка в плане здания; где рисуют и на каком этаже — <see cref="LiveBuilderBuildingContext"/>.
    /// </summary>
    public static class SplineBuildingWallSnapping
    {
        // Насколько точка может не дойти до стены здания и всё равно к ней прилипнуть — доля шага сетки.
        private const float ContourSnapFraction = 0.6f;


        // Точка в осях здания, прилипшая к внутренней стороне его стен, если до неё меньше чем полшага сетки.
        // Рядом сразу две стены здания (угол) — прилипает к обеим, ровно в угол.
        public static Vector3 SnapToWalls(SplineBuilding building, Vector3 localPoint)
        {
            var maxDistance = LiveBuilderGrid.CurrentStep * ContourSnapFraction;
            var point = new Vector2(localPoint.x, localPoint.z);
            var nearEdges = new List<(Vector2 Start, Vector2 End, float Distance)>();

            foreach (var contour in SplineBuildingMeshBuilder.InnerContoursOf(building))
                for (var index = 0; index < contour.Count; index++)
                {
                    var start = contour[index];
                    var end = contour[(index + 1) % contour.Count];
                    var distance = Vector2.Distance(point, PlanPolygons.ClosestOnSegment(point, start, end));

                    if (distance < maxDistance)
                        nearEdges.Add((start, end, distance));
                }

            if (nearEdges.Count == 0)
                return localPoint;

            nearEdges.Sort((first, second) => first.Distance.CompareTo(second.Distance));
            var nearest = nearEdges[0];
            var snapped = PlanPolygons.ClosestOnSegment(point, nearest.Start, nearest.End);

            foreach (var other in nearEdges)
                if (TryIntersectLines(nearest.Start, nearest.End, other.Start, other.End, out var corner) && Vector2.Distance(point, corner) < maxDistance * 1.5f)
                {
                    snapped = corner;
                    break;
                }

            return new Vector3(snapped.x, localPoint.y, snapped.y);
        }

        // Пересечение двух прямых; почти параллельные — не пересекаются.
        private static bool TryIntersectLines(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd, out Vector2 intersection)
        {
            var first = firstEnd - firstStart;
            var second = secondEnd - secondStart;
            var cross = first.x * second.y - first.y * second.x;
            intersection = default;

            if (Mathf.Abs(cross) < 0.01f * first.magnitude * second.magnitude)
                return false;

            var offset = secondStart - firstStart;
            var along = (offset.x * second.y - offset.y * second.x) / cross;
            intersection = firstStart + first * along;
            return true;
        }
    }
}
