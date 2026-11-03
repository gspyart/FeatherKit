using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Форма выреза (<see cref="GeometryCutter"/>) точками: контур на плоскости XY выреза — прямоугольник со скруглёнными углами или овал,
    /// и тело — этот контур, протянутый по Z на глубину; где стоит каждая копия вверх. Нужна вычитанию из меша, рамке в окне сцены и поиску пересечений.
    /// </summary>
    public static class GeometryCutterOutline
    {
        // Копия выреза в мире: сам вырез, поднятый на шаг копий столько раз, какая это копия. Копия 0 — сам вырез.
        public static Matrix4x4 CopyToWorld(GeometryCutter cutter, int copy)
        {
            return Matrix4x4.Translate(Vector3.up * (copy * cutter.CopyStep)) * cutter.transform.localToWorldMatrix;
        }



        // Контур в осях выреза, против часовой стрелки, если смотреть с +Z: тогда лицо передней крышки смотрит по +Z.
        public static List<Vector2> OutlineOf(GeometryCutter cutter)
        {
            return cutter.Shape == GeometryCutterShape.Circle
                ? EllipseOutline(cutter.Width, cutter.Height, cutter.CircleSegments)
                : RoundedBoxOutline(cutter.Width, cutter.Height, cutter.TopCornerRadius, cutter.BottomCornerRadius, cutter.CornerSegments);
        }


        // Овал, вписанный в ширину и высоту; низ — на опоре.
        public static List<Vector2> EllipseOutline(float width, float height, int segments)
        {
            var points = new List<Vector2>();
            var radius = new Vector2(width / 2f, height / 2f);

            for (var index = 0; index < segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                points.Add(new Vector2(Mathf.Cos(angle) * radius.x, radius.y + Mathf.Sin(angle) * radius.y));
            }

            return points;
        }


        // Углы по кругу: низ справа, верх справа, верх слева, низ слева; у скруглённого — дуга.
        public static List<Vector2> RoundedBoxOutline(float width, float height, float topRadius, float bottomRadius, int cornerSegments)
        {
            var points = new List<Vector2>();
            var halfWidth = width / 2f;

            AddCorner(points, new Vector2(halfWidth - bottomRadius, bottomRadius), bottomRadius, -90f, cornerSegments);
            AddCorner(points, new Vector2(halfWidth - topRadius, height - topRadius), topRadius, 0f, cornerSegments);
            AddCorner(points, new Vector2(-halfWidth + topRadius, height - topRadius), topRadius, 90f, cornerSegments);
            AddCorner(points, new Vector2(-halfWidth + bottomRadius, bottomRadius), bottomRadius, 180f, cornerSegments);
            return points;
        }

        // Четверть дуги от угла startDegrees; радиус ноль — просто точка угла.
        private static void AddCorner(List<Vector2> points, Vector2 center, float radius, float startDegrees, int segments)
        {
            var steps = radius > 0.001f ? segments : 0;

            for (var step = 0; step <= steps; step++)
            {
                var angle = (startDegrees + 90f * (steps > 0 ? step / (float)steps : 0.5f)) * Mathf.Deg2Rad;
                var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                if (points.Count == 0 || Vector2.Distance(points[^1], point) > 0.0005f)
                    points.Add(point);
            }
        }


        // Тело выреза многоугольниками, лицом наружу, в осях того, что режем. submesh — часть меша для стенок дыры.
        public static List<MeshBooleanPolygon> PrismPolygons(GeometryCutter cutter, Matrix4x4 cutterToTarget, int submesh)
        {
            return MeshSubtraction.PrismPolygons(OutlineOf(cutter), -cutter.Depth / 2f, cutter.Depth / 2f, cutterToTarget, submesh);
        }


        // Восемь углов коробки, в которую вписан вырез, в осях выреза: опора — низ посередине.
        public static Vector3[] BoxCorners(GeometryCutter cutter)
        {
            var halfWidth = cutter.Width / 2f;
            var halfDepth = cutter.Depth / 2f;
            var corners = new Vector3[8];

            for (var index = 0; index < 8; index++)
                corners[index] = new Vector3((index & 1) == 0 ? -halfWidth : halfWidth, (index & 2) == 0 ? 0f : cutter.Height, (index & 4) == 0 ? -halfDepth : halfDepth);

            return corners;
        }


        // Габарит выреза в осях другого объекта.
        public static Bounds BoundsIn(GeometryCutter cutter, Matrix4x4 cutterToTarget)
        {
            var corners = BoxCorners(cutter);
            var bounds = new Bounds(cutterToTarget.MultiplyPoint3x4(corners[0]), Vector3.zero);

            for (var index = 1; index < corners.Length; index++)
                bounds.Encapsulate(cutterToTarget.MultiplyPoint3x4(corners[index]));

            return bounds;
        }
    }
}
