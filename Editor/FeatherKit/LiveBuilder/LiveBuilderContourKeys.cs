using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Клавиши правки контура (<see cref="LiveBuilderContourHandles"/>): Delete — убрать выбранные точки (у замкнутого контура остаётся хотя бы три,
    /// у линии — две; объект целиком тогда не удаляется), стрелки — выбранные на шаг сетки, C у стены по линии — замкнуть или разомкнуть.
    /// Стрелки и C — ярлыки (<see cref="LiveBuilderContourShortcuts"/>), снять выбор — цепочка Esc (<see cref="LiveBuilderSceneKeyboard.Cancel"/>).
    /// Пока тянут точку или рамку, Delete только глушится, чтобы не удалить объект целиком. Здание всегда замкнуто.
    /// </summary>
    public static class LiveBuilderContourKeys
    {
        private const int MinClosedKnots = 3;
        private const int MinOpenKnots = 2;


        // Delete у выбранных точек — команда Unity, не ярлык. Возвращает, забрал ли событие. isGestureActive — тянут точку или рамку.
        public static bool Handle(Component owner, SplineContainer container, bool isGestureActive)
        {
            var current = Event.current;

            if (isGestureActive)
                return SwallowDelete(current);

            if (LiveBuilderContourSelection.Knots.Count == 0 || !IsDelete(current))
                return false;

            if (current.type != EventType.ValidateCommand)
                RemoveSelectedKnots(container);

            current.Use();
            return true;
        }

        // Delete посреди перетаскивания: ничего не убираем, но и окну сцены не отдаём — иначе удалит объект.
        private static bool SwallowDelete(Event current)
        {
            if (!IsDelete(current))
                return false;

            current.Use();
            return true;
        }

        private static bool IsDelete(Event current)
        {
            var isDeleteCommand = current.type is EventType.ValidateCommand or EventType.ExecuteCommand && current.commandName is "Delete" or "SoftDelete";
            var isDeleteKey = current.type == EventType.KeyDown && current.keyCode is KeyCode.Delete or KeyCode.Backspace;
            return isDeleteCommand || isDeleteKey;
        }


        // ── Ярлыки ───────────────────────────────────────────────────────

        // Стрелка — выбранные точки показанного контура на шаг сетки.
        public static void NudgeSelectedPoints(KeyCode arrow)
        {
            var owner = LiveBuilderContourSelection.Owner;

            if (owner == null || LiveBuilderContourSelection.Knots.Count == 0 || !owner.TryGetComponent<SplineContainer>(out var container)
                || !TryGetArrowShift(owner, arrow, out var localShift))
                return;

            LiveBuilderContourDrag.ShiftSelected(container, localShift, "Nudge Contour Points");
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
            var worldShift = (frame != null ? frame.TransformDirection(direction) : direction) * LiveBuilderGrid.CurrentStep;
            localShift = owner.transform.InverseTransformDirection(worldShift);
            return direction != Vector3.zero;
        }


        // C — у стены по линии, пока видны её точки, выбраны они или нет.
        public static void ToggleClosedOfShownContour()
        {
            var owner = LiveBuilderContourSelection.Owner;

            if (owner != null && owner.TryGetComponent<SplineContainer>(out var container) && CanToggleClosed(owner, container))
                ToggleClosed(container);
        }


        // Замкнуть или разомкнуть — только стену по линии, и замкнуть — не меньше чем из трёх точек.
        public static bool CanToggleClosed(Component owner, SplineContainer container)
        {
            return owner is FeatherKit.LiveBuilder.SplineWall && (container.Spline.Closed || container.Spline.Count >= MinClosedKnots);
        }


        // Замкнуть: последняя точка соединяется с первой. Разомкнуть: замыкающий отрезок убирается, точки на месте. Одним шагом Ctrl+Z;
        // стена пересобирается сама, проёмы на линии остаются, где стояли. Зовут клавиша C в сцене и кнопка в окне.
        public static void ToggleClosed(SplineContainer container)
        {
            Undo.RecordObject(container, container.Spline.Closed ? "Open Wall" : "Close Wall");
            container.Spline.Closed = !container.Spline.Closed;
        }


        // С конца, чтобы номера ещё не убранных не съехали. Меньше трёх точек у замкнутого или двух у линии не бывает. Зовёт и окно.
        public static void RemoveSelectedKnots(SplineContainer container)
        {
            var knots = new List<(int Spline, int Knot)>(LiveBuilderContourSelection.Knots);
            knots.Sort((first, second) => second.Knot.CompareTo(first.Knot));
            Undo.RecordObject(container, "Remove Contour Points");

            foreach (var (splineIndex, knotIndex) in knots)
            {
                if (!LiveBuilderContourSelection.IsKnotInside(container, splineIndex, knotIndex))
                    continue;

                var spline = container.Splines[splineIndex];

                if (spline.Count > (spline.Closed ? MinClosedKnots : MinOpenKnots))
                    spline.RemoveAt(knotIndex);
            }

            LiveBuilderContourSelection.Clear();
        }
    }
}
