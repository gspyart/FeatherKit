using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Клавиши правки контура (<see cref="LiveBuilderContourHandles"/>), пока выбраны точки: Delete — убрать выбранные
    /// (у замкнутого контура остаётся хотя бы три, у линии — две; объект целиком тогда не удаляется), стрелки — сдвинуть на шаг сетки, Esc — снять выбор.
    /// </summary>
    public static class LiveBuilderContourKeys
    {
        private const int MinClosedKnots = 3;
        private const int MinOpenKnots = 2;


        // Возвращает, забрал ли событие.
        public static bool Handle(Component owner, SplineContainer container)
        {
            var current = Event.current;

            if (LiveBuilderContourSelection.Knots.Count == 0)
                return false;

            var isDeleteCommand = current.type is EventType.ValidateCommand or EventType.ExecuteCommand && current.commandName is "Delete" or "SoftDelete";
            var isDeleteKey = current.type == EventType.KeyDown && current.keyCode is KeyCode.Delete or KeyCode.Backspace;

            if (isDeleteCommand || isDeleteKey)
            {
                if (current.type != EventType.ValidateCommand)
                    RemoveSelectedKnots(container);

                current.Use();
                return true;
            }

            if (current.type != EventType.KeyDown)
                return false;

            if (current.keyCode == KeyCode.Escape)
            {
                LiveBuilderContourSelection.Clear();
                current.Use();
                return true;
            }

            if (!TryGetArrowShift(owner, current.keyCode, out var localShift))
                return false;

            LiveBuilderContourDrag.ShiftSelected(container, localShift, "Nudge Contour Points");
            current.Use();
            return true;
        }

        // Стрелки — на шаг сетки вдоль осей сетки: влево-вправо — X, вверх-вниз — Z.
        private static bool TryGetArrowShift(Component owner, KeyCode key, out Vector3 localShift)
        {
            var direction = key switch
            {
                KeyCode.LeftArrow => Vector3.left,
                KeyCode.RightArrow => Vector3.right,
                KeyCode.UpArrow => Vector3.forward,
                KeyCode.DownArrow => Vector3.back,
                _ => Vector3.zero
            };

            var frame = LiveBuilderContourDrag.SnapFrameOf(owner);
            var worldShift = (frame != null ? frame.TransformDirection(direction) : direction) * BoxModelGrid.CurrentStep;
            localShift = owner.transform.InverseTransformDirection(worldShift);
            return direction != Vector3.zero;
        }


        // С конца, чтобы номера ещё не убранных не съехали. Меньше трёх точек у замкнутого или двух у линии не бывает. Зовёт и окно.
        public static void RemoveSelectedKnots(SplineContainer container)
        {
            var knots = new List<(int Spline, int Knot)>(LiveBuilderContourSelection.Knots);
            knots.Sort((first, second) => second.Knot.CompareTo(first.Knot));
            Undo.RecordObject(container, "Remove Contour Points");

            foreach (var (splineIndex, knotIndex) in knots)
            {
                var spline = container.Splines[splineIndex];

                if (spline.Count > (spline.Closed ? MinClosedKnots : MinOpenKnots))
                    spline.RemoveAt(knotIndex);
            }

            LiveBuilderContourSelection.Clear();
        }
    }
}
