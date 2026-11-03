using FeatherKit.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Ручки модификатора Array (<see cref="ArrayModifier"/>) в окне сцены, в осях объекта:
    /// - точка в опоре каждой копии — видно раскладку, даже где копии закрыты стеной
    /// - у сетки — стрелка на конце каждой оси: тянешь — копий больше или меньше с тем же шагом; с Shift — меняется шаг
    /// - у круга — дуга и ручка радиуса в центре
    /// Шаг и радиус — по 5 см. Возвращает, поменялись ли настройки: тогда массив пересобирают.
    /// </summary>
    public static class ArrayModifierHandles
    {
        private const float Snap = 0.05f;
        private const float ArrowSize = 0.9f;
        private const float DotSize = 0.04f;

        private static readonly Color DotColor = new(1f, 0.85f, 0.2f, 0.9f);
        private static readonly Color[] AxisColors = { Handles.xAxisColor, Handles.yAxisColor, Handles.zAxisColor };
        private static readonly Vector3[] Axes = { Vector3.right, Vector3.up, Vector3.forward };


        public static bool Draw(ArrayModifier array)
        {
            if (ArrayModifierBuilder.ProblemOf(array) != null)
                return false;

            var copySize = ArrayModifierBuilder.CopySizeOf(array.gameObject);
            var frame = Matrix4x4.TRS(array.transform.position, array.transform.rotation, Vector3.one);

            using (new Handles.DrawingScope(frame))
            {
                DrawCopyDots(array, copySize);

                return array.Layout == ArrayModifierLayout.Circle ? DrawRadiusHandle(array) : DrawGridArrows(array, copySize);
            }
        }

        private static void DrawCopyDots(ArrayModifier array, Vector3 copySize)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            Handles.color = DotColor;

            foreach (var pose in ArrayModifierPlacement.AllPosesOf(array, copySize))
                Handles.DotHandleCap(0, pose.Position, Quaternion.identity, HandleUtility.GetHandleSize(pose.Position) * DotSize, EventType.Repaint);
        }


        // ── Сетка ────────────────────────────────────────────────────────

        // Стрелка по каждой оси — на последней копии вдоль неё.
        private static bool DrawGridArrows(ArrayModifier array, Vector3 copySize)
        {
            var isChanged = false;

            for (var axis = 0; axis < 3; axis++)
                isChanged |= DrawGridArrow(array, copySize, axis);

            return isChanged;
        }

        private static bool DrawGridArrow(ArrayModifier array, Vector3 copySize, int axis)
        {
            var counts = array.Counts;
            var step = ArrayModifierPlacement.GridStepOf(array, copySize)[axis];
            var direction = Axes[axis];
            var tip = direction * (step * (counts[axis] - 1));
            var handleSize = HandleUtility.GetHandleSize(tip) * ArrowSize;

            Handles.color = AxisColors[axis];
            EditorGUI.BeginChangeCheck();
            var moved = Handles.Slider(tip, direction, handleSize, Handles.ArrowHandleCap, 0f);
            Handles.Label(tip + direction * handleSize * 1.15f, Event.current.shift ? FormatMeters(step) : "×" + counts[axis], EditorStyles.boldLabel);

            if (!EditorGUI.EndChangeCheck())
                return false;

            var along = Vector3.Dot(moved, direction);
            Undo.RecordObject(array, "Change Array");

            if (Event.current.shift)
                SetStep(array, copySize, axis, counts[axis] > 1 ? along / (counts[axis] - 1) : along);
            else
                SetCount(array, axis, along, EffectiveStep(step, copySize[axis]));

            return true;
        }

        // Шаг ноль — копии в одной точке: тогда число считаем по размеру копии, иначе стрелка не двигала бы ничего.
        private static float EffectiveStep(float step, float copySize)
        {
            if (Mathf.Abs(step) > 0.01f)
                return step;

            return copySize > 0.01f ? copySize : 1f;
        }

        private static void SetCount(ArrayModifier array, int axis, float along, float step)
        {
            var counts = array.Counts;
            counts[axis] = Mathf.Clamp(Mathf.RoundToInt(along / step) + 1, 1, ArrayModifier.MaxCopies);
            array.Counts = counts;
        }

        // Шаг от опоры — сам шаг; от края — зазор, размер копии тот же.
        private static void SetStep(ArrayModifier array, Vector3 copySize, int axis, float step)
        {
            step = Mathf.Round(step / Snap) * Snap;

            if (array.StepsFromSize)
            {
                var gap = array.Gap;
                gap[axis] = step - copySize[axis];
                array.Gap = gap;
            }
            else
            {
                var arrayStep = array.Step;
                arrayStep[axis] = step;
                array.Step = arrayStep;
            }
        }


        // ── Круг ─────────────────────────────────────────────────────────

        // Дуга копий и ручка в центре круга: тянешь от объекта или к нему — меняется радиус.
        private static bool DrawRadiusHandle(ArrayModifier array)
        {
            var center = ArrayModifierPlacement.CircleCenterOf(array);
            var towardCenter = center.sqrMagnitude > 1e-6f ? center.normalized : Vector3.forward;

            Handles.color = DotColor;
            Handles.DrawWireArc(center, Vector3.up, -towardCenter, array.Arc, array.Radius);

            Handles.color = Handles.zAxisColor;
            EditorGUI.BeginChangeCheck();
            var moved = Handles.Slider(center, towardCenter, HandleUtility.GetHandleSize(center) * ArrowSize, Handles.ArrowHandleCap, 0f);
            Handles.Label(center + Vector3.up * HandleUtility.GetHandleSize(center) * 0.3f, "R " + FormatMeters(array.Radius), EditorStyles.boldLabel);

            if (!EditorGUI.EndChangeCheck())
                return false;

            Undo.RecordObject(array, "Change Array");
            array.Radius = Mathf.Round(Mathf.Max(0f, Vector3.Dot(moved, towardCenter)) / Snap) * Snap;
            return true;
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static string FormatMeters(float meters)
        {
            return meters.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " m";
        }
    }
}
