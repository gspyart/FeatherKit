using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Держит точки перегородок (<see cref="SplineWall"/> в привязке к зданию) на стенах здания — перед каждой пересборкой, как стены в Revit.
    /// Точка, что лежит на внутренней стороне стены здания, помнит, за что держится (<see cref="SplineWallBuildingAnchor"/>):
    /// - за внутренний угол — двигаешь стены здания, точка встаёт в новый угол
    /// - за стену (отрезок контура) — точка встаёт на ту же стену там, где в неё упирается сама перегородка: стена повернулась — перегородка
    ///   не меняет направления и не ползёт вдоль неё, повернули обратно — точка на прежнем месте
    /// Высота точки — своя. Поменялось число точек контура или двигали саму перегородку — запоминается, где её точки сейчас.
    /// Правки — без Undo, как у других сверок (<see cref="LiveBuilderSyncEdits"/>).
    /// </summary>
    public static class SplineWallBuildingAnchors
    {
        // Точка ближе этого к внутренней стороне стены или углу — лежит на них, м.
        private const float OnWallTolerance = SplineWallBuildingSides.OnWallTolerance;


        // Все перегородки зданий сцены или открытого префаба. Ошибка в одной не останавливает остальные.
        public static void SyncAll()
        {
            foreach (var wall in StageUtility.GetCurrentStageHandle().FindComponentsOfType<SplineWall>())
            {
                try
                {
                    if (wall.TryGetComponent<SplineBuildingStoreyLink>(out var link) && link.IsValid)
                        Sync(wall, link.Building);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, wall);
                }
            }
        }

        // Двигали только здание, и число его точек то же, — точки за ним. Иначе, если что-то поменялось, — запомнить заново.
        private static void Sync(SplineWall wall, SplineBuilding building)
        {
            var container = wall.GetComponent<SplineContainer>();
            var buildingContainer = building.GetComponent<SplineContainer>();
            var ownHash = OwnLineHashOf(wall, container, building);
            var buildingHash = BuildingLineHashOf(building, buildingContainer);
            var buildingKnotCount = KnotCountOf(buildingContainer);

            if (ownHash == wall.SyncedOwnLineHash && buildingHash == wall.SyncedBuildingLineHash)
                return;

            var isOnlyBuildingMoved = ownHash == wall.SyncedOwnLineHash && wall.SyncedBuildingLineHash != 0 && buildingKnotCount == wall.SyncedBuildingKnotCount;

            if (isOnlyBuildingMoved && FollowBuilding(wall, container, building, buildingContainer))
                ownHash = OwnLineHashOf(wall, container, building);

            Remember(wall, container, building, buildingContainer, ownHash, buildingHash, buildingKnotCount);
        }


        // ── За зданием ───────────────────────────────────────────────────

        // Точки на стенах здания — в свой угол или на свою стену там, где в неё упирается сама перегородка. Возвращает, сдвинулась ли хоть одна.
        // Соседние точки берутся такими, какими были до сверки: результат не зависит от того, какая точка сдвинулась первой.
        private static bool FollowBuilding(SplineWall wall, SplineContainer container, SplineBuilding building, SplineContainer buildingContainer)
        {
            var wallToBuilding = building.transform.worldToLocalMatrix * wall.transform.localToWorldMatrix;
            var buildingToWall = wallToBuilding.inverse;
            var positionsBefore = KnotPositionsOf(container);
            var isMoved = false;

            foreach (var anchor in wall.BuildingAnchors)
            {
                if (!IsValid(anchor, container, buildingContainer))
                    continue;

                var spline = container.Splines[anchor.Spline];
                var knot = spline[anchor.Knot];
                var buildingPoint = wallToBuilding.MultiplyPoint3x4(knot.Position);
                var path = SplineWallPath.FromSpline(buildingContainer.Splines[anchor.BuildingSpline], building.CurveStep, building.CornerSegments);

                if (!path.IsUsable)
                    continue;

                var innerOffset = SplineBuildingMeshBuilder.InnerOffsetOf(building, path);
                var buildingSpline = buildingContainer.Splines[anchor.BuildingSpline];
                var neighbour = TryGetNeighbourKnot(positionsBefore[anchor.Spline], anchor.Knot, spline.Closed, out var neighbourPosition)
                    ? wallToBuilding.MultiplyPoint3x4(neighbourPosition)
                    : (Vector3?)null;

                var target = anchor.IsAtCorner
                    ? InnerCornerOf(path, buildingSpline, anchor.BuildingKnot, innerOffset)
                    : InnerPointAlongPartition(path, buildingSpline, anchor.BuildingKnot, innerOffset, buildingPoint, neighbour);

                target = buildingToWall.MultiplyPoint3x4(target);
                target.y = knot.Position.y;

                if (Vector3.Distance(target, knot.Position) < 0.0005f)
                    continue;

                knot.Position = target;
                LiveBuilderSyncEdits.Apply(container, () => spline.SetKnot(anchor.Knot, knot));
                isMoved = true;
            }

            return isMoved;
        }

        private static bool IsValid(SplineWallBuildingAnchor anchor, SplineContainer container, SplineContainer buildingContainer)
        {
            return anchor.Spline < container.Splines.Count && anchor.Knot < container.Splines[anchor.Spline].Count
                   && anchor.BuildingSpline < buildingContainer.Splines.Count && anchor.BuildingKnot < buildingContainer.Splines[anchor.BuildingSpline].Count;
        }

        // Места всех точек перегородки, по сплайнам.
        private static List<Vector3[]> KnotPositionsOf(SplineContainer container)
        {
            var positions = new List<Vector3[]>();

            foreach (var spline in container.Splines)
            {
                var knots = new Vector3[spline.Count];

                for (var index = 0; index < knots.Length; index++)
                    knots[index] = spline[index].Position;

                positions.Add(knots);
            }

            return positions;
        }

        // Соседняя точка перегородки, откуда она идёт к стене: предыдущая, у первой — следующая. Одна точка — соседа нет.
        private static bool TryGetNeighbourKnot(Vector3[] knots, int knot, bool isClosed, out Vector3 neighbour)
        {
            var neighbourIndex = knot > 0 ? knot - 1 : isClosed ? knots.Length - 1 : knot + 1;
            var isFound = knots.Length > 1 && neighbourIndex >= 0 && neighbourIndex < knots.Length && neighbourIndex != knot;
            neighbour = isFound ? knots[neighbourIndex] : default;
            return isFound;
        }


        // ── Запомнить ────────────────────────────────────────────────────

        // У каждой точки на стене здания — за что она держится: у угла — сам угол, иначе — отрезок контура напротив.
        private static void Remember(SplineWall wall, SplineContainer container, SplineBuilding building, SplineContainer buildingContainer,
            int ownHash, int buildingHash, int buildingKnotCount)
        {
            var wallToBuilding = building.transform.worldToLocalMatrix * wall.transform.localToWorldMatrix;
            var buildingPaths = PathsOf(building, buildingContainer);
            var anchors = new List<SplineWallBuildingAnchor>();

            for (var splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
            {
                var spline = container.Splines[splineIndex];

                for (var knotIndex = 0; knotIndex < spline.Count; knotIndex++)
                {
                    var point = wallToBuilding.MultiplyPoint3x4(spline[knotIndex].Position);

                    if (TryFindAnchor(buildingContainer, buildingPaths, point, splineIndex, knotIndex, out var anchor))
                        anchors.Add(anchor);
                }
            }

            LiveBuilderSyncEdits.Apply(wall, () =>
            {
                wall.BuildingAnchors.Clear();
                wall.BuildingAnchors.AddRange(anchors);
                wall.SyncedOwnLineHash = ownHash;
                wall.SyncedBuildingLineHash = buildingHash;
                wall.SyncedBuildingKnotCount = buildingKnotCount;
            });
        }

        // Пути контура здания по сплайнам, с внутренней стороной стен; у сплайна без длины — null.
        private static List<(SplineWallPath Path, float InnerOffset)> PathsOf(SplineBuilding building, SplineContainer buildingContainer)
        {
            var paths = new List<(SplineWallPath Path, float InnerOffset)>();

            foreach (var spline in buildingContainer.Splines)
            {
                var path = SplineWallPath.FromSpline(spline, building.CurveStep, building.CornerSegments);
                paths.Add(path.IsUsable ? (path, SplineBuildingMeshBuilder.InnerOffsetOf(building, path)) : (null, 0f));
            }

            return paths;
        }

        // Сначала углы: точка у внутреннего угла держится за угол. Иначе — за ближайший отрезок контура, если лежит на его внутренней стороне.
        private static bool TryFindAnchor(SplineContainer buildingContainer, List<(SplineWallPath Path, float InnerOffset)> buildingPaths, Vector3 buildingPoint,
            int splineIndex, int knotIndex, out SplineWallBuildingAnchor anchor)
        {
            anchor = default;

            for (var buildingSpline = 0; buildingSpline < buildingContainer.Splines.Count; buildingSpline++)
            {
                var (path, innerOffset) = buildingPaths[buildingSpline];

                if (path == null)
                    continue;

                var spline = buildingContainer.Splines[buildingSpline];

                for (var buildingKnot = 0; buildingKnot < spline.Count; buildingKnot++)
                    if (FlatDistance(InnerCornerOf(path, spline, buildingKnot, innerOffset), buildingPoint) < OnWallTolerance)
                    {
                        anchor = new SplineWallBuildingAnchor(splineIndex, knotIndex, buildingSpline, buildingKnot, true);
                        return true;
                    }
            }

            LiveBuilderSyncEdits.NearestPlace(buildingContainer, buildingPoint, out var nearestSpline, out var place);

            if (nearestSpline >= buildingPaths.Count || buildingPaths[nearestSpline].Path == null)
                return false;

            var nearest = buildingContainer.Splines[nearestSpline];
            var segment = Mathf.Clamp(Mathf.FloorToInt(place), 0, Mathf.Max(0, (nearest.Closed ? nearest.Count : nearest.Count - 1) - 1));
            var (nearestPath, nearestInnerOffset) = buildingPaths[nearestSpline];

            if (FlatDistance(InnerPointOnSegment(nearestPath, nearest, segment, nearestInnerOffset, buildingPoint), buildingPoint) >= OnWallTolerance)
                return false;

            anchor = new SplineWallBuildingAnchor(splineIndex, knotIndex, nearestSpline, segment, false);
            return true;
        }


        // ── Внутренняя сторона стен ──────────────────────────────────────

        // Внутренний угол напротив точки контура, в осях здания: стороны соседних стен сходятся в нём.
        private static Vector3 InnerCornerOf(SplineWallPath path, Spline spline, int knot, float innerOffset)
        {
            return path.PointAt(KnotAlong(path, spline, knot), innerOffset, 0f);
        }

        // Точка внутренней стороны отрезка контура напротив точки point; за концами отрезка — у его конца.
        private static Vector3 InnerPointOnSegment(SplineWallPath path, Spline spline, int segment, float innerOffset, Vector3 point)
        {
            GetSegmentAlong(path, spline, segment, out var startAlong, out var endAlong);
            var along = Mathf.Clamp(path.ClosestDistanceTo(point), startAlong, endAlong);
            return path.PointAt(along, innerOffset, 0f);
        }

        // Где перегородка, продолженная от соседней точки, встречает внутреннюю сторону отрезка: стена здания повернулась —
        // перегородка не меняет направления и не ползёт вдоль неё; повернули обратно — точка ровно на прежнем месте.
        // Соседа нет или перегородка идёт почти вдоль стены — напротив точки, как раньше.
        private static Vector3 InnerPointAlongPartition(SplineWallPath path, Spline spline, int segment, float innerOffset, Vector3 point, Vector3? neighbour)
        {
            if (neighbour is not { } from)
                return InnerPointOnSegment(path, spline, segment, innerOffset, point);

            GetSegmentAlong(path, spline, segment, out var startAlong, out var endAlong);
            var sideStart = path.PointAt(startAlong, innerOffset, 0f);
            var sideEnd = path.PointAt(endAlong, innerOffset, 0f);

            if (!TryIntersectFlat(from, point, sideStart, sideEnd, out var crossing))
                return InnerPointOnSegment(path, spline, segment, innerOffset, point);

            var along = Mathf.Clamp(path.ClosestDistanceTo(crossing), startAlong, endAlong);
            return path.PointAt(along, innerOffset, 0f);
        }

        // Отрезок контура на пути, м от начала: от меньшего к большему.
        private static void GetSegmentAlong(SplineWallPath path, Spline spline, int segment, out float startAlong, out float endAlong)
        {
            var first = KnotAlong(path, spline, segment);
            var second = segment + 1 < spline.Count ? KnotAlong(path, spline, segment + 1) : path.Length;
            startAlong = Mathf.Min(first, second);
            endAlong = Mathf.Max(first, second);
        }

        // Пересечение прямых в плане: через from и to — и через sideStart и sideEnd. Почти параллельны (меньше ~6°) — нет.
        private static bool TryIntersectFlat(Vector3 from, Vector3 to, Vector3 sideStart, Vector3 sideEnd, out Vector3 crossing)
        {
            crossing = default;
            var direction = new Vector2(to.x - from.x, to.z - from.z);
            var side = new Vector2(sideEnd.x - sideStart.x, sideEnd.z - sideStart.z);

            if (direction.sqrMagnitude < 1e-6f || side.sqrMagnitude < 1e-6f)
                return false;

            var denominator = direction.x * side.y - direction.y * side.x;

            if (Mathf.Abs(denominator) < 0.1f * direction.magnitude * side.magnitude)
                return false;

            var offset = new Vector2(sideStart.x - from.x, sideStart.z - from.z);
            var distance = (offset.x * side.y - offset.y * side.x) / denominator;
            crossing = new Vector3(from.x + direction.x * distance, to.y, from.z + direction.y * distance);
            return true;
        }

        // Где на пути стоит точка контура, м от его начала: ближайшее к ней место пути, у скруглённого угла — середина дуги.
        // Так и у первой точки замкнутого контура; первая точка открытой линии — в самом начале.
        private static float KnotAlong(SplineWallPath path, Spline spline, int knot)
        {
            return knot == 0 && !spline.Closed ? 0f : path.ClosestDistanceTo(spline[knot].Position);
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Своя линия и где перегородка относительно здания: сдвинули её целиком — тоже «двигали саму».
        private static int OwnLineHashOf(SplineWall wall, SplineContainer container, SplineBuilding building)
        {
            var hash = new Hash128();
            LiveBuilderStableHash.AppendSplines(ref hash, container);
            LiveBuilderStableHash.AppendQuantizedPlacement(ref hash, building.transform, wall.transform);
            return LiveBuilderStableHash.ToNonZeroInt(hash);
        }

        // Контур здания и всё, от чего зависит внутренняя сторона его стен, — и скругления углов.
        private static int BuildingLineHashOf(SplineBuilding building, SplineContainer buildingContainer)
        {
            var hash = new Hash128();
            LiveBuilderStableHash.AppendSplines(ref hash, buildingContainer);
            hash.Append(building.WallThickness);
            hash.Append((int)building.ContourSide);
            hash.Append(building.CurveStep);
            hash.Append(building.CornerSegments);
            return LiveBuilderStableHash.ToNonZeroInt(hash);
        }

        private static int KnotCountOf(SplineContainer container)
        {
            var count = container.Splines.Count * 10000;

            foreach (var spline in container.Splines)
                count += spline.Count;

            return count;
        }

        private static float FlatDistance(Vector3 first, Vector3 second)
        {
            return Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z));
        }
    }
}
