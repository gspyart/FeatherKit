using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Ход повтора вдоль линии стены или контура здания (<see cref="LiveBuilderArray"/>, Along = Wall): копия X — точка и поворот линии на расстоянии от оригинала.
    /// - Y — вверх, Z — поперёк, в осях повтора, повёрнутых вместе с линией
    /// - сторона между углами — для Fill «вся сторона»; копия, которая шириной накрывает угол или уходит за конец линии, не ставится
    /// - углы, сторона и ближайший путь — и для модуля на стене (<see cref="LiveBuilderModulePlacer"/>)
    /// </summary>
    public sealed class LiveBuilderArrayWallPath
    {
        // Излом больше этого — угол: копии через него не ставятся. Меньше — изгиб кривой стены, копии поворачиваются по нему, градусы.
        public const float CornerTurnDegrees = 30f;

        // Ближе этого к углу или концу линии — уже на нём, м.
        private const float EdgeTolerance = 0.001f;

        // Поворот меньше этого — копия не повёрнута, градусы.
        private const float TurnToleranceDegrees = 0.01f;

        private readonly Transform owner;
        private readonly SplineWallPath path;
        private readonly List<float> corners;
        private readonly Vector3 originalLinePoint;
        private readonly Vector3 originalDirection;


        private LiveBuilderArrayWallPath(LiveBuilderArray array, Component pathOwner, SplineWallPath path)
        {
            owner = pathOwner.transform;
            this.path = path;
            PathOwner = pathOwner;

            var localPivot = owner.InverseTransformPoint(array.transform.position);
            OriginalDistance = path.ClosestDistanceTo(localPivot);
            originalLinePoint = path.PointAt(OriginalDistance, 0f, 0f);
            originalDirection = DirectionAt(OriginalDistance);
            corners = CornersOf(path);

            var copyCorners = LiveBuilderArrayCopyBounds.CornersOf(array);
            CopyExtent = LiveBuilderArrayCopyBounds.ExtentAlong(array, copyCorners, owner.TransformDirection(originalDirection).normalized);
            FindSide(path, corners, OriginalDistance, out var sideStart, out var sideEnd);
            SideStart = sideStart;
            SideLength = sideEnd - sideStart;
        }


        // Стена (SplineWall) или здание (SplineBuilding), по линии которых идёт повтор.
        public Component PathOwner { get; }

        // Где оригинал на линии, м от её начала.
        public float OriginalDistance { get; }

        // Копия от опоры вдоль линии: x — назад, y — вперёд, м.
        public Vector2 CopyExtent { get; }

        public float CopyWidth => CopyExtent.y - CopyExtent.x;

        // Сторона между углами, на которой стоит оригинал: начало (у замкнутой может быть меньше нуля — через начало линии) и длина, м.
        public float SideStart { get; }
        public float SideLength { get; }


        // ── Откуда ───────────────────────────────────────────────────────

        // Линия повтора: ближайшие над ним стена или здание, ближайший к нему их путь. Ни стены, ни здания — null: повтор идёт по осям.
        public static LiveBuilderArrayWallPath Of(LiveBuilderArray array)
        {
            var pathOwner = LiveBuilderPathOwners.OwnerAbove(array);

            if (pathOwner == null)
                return null;

            var nearest = NearestPathOf(pathOwner, pathOwner.transform.InverseTransformPoint(array.transform.position));
            return nearest != null ? new LiveBuilderArrayWallPath(array, pathOwner, nearest) : null;
        }


        // Новый ряд Fill «во всю сторону» — сразу на первое место стороны: оригинал — первая копия, а не спрятанный в стороне.
        public static void MoveToFirstPlace(LiveBuilderArray array)
        {
            var wallPath = Of(array);

            if (wallPath == null)
                return;

            var shift = wallPath.FirstCopyShiftFor(array.Margin);

            if (shift != 0f && wallPath.TryGetCopy(array, new Vector3(shift, 0f, 0f), LiveBuilderArrayPattern.AllVariants, out var copy) && !copy.IsTurned)
                array.transform.position += copy.Shift;
        }


        // Путь стены или здания, ближайший к точке в их осях; путей нет — null.
        public static SplineWallPath NearestPathOf(Component pathOwner, Vector3 localPoint)
        {
            SplineWallPath nearest = null;
            var nearestGap = float.MaxValue;

            foreach (var path in LiveBuilderPathOwners.PathsOf(pathOwner))
            {
                path.ClosestDistanceTo(localPoint, out var gap);

                if (gap < nearestGap)
                {
                    nearestGap = gap;
                    nearest = path;
                }
            }

            return nearest;
        }


        // ── Копии ────────────────────────────────────────────────────────

        // Где первая копия Fill «во всю сторону» от оригинала, м по линии: её край — на отступе от начала стороны.
        public float FirstCopyShiftFor(float margin)
        {
            var shift = SideStart + margin - CopyExtent.x - OriginalDistance;
            return Mathf.Abs(shift) < EdgeTolerance ? 0f : shift;
        }


        // Копия со сдвигом от оригинала в осях повтора (X — по линии). Нет — копия на углу или за концом линии.
        // Не повёрнутая — чистый сдвиг, без матриц: копия на месте оригинала — ровно ноль.
        public bool TryGetCopy(LiveBuilderArray array, Vector3 offset, int variant, out LiveBuilderArrayCopy copy)
        {
            copy = LiveBuilderArrayCopy.Shifted(Vector3.zero, variant);

            if (offset == Vector3.zero)
                return !CoversCorner(OriginalDistance);

            if (!TryGetDistance(OriginalDistance + offset.x, out var distance))
                return false;

            var acrossAndUp = Quaternion.Inverse(owner.rotation) * array.transform.rotation * new Vector3(0f, offset.y, offset.z);

            if (offset.x == 0f)
            {
                copy = LiveBuilderArrayCopy.Shifted(owner.TransformVector(acrossAndUp), variant);
                return true;
            }

            var turn = Vector3.SignedAngle(originalDirection, DirectionAt(distance), Vector3.up);
            var linePoint = path.PointAt(distance, 0f, 0f);

            if (Mathf.Abs(turn) <= TurnToleranceDegrees)
            {
                copy = LiveBuilderArrayCopy.Shifted(owner.TransformVector(linePoint + acrossAndUp - originalLinePoint), variant);
                return true;
            }

            var localMove = Matrix4x4.Translate(linePoint) * Matrix4x4.Rotate(Quaternion.AngleAxis(turn, Vector3.up))
                            * Matrix4x4.Translate(acrossAndUp - originalLinePoint);
            copy = LiveBuilderArrayCopy.Turned(owner.localToWorldMatrix * localMove * owner.worldToLocalMatrix, variant);
            return true;
        }

        // Место на линии: у замкнутой — по кругу; на углу или за концом открытой — нет.
        private bool TryGetDistance(float rawDistance, out float distance)
        {
            distance = rawDistance;

            if (path.IsClosed)
                distance = Mathf.Repeat(rawDistance, path.Length);
            else if (rawDistance < -EdgeTolerance || rawDistance > path.Length + EdgeTolerance)
                return false;

            if (!path.IsClosed && (distance + CopyExtent.x < -EdgeTolerance || distance + CopyExtent.y > path.Length + EdgeTolerance))
                return false;

            return !CoversCorner(distance);
        }

        // Угол строго внутри копии — шириной она его накрывает.
        private bool CoversCorner(float distance)
        {
            var from = distance + CopyExtent.x + EdgeTolerance;
            var to = distance + CopyExtent.y - EdgeTolerance;

            foreach (var corner in corners)
            {
                if (IsBetween(corner, from, to))
                    return true;

                if (path.IsClosed && (IsBetween(corner + path.Length, from, to) || IsBetween(corner - path.Length, from, to)))
                    return true;
            }

            return false;
        }

        private static bool IsBetween(float value, float from, float to)
        {
            return value > from && value < to;
        }


        // ── Линия ────────────────────────────────────────────────────────

        // Вдоль линии на расстоянии: направление отрезка, на котором оно лежит.
        private Vector3 DirectionAt(float distance)
        {
            return path.DirectionOf(path.SegmentAt(distance, out _));
        }

        // Расстояния до точек, где линия ломается больше чем на CornerTurnDegrees. У замкнутой начало — тоже точка с изломом.
        public static List<float> CornersOf(SplineWallPath path)
        {
            var corners = new List<float>();
            var last = path.Points.Count - 1;

            for (var index = 0; index <= last; index++)
            {
                var isInside = index > 0 && index < last;

                if (!isInside && !(path.IsClosed && index == 0))
                    continue;

                var incoming = path.DirectionOf(index > 0 ? index - 1 : path.SegmentCount - 1);
                var outgoing = path.DirectionOf(index < last ? index : 0);

                if (Vector3.Angle(incoming, outgoing) > CornerTurnDegrees)
                    corners.Add(path.Distances[index]);
            }

            return corners;
        }

        // Сторона между ближайшими углами вокруг места на линии; углов нет — вся линия.
        public static void FindSide(SplineWallPath path, List<float> corners, float distance, out float sideStart, out float sideEnd)
        {
            sideStart = 0f;
            sideEnd = path.Length;

            if (corners.Count == 0)
                return;

            var before = float.MinValue;
            var after = float.MaxValue;

            foreach (var corner in corners)
            {
                if (corner <= distance + EdgeTolerance)
                    before = Mathf.Max(before, corner);

                if (corner > distance + EdgeTolerance)
                    after = Mathf.Min(after, corner);
            }

            if (!path.IsClosed)
            {
                sideStart = before > float.MinValue ? before : 0f;
                sideEnd = after < float.MaxValue ? after : path.Length;
                return;
            }

            // Замкнутая: до первого угла — это хвост стороны от последнего, через начало линии.
            sideStart = before > float.MinValue ? before : corners[^1] - path.Length;
            sideEnd = after < float.MaxValue ? after : corners[0] + path.Length;
        }
    }
}
