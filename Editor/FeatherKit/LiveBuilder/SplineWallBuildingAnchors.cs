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
    /// - за стену (отрезок контура) — точка встаёт на ту же стену напротив себя: вдоль стены не едет, только за ней поперёк
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

        // Точки на стенах здания — в свой угол или на свою стену напротив себя. Возвращает, сдвинулась ли хоть одна.
        private static bool FollowBuilding(SplineWall wall, SplineContainer container, SplineBuilding building, SplineContainer buildingContainer)
        {
            var wallToBuilding = building.transform.worldToLocalMatrix * wall.transform.localToWorldMatrix;
            var buildingToWall = wallToBuilding.inverse;
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
                var target = anchor.IsAtCorner
                    ? InnerCornerOf(path, buildingSpline, anchor.BuildingKnot, innerOffset)
                    : InnerPointOnSegment(path, buildingSpline, anchor.BuildingKnot, innerOffset, buildingPoint);

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
            var startAlong = KnotAlong(path, spline, segment);
            var endAlong = segment + 1 < spline.Count ? KnotAlong(path, spline, segment + 1) : path.Length;
            var along = Mathf.Clamp(path.ClosestDistanceTo(point), Mathf.Min(startAlong, endAlong), Mathf.Max(startAlong, endAlong));
            return path.PointAt(along, innerOffset, 0f);
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
