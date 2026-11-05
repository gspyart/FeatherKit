using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Стрелки у выбранных точек контура (<see cref="LiveBuilderContourSelection"/>), как у точек в ProBuilder и tsitski: в середине выбранных —
    /// стрелки по X и Z и квадрат для переноса в плане; у стены по линии ещё и стрелка вверх — её низ идёт по высоте точек, а контур здания плоский.
    /// Тянешь — все выбранные едут на целое число клеток сетки от исходного места, в осях сетки контура (<see cref="LiveBuilderContourDrag.SnapFrameOf"/>).
    /// Весь жест — один шаг Ctrl+Z. Тянуть саму точку по-прежнему можно (<see cref="LiveBuilderContourDrag"/>): там прилипание к стенам здания и длины сторон.
    /// </summary>
    public sealed class LiveBuilderContourMoveHandle
    {
        // Квадрат переноса в плане — доля размера ручки.
        private const float PlaneHandleSize = 0.15f;

        private static readonly string[] AxisNames = { "X", "Y", "Z" };

        // Выбранные точки и где они были в начале: сдвиг считается от начала, без накопления округлений.
        private readonly List<(int Spline, int Knot)> movedKnots = new();
        private readonly List<float3> startPositions = new();

        private bool isDragging;
        private int undoGroup;
        private Vector3 startCenter;
        private Quaternion axes;
        private bool canMoveUp;

        // Сдвиг по сетке в осях стрелок — на столько уже уехало.
        private Vector3 snappedOffset;


        // Тянут стрелки — пока тянут, клавиши контура (Delete, стрелки) не действуют.
        public bool IsDragging => isDragging;


        // ── Стрелки ──────────────────────────────────────────────────────

        // Стрелки в середине выбранных точек; ничего не выбрано — их нет. Звать раньше точек: точка под стрелками забирает нажатие себе.
        // Возвращает, сдвинулись ли точки.
        public bool Draw(Component owner, SplineContainer container)
        {
            EndIfReleased();

            if (!isDragging && LiveBuilderContourSelection.Knots.Count == 0)
                return false;

            var handleAxes = isDragging ? axes : AxesOf(owner);
            var center = isDragging ? startCenter + axes * snappedOffset : CenterOf(owner, container);
            var size = HandleUtility.GetHandleSize(center);
            var isWall = owner is SplineWall;

            EditorGUI.BeginChangeCheck();
            var moved = center;
            moved += Arrow(center, handleAxes * Vector3.right, size, Handles.xAxisColor) - center;
            moved += Arrow(center, handleAxes * Vector3.forward, size, Handles.zAxisColor) - center;

            if (isWall)
                moved += Arrow(center, handleAxes * Vector3.up, size, Handles.yAxisColor) - center;

            Handles.color = Handles.yAxisColor;
            moved += Handles.Slider2D(center, handleAxes * Vector3.up, handleAxes * Vector3.right, handleAxes * Vector3.forward, size * PlaneHandleSize,
                Handles.RectangleHandleCap, 0f) - center;

            var isChanged = EditorGUI.EndChangeCheck();

            if (isChanged)
            {
                if (!isDragging)
                    BeginDrag(owner, container, center, handleAxes, isWall);

                DragTo(owner, container, moved);
            }

            DrawDragLabel();
            return isChanged;
        }

        private static Vector3 Arrow(Vector3 center, Vector3 direction, float size, Color color)
        {
            Handles.color = color;
            return Handles.Slider(center, direction, size, Handles.ArrowHandleCap, 0f);
        }

        // Середина выбранных точек в мире.
        private static Vector3 CenterOf(Component owner, SplineContainer container)
        {
            var sum = Vector3.zero;
            var count = 0;

            foreach (var (splineIndex, knotIndex) in LiveBuilderContourSelection.Knots)
            {
                if (!LiveBuilderContourSelection.IsKnotInside(container, splineIndex, knotIndex))
                    continue;

                sum += owner.transform.TransformPoint(container.Splines[splineIndex][knotIndex].Position);
                count++;
            }

            return count > 0 ? sum / count : owner.transform.position;
        }

        // Оси сетки контура: здание у перегородки, объект из коробок вокруг, иначе мир.
        private static Quaternion AxesOf(Component owner)
        {
            var frame = LiveBuilderContourDrag.SnapFrameOf(owner);
            return frame != null ? frame.rotation : Quaternion.identity;
        }


        // ── Перенос ──────────────────────────────────────────────────────

        // Начать перенос выбранных точек от середины center; canMoveUpward — можно ли вверх (стена), иначе только в плане. Один шаг Ctrl+Z.
        public void BeginDrag(Component owner, SplineContainer container, Vector3 center, Quaternion handleAxes, bool canMoveUpward)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Move Contour Points");
            undoGroup = Undo.GetCurrentGroup();

            isDragging = true;
            startCenter = center;
            axes = handleAxes;
            canMoveUp = canMoveUpward;
            snappedOffset = Vector3.zero;
            movedKnots.Clear();
            startPositions.Clear();

            foreach (var (splineIndex, knotIndex) in LiveBuilderContourSelection.Knots)
            {
                if (!LiveBuilderContourSelection.IsKnotInside(container, splineIndex, knotIndex))
                    continue;

                movedKnots.Add((splineIndex, knotIndex));
                startPositions.Add(container.Splines[splineIndex][knotIndex].Position);
            }
        }


        // Стрелки ушли сюда: все выбранные точки — на целые клетки сетки в осях стрелок от того, где были.
        public void DragTo(Component owner, SplineContainer container, Vector3 handle)
        {
            if (!isDragging)
                return;

            var offset = LiveBuilderGrid.Snap(Quaternion.Inverse(axes) * (handle - startCenter));

            if (!canMoveUp)
                offset.y = 0f;

            if (offset == snappedOffset)
                return;

            snappedOffset = offset;
            var localOffset = owner.transform.InverseTransformVector(axes * snappedOffset);

            if (!canMoveUp)
                localOffset.y = 0f;

            Undo.RecordObject(container, "Move Contour Points");

            for (var index = 0; index < movedKnots.Count; index++)
            {
                var (splineIndex, knotIndex) = movedKnots[index];

                if (!LiveBuilderContourSelection.IsKnotInside(container, splineIndex, knotIndex))
                    continue;

                var spline = container.Splines[splineIndex];
                var knot = spline[knotIndex];
                knot.Position = startPositions[index] + (float3)localOffset;
                spline.SetKnot(knotIndex, knot);
            }
        }


        // Отпустили — весь жест одним шагом Ctrl+Z.
        public void EndIfReleased()
        {
            if (isDragging && GUIUtility.hotControl == 0)
                EndDrag();
        }

        public void EndDrag()
        {
            if (!isDragging)
                return;

            Undo.CollapseUndoOperations(undoGroup);
            isDragging = false;
            movedKnots.Clear();
            startPositions.Clear();
        }


        // ── Подпись ──────────────────────────────────────────────────────

        // У стрелок, пока тянешь: «X +2 · Z −1 м».
        private void DrawDragLabel()
        {
            if (!isDragging || Event.current.type != EventType.Repaint)
                return;

            var parts = new List<string>();

            for (var axis = 0; axis < 3; axis++)
                if (!Mathf.Approximately(snappedOffset[axis], 0f))
                    parts.Add(AxisNames[axis] + " " + (snappedOffset[axis] > 0f ? "+" : "−") + LiveBuilderSceneText.FormatMeters(snappedOffset[axis]));

            var text = "точек: " + movedKnots.Count + " · " + (parts.Count > 0 ? string.Join(" · ", parts) + " м" : "0 м");
            LiveBuilderSceneText.DrawLabel(startCenter + axes * snappedOffset, text);
        }
    }
}
