using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Плоскость, на которой рисуется прямоугольник новой коробки: в чей объект пойдёт коробка, в каких осях,
    /// какая ось смотрит из плоскости наружу. Точки — в осях рамки: у объекта из коробок — в его осях, без объекта — в осях мира.
    /// </summary>
    public readonly struct BoxModelDrawPlane
    {
        public BoxModelDrawPlane(BoxModel model, Matrix4x4 frameToWorld, int axis, float coordinate, float outwardSign)
        {
            Model = model;
            FrameToWorld = frameToWorld;
            Axis = axis;
            Coordinate = coordinate;
            OutwardSign = outwardSign;
        }

        // Объект, в который пойдёт коробка; null — будет новый.
        public BoxModel Model { get; }
        public Matrix4x4 FrameToWorld { get; }

        // Ось рамки, поперёк которой лежит плоскость: 0 — X, 1 — Y, 2 — Z.
        public int Axis { get; }
        public float Coordinate { get; }

        // +1 — наружу смотрит плюс по оси, −1 — минус.
        public float OutwardSign { get; }

        public Vector3 OutwardDirection
        {
            get
            {
                var direction = Vector3.zero;
                direction[Axis] = OutwardSign;
                return direction;
            }
        }


        // Точка на плоскости, обе оси плоскости — на сетке.
        public Vector3 SnapOnPlane(Vector3 localPoint)
        {
            var snapped = LiveBuilderGrid.Snap(localPoint);
            snapped[Axis] = Coordinate;
            return snapped;
        }


        // Где луч мыши пересекает плоскость, в осях рамки.
        public bool TryIntersect(Ray worldRay, out Vector3 localPoint)
        {
            var worldToFrame = FrameToWorld.inverse;
            var origin = worldToFrame.MultiplyPoint3x4(worldRay.origin);
            var direction = worldToFrame.MultiplyVector(worldRay.direction);
            localPoint = default;

            if (Mathf.Abs(direction[Axis]) < 1e-5f)
                return false;

            var along = (Coordinate - origin[Axis]) / direction[Axis];

            if (along <= 0f)
                return false;

            localPoint = origin + direction * along;
            return true;
        }
    }
}
