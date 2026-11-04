using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Голубая ручка скругления у выбранной угловой точки контура, как в tsitski (<see cref="LiveBuilderCornerRadii"/>):
    /// стоит на биссектрисе угла внутри него, тянешь от точки — радиус больше, к точке — меньше, до острого угла. Радиус — по 5 см, рядом подпись.
    /// Только для угла (не плавной и не Безье точки) между двумя отрезками. Зовёт правка контура (<see cref="LiveBuilderContourHandles"/>).
    /// </summary>
    public static class LiveBuilderCornerRadiusHandle
    {
        // Шаг радиуса, м.
        private const float RadiusStep = 0.05f;

        // Ручка при остром угле — на столько от точки, доля размера ручки Unity: не лежит на самой точке.
        private const float RestOffset = 0.45f;
        private const float HandleSize = 0.05f;

        private static readonly Color HandleColor = new(0.3f, 0.85f, 1f);
        private static readonly int RadiusControlHint = "LiveBuilderContourRadius".GetHashCode();


        // Можно ли скруглить точку: угол, и с обеих сторон есть отрезки.
        public static bool CanRound(Spline spline, int knot)
        {
            var isInner = spline.Closed || knot > 0 && knot < spline.Count - 1;
            return isInner && spline.Count >= 3 && LiveBuilderContourSelection.ShapeOf(spline.GetTangentMode(knot)) == LiveBuilderKnotShape.Corner;
        }


        // Ручка одной точки. control — для правки контура: нажатие по ней не начинает рамку выбора. Возвращает, поменялся ли радиус.
        public static bool Draw(Component owner, SplineContainer container, Spline spline, int knot, out int control)
        {
            control = GUIUtility.GetControlID(RadiusControlHint, FocusType.Passive);

            if (!TryGetCorner(owner, spline, knot, out var corner, out var inward, out var arcDepthPerRadius))
                return false;

            var radius = LiveBuilderCornerRadii.RadiusOf(spline, knot);
            var restOffset = HandleUtility.GetHandleSize(corner) * RestOffset;
            var handlePosition = corner + inward * (restOffset + radius * arcDepthPerRadius);
            var size = HandleUtility.GetHandleSize(handlePosition) * HandleSize;

            if (Event.current.type == EventType.Repaint)
            {
                Handles.color = HandleColor;
                Handles.DrawLine(corner, handlePosition, 2f);

                if (radius > 0f || GUIUtility.hotControl == control || HandleUtility.nearestControl == control)
                    LiveBuilderSceneText.DrawLabel(handlePosition, "R " + LiveBuilderSceneText.FormatMeters(radius) + " м");
            }

            Handles.color = HandleColor;
            EditorGUI.BeginChangeCheck();
            var moved = Handles.Slider(control, handlePosition, inward, size * 2f, Handles.SphereHandleCap, 0f);

            if (!EditorGUI.EndChangeCheck())
                return false;

            var depth = Mathf.Max(0f, Vector3.Dot(moved - corner, inward) - restOffset);
            var newRadius = Mathf.Round(depth / arcDepthPerRadius / RadiusStep) * RadiusStep;

            if (Mathf.Approximately(newRadius, radius))
                return false;

            Undo.RecordObject(container, "Round Corner");
            LiveBuilderCornerRadii.SetRadius(spline, knot, newRadius);
            return true;
        }

        // Угол в мире: сама точка, направление внутрь угла по биссектрисе и насколько середина дуги уходит от точки на метр радиуса.
        private static bool TryGetCorner(Component owner, Spline spline, int knot, out Vector3 corner, out Vector3 inward, out float arcDepthPerRadius)
        {
            var previous = owner.transform.TransformPoint(spline[(knot - 1 + spline.Count) % spline.Count].Position);
            var next = owner.transform.TransformPoint(spline[(knot + 1) % spline.Count].Position);
            corner = owner.transform.TransformPoint(spline[knot].Position);
            inward = Vector3.zero;
            arcDepthPerRadius = 0f;

            var toPrevious = Vector3.ProjectOnPlane(previous - corner, owner.transform.up).normalized;
            var toNext = Vector3.ProjectOnPlane(next - corner, owner.transform.up).normalized;
            var cornerAngle = Vector3.Angle(toPrevious, toNext) * Mathf.Deg2Rad;

            // Почти прямая или развёрнутая назад — угла нет.
            if (cornerAngle < 0.02f || cornerAngle > Mathf.PI - 0.02f)
                return false;

            inward = (toPrevious + toNext).normalized;
            arcDepthPerRadius = 1f / Mathf.Sin(cornerAngle / 2f) - 1f;
            return arcDepthPerRadius > 0.0001f;
        }
    }
}
