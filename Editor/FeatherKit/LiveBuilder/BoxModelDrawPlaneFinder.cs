using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Находит плоскость рисования под мышью и точку на ней, уже прилипшую к сетке:
    /// грань объекта из коробок → прочая геометрия сцены → пустота. В пустоте — пол текущего этажа (<see cref="LiveBuilderStoreys"/>).
    /// Здание или стена по линии под мышью — плоскость в осях их объекта из коробок, ровно на их грани: коробка встаёт в него и может их вырезать.
    /// Спрятанное разрезом над этажом пропускается.
    /// Для нового объекта («Новый объект» в окне Live Builder) грани других объектов — просто опора, коробка в них не встаёт; оси — мира.
    /// </summary>
    public static class BoxModelDrawPlaneFinder
    {
        // keepsExactHeight — плоскость ровно на высоте поверхности, без сетки (по сетке — только точка на ней): здание встаёт низом прямо на неё.
        // Без него высота прочей геометрии округляется к сетке — так коробки встают ровно по шагу.
        public static bool TryFind(Ray worldRay, bool isForNewModel, out BoxModelDrawPlane plane, out Vector3 snappedLocalPoint, bool keepsExactHeight = false)
        {
            var hasSurface = BoxModelPicker.TryPickSurface(worldRay, out var surfaceHit);

            if (hasSurface && !isForNewModel)
            {
                var face = surfaceHit.Surface.Face;
                plane = new BoxModelDrawPlane(surfaceHit.Model, surfaceHit.Model.transform.localToWorldMatrix, BoxFaces.Axis(face),
                    MillimeterBox.ToMeters(surfaceHit.Surface.Plane), BoxFaces.IsPositive(face) ? 1f : -1f);
                snappedLocalPoint = plane.SnapOnPlane(surfaceHit.LocalPoint);
                return true;
            }

            var targetModel = isForNewModel ? null : BoxModelActions.SelectedModel();
            var frameToWorld = targetModel != null ? targetModel.transform.localToWorldMatrix : Matrix4x4.identity;

            if (hasSurface)
            {
                var modelTransform = surfaceHit.Model.transform;
                var worldPoint = modelTransform.TransformPoint(surfaceHit.LocalPoint);
                var worldNormal = modelTransform.TransformDirection(BoxFaces.Normal(surfaceHit.Surface.Face));
                plane = PlaneOnSurface(targetModel, frameToWorld, worldPoint, worldNormal, keepsExactHeight, out snappedLocalPoint);
                return true;
            }

            if (BoxModelPicker.TryRaycastVisible(worldRay, float.MaxValue, out var hit))
            {
                // Пол или стена здания, стена по линии — коробка встаёт в их объект: так ей есть что вырезать.
                var hitModel = targetModel == null && !isForNewModel ? OuterModelOfPathGeometry(hit.collider) : targetModel;
                var hitFrame = hitModel != null ? hitModel.transform.localToWorldMatrix : frameToWorld;

                // Грань стены или здания — плоскость ровно на ней: прилипшая к сетке уехала бы с грани, и вырез встал бы рядом со стеной.
                var isOnPathGeometry = LiveBuilderPathOwners.OwnerOf(hit.collider) != null;
                plane = PlaneOnSurface(hitModel, hitFrame, hit.point, hit.normal, keepsExactHeight || isOnPathGeometry, out snappedLocalPoint);
                return true;
            }

            plane = StoreyFloorPlane(targetModel);

            if (plane.TryIntersect(worldRay, out var floorPoint))
            {
                snappedLocalPoint = plane.SnapOnPlane(floorPoint);
                return true;
            }

            snappedLocalPoint = default;
            return false;
        }

        // Объект из коробок, в котором лежит здание или стена по линии этого коллайдера; не они — null.
        private static BoxModel OuterModelOfPathGeometry(Collider collider)
        {
            var owner = LiveBuilderPathOwners.OwnerOf(collider);
            return owner != null ? SplineWallRebuilder.OuterModelOf(owner) : null;
        }

        // Плоскость по точке и нормали поверхности в мире: поперёк ближайшей к нормали оси рамки, на сетке или ровно на поверхности.
        private static BoxModelDrawPlane PlaneOnSurface(BoxModel targetModel, Matrix4x4 frameToWorld, Vector3 worldPoint, Vector3 worldNormal,
            bool keepsExactHeight, out Vector3 snappedLocalPoint)
        {
            var localPoint = frameToWorld.inverse.MultiplyPoint3x4(worldPoint);
            var localNormal = frameToWorld.inverse.MultiplyVector(worldNormal);
            var axis = DominantAxis(localNormal);
            var coordinate = keepsExactHeight ? localPoint[axis] : LiveBuilderGrid.Snap(localPoint[axis], axis);
            var plane = new BoxModelDrawPlane(targetModel, frameToWorld, axis, coordinate, localNormal[axis] >= 0f ? 1f : -1f);
            snappedLocalPoint = plane.SnapOnPlane(localPoint);
            return plane;
        }


        // Пол текущего этажа как плоскость рисования: в осях объекта, а без него — мира.
        public static BoxModelDrawPlane StoreyFloorPlane(BoxModel model)
        {
            var frameToWorld = model != null ? model.transform.localToWorldMatrix : Matrix4x4.identity;
            return new BoxModelDrawPlane(model, frameToWorld, 1, FloorHeightOf(model), 1f);
        }


        // Пол текущего этажа в осях рамки: у объекта — в его осях, без объекта — в осях мира.
        private static float FloorHeightOf(BoxModel model)
        {
            var floorPoint = new Vector3(0f, LiveBuilderStoreys.CurrentFloorHeight, 0f);
            return model != null ? model.transform.InverseTransformPoint(floorPoint).y : floorPoint.y;
        }

        private static int DominantAxis(Vector3 direction)
        {
            var absolute = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
            return absolute.x >= absolute.y && absolute.x >= absolute.z ? 0 : absolute.y >= absolute.z ? 1 : 2;
        }
    }
}
