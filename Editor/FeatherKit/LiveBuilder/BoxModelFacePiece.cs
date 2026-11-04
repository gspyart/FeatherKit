using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Видимый кусок грани коробки: прямоугольник поверхности, который остался от грани после соседних коробок и вырезов.
    /// Всё в осях объекта, м. OutwardFace — куда смотрит поверхность, в воздух: у стенки ниши — внутрь вырезающей коробки.
    /// Сам по себе не двигается — от него растёт новая коробка (<see cref="BoxModelFacePiecePushPull"/>).
    /// </summary>
    public sealed class BoxModelFacePiece
    {
        public BoxModelFacePiece(BoxModelSurface surface)
        {
            OutwardFace = surface.Face;
            Plane = MillimeterBox.ToMeters(surface.Plane);
            var area = surface.Area;
            Area = Rect.MinMaxRect(MillimeterBox.ToMeters(area.xMin), MillimeterBox.ToMeters(area.yMin),
                MillimeterBox.ToMeters(area.xMax), MillimeterBox.ToMeters(area.yMax));
        }


        public int OutwardFace { get; }

        // Где плоскость куска по оси грани, м.
        public float Plane { get; }

        // Прямоугольник по осям плоскости грани (<see cref="BoxFaces.SurfaceAxisU"/>, <see cref="BoxFaces.SurfaceAxisV"/>), м.
        public Rect Area { get; }

        public int Axis => BoxFaces.Axis(OutwardFace);

        // +1 — наружу по оси, −1 — против оси.
        public float OutwardSign => BoxFaces.IsPositive(OutwardFace) ? 1f : -1f;

        public Vector3 Center => PointOnPlane(Area.center.x, Area.center.y);


        // Четыре угла по кругу, в осях объекта.
        public Vector3[] Corners()
        {
            return new[]
            {
                PointOnPlane(Area.xMin, Area.yMin), PointOnPlane(Area.xMax, Area.yMin),
                PointOnPlane(Area.xMax, Area.yMax), PointOnPlane(Area.xMin, Area.yMax)
            };
        }


        // Коробка куска: от плоскости на thickness в сторону direction (+1 — наружу, −1 — внутрь), углы в осях объекта.
        public void BoxFromPlane(float thickness, float direction, out Vector3 min, out Vector3 max)
        {
            var near = PointOnPlane(Area.xMin, Area.yMin);
            var far = PointOnPlane(Area.xMax, Area.yMax);
            far[Axis] = Plane + thickness * direction * OutwardSign;
            min = Vector3.Min(near, far);
            max = Vector3.Max(near, far);
        }


        public bool IsSameAs(BoxModelFacePiece other)
        {
            return other != null && OutwardFace == other.OutwardFace && Mathf.Approximately(Plane, other.Plane) && Area == other.Area;
        }


        private Vector3 PointOnPlane(float u, float v)
        {
            var point = Vector3.zero;
            point[Axis] = Plane;
            point[BoxFaces.SurfaceAxisU(OutwardFace)] = u;
            point[BoxFaces.SurfaceAxisV(OutwardFace)] = v;
            return point;
        }
    }
}
