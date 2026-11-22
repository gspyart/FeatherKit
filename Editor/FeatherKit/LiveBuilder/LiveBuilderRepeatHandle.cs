using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Ручка повтора (<see cref="LiveBuilderArray"/>) в окне сцены: стрелка за последней копией выделенного повтора по его главной оси.
    /// Тянешь вдоль шага — копий больше или меньше, рядом надпись «× 5». С Undo.
    /// </summary>
    public static class LiveBuilderRepeatHandle
    {
        private static readonly Color HandleColor = new(0.75f, 0.45f, 1f);

        // Пока тянешь: сколько было копий и где была ручка в начале — число считаем от них.
        private static bool isDragging;
        private static int countAtDragStart;
        private static Vector3 positionAtDragStart;
        private static Vector3 handlePosition;


        public static void DrawForSelection()
        {
            var selected = Selection.activeGameObject;
            var array = selected != null ? selected.GetComponentInParent<LiveBuilderArray>() : null;

            if (isDragging && GUIUtility.hotControl == 0)
                isDragging = false;

            if (array == null || !TryGetMainAxis(array, out var axis))
                return;

            var axisDirection = Vector3.zero;
            axisDirection[axis] = 1f;
            var worldStep = array.transform.rotation * axisDirection * array.StepOf(axis);
            var count = array.CountOf(axis);
            var worldPosition = array.transform.position + worldStep * (count - 0.5f);
            var worldDirection = worldStep.normalized;

            Handles.color = HandleColor;
            var position = isDragging ? handlePosition : worldPosition;

            EditorGUI.BeginChangeCheck();
            var moved = Handles.Slider(position, worldDirection, HandleUtility.GetHandleSize(position) * 0.9f, Handles.ArrowHandleCap, 0f);

            if (EditorGUI.EndChangeCheck())
            {
                if (!isDragging)
                {
                    isDragging = true;
                    countAtDragStart = count;
                    positionAtDragStart = position;
                }

                handlePosition = moved;
                var stepsMoved = Mathf.RoundToInt(Vector3.Dot(moved - positionAtDragStart, worldDirection) / worldStep.magnitude);
                var newCount = Mathf.Clamp(countAtDragStart + stepsMoved, 1, LiveBuilderArray.MaxCountPerAxis);

                if (newCount != count)
                {
                    Undo.RecordObject(array, "Change Repeat Count");
                    SetCount(array, axis, newCount);
                }
            }

            LiveBuilderSceneText.DrawLabel(position, "× " + array.CountOf(axis));
        }

        // Ось с копиями и шагом: X, потом Z, потом Y. Y «до верха здания» и шаг-этаж считает здание — ручка не нужна.
        private static bool TryGetMainAxis(LiveBuilderArray array, out int axis)
        {
            foreach (var candidate in new[] { 0, 2, 1 })
            {
                var isDrivenByStoreys = candidate == 1 && (array.CountYToBuildingTop || array.StepYIsStoreyHeight);

                if (!isDrivenByStoreys && array.CountOf(candidate) > 1 && Mathf.Abs(array.StepOf(candidate)) > 0.001f)
                {
                    axis = candidate;
                    return true;
                }
            }

            axis = -1;
            return false;
        }

        private static void SetCount(LiveBuilderArray array, int axis, int count)
        {
            if (axis == 0)
                array.CountX = count;
            else if (axis == 1)
                array.CountY = count;
            else
                array.CountZ = count;
        }
    }
}
