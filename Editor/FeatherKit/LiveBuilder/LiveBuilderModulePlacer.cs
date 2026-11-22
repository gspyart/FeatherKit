using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Где встанет модуль (<see cref="LiveBuilderModule"/>) кликом инструмента «Module»: стена здания или стена по линии под мышью.
    /// - по нормали, +Z наружу, по толщине — середина стены; низ — пол этажа под мышью плюс высота модуля
    /// - вдоль стены край модуля — по сетке от начала стороны между углами
    /// Клик ставит модуль, Shift + клик — ряд вдоль стены (<see cref="LiveBuilderModuleFactory"/>). Стены объекта из коробок — не цель.
    /// </summary>
    public sealed class LiveBuilderModulePlacer
    {
        // Нормаль круче этого — пол или потолок, а не стена.
        private const float MaxWallNormalY = 0.7f;

        private Component targetPathOwner;
        private Vector3 worldPosition;
        private Quaternion worldRotation;
        private Bounds moduleBounds;


        public bool HasTarget => targetPathOwner != null;

        public string Hint => HasTarget
            ? "Click — place the module " + LiveBuilderHintKeys.Shift + " + click — a row along this side"
            : "Point at a wall of a building or a spline wall";


        // ── Под мышью ────────────────────────────────────────────────────

        public void UpdateHover(LiveBuilderSceneHit hit, GameObject prefab)
        {
            targetPathOwner = null;

            if (prefab == null || !hit.IsPathOwnerCloser || !hit.HasVisibleHit || Mathf.Abs(hit.VisibleHit.normal.y) > MaxWallNormalY)
                return;

            AimAtPathWall(hit.PathOwner, hit.VisibleHit, prefab);
        }

        private void AimAtPathWall(Component wall, RaycastHit surfaceHit, GameObject prefab)
        {
            var owner = wall.transform;
            var localPoint = owner.InverseTransformPoint(surfaceHit.point);
            var path = LiveBuilderArrayWallPath.NearestPathOf(wall, localPoint);

            if (path == null)
                return;

            var along = path.ClosestDistanceTo(localPoint);
            var localRotation = OutwardRotation(owner, path, along, surfaceHit.normal);
            moduleBounds = LiveBuilderModuleFactory.BoundsOf(prefab);
            along = SnappedAlong(path, along, localRotation);

            LiveBuilderPathOwners.GetWallSides(wall, path, out var leftOffset, out var rightOffset);
            var localPosition = path.PointAt(along, (leftOffset + rightOffset) / 2f, 0f);
            localPosition.y = FloorUnder(wall, path, along, localPoint.y) + BottomAboveFloorOf(prefab);

            targetPathOwner = wall;
            worldPosition = owner.TransformPoint(localPosition);
            worldRotation = owner.rotation * localRotation;
        }

        // +Z — от стены к той стороне, на которую смотрит мышь.
        private static Quaternion OutwardRotation(Transform owner, SplineWallPath path, float along, Vector3 worldHitNormal)
        {
            var normal = path.SideNormalAt(along);
            normal.y = 0f;

            if (Vector3.Dot(owner.TransformDirection(normal), worldHitNormal) < 0f)
                normal = -normal;

            return Quaternion.LookRotation(normal.normalized, Vector3.up);
        }

        // Опора модуля так, чтобы его край, ближний к началу стороны, лёг на сетку от этого начала; за углы стороны модуль не выходит.
        private float SnappedAlong(SplineWallPath path, float along, Quaternion localRotation)
        {
            LiveBuilderArrayWallPath.FindSide(path, LiveBuilderArrayWallPath.CornersOf(path), along, out var sideStart, out var sideEnd);
            var direction = path.DirectionOf(path.SegmentAt(along, out _));
            var isRightAlongLine = Vector3.Dot(localRotation * Vector3.right, direction) >= 0f;
            var back = isRightAlongLine ? moduleBounds.min.x : -moduleBounds.max.x;
            var width = moduleBounds.size.x;
            var step = LiveBuilderGrid.StepOn(0);

            var edge = along + back;
            edge = sideStart + Mathf.Round((edge - sideStart) / step) * step;
            edge = Mathf.Clamp(edge, sideStart, Mathf.Max(sideStart, sideEnd - width));
            return edge - back;
        }

        // Пол этажа под мышью в осях стены: у здания — этажа, на высоте которого мышь; у стены по линии — её низ.
        private static float FloorUnder(Component wall, SplineWallPath path, float along, float localHeight)
        {
            if (wall is not SplineBuilding building)
                return path.BaseHeightAt(along);

            var firstFloor = path.BaseHeightAt(0f) + building.FloorTopHeight;
            var storey = Mathf.Clamp(Mathf.FloorToInt((localHeight - firstFloor) / building.StoreyHeight + 0.001f), 0, building.StoreyCount - 1);
            return firstFloor + storey * building.StoreyHeight;
        }

        private static float BottomAboveFloorOf(GameObject prefab)
        {
            return prefab.TryGetComponent<LiveBuilderModule>(out var module) ? module.BottomAboveFloor : 0f;
        }


        // ── Клик ─────────────────────────────────────────────────────────

        // Модуль или ряд — в этаж здания под ним или в стену по линии; выделяется. Цели нет — null.
        public GameObject Commit(GameObject prefab, bool isRow)
        {
            if (!HasTarget || prefab == null)
                return null;

            var parent = ParentFor(targetPathOwner, worldPosition);
            var created = isRow
                ? LiveBuilderModuleFactory.CreateRow(prefab, parent, worldPosition, worldRotation)
                : LiveBuilderModuleFactory.CreateModule(prefab, parent, worldPosition, worldRotation);

            Selection.activeGameObject = created;
            return created;
        }

        private static Transform ParentFor(Component wall, Vector3 position)
        {
            if (wall is SplineBuilding building && SplineBuildingStoreys.HasStoreyContainers(building))
                return SplineBuildingStoreys.ContainerAtHeight(building, position.y).transform;

            return wall.transform;
        }


        // ── Рамка ────────────────────────────────────────────────────────

        public void DrawPreview()
        {
            if (!HasTarget)
                return;

            using (new Handles.DrawingScope(GeometryCutterGizmos.CutterColor, Matrix4x4.TRS(worldPosition, worldRotation, Vector3.one)))
            {
                Handles.DrawWireCube(moduleBounds.center, moduleBounds.size);
                Handles.DrawLine(Vector3.zero, Vector3.forward * 0.5f);
            }
        }
    }
}
