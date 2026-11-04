using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Рамка выбора в правке контура (<see cref="LiveBuilderContourHandles"/>): Shift + нажатие по пустому месту и протянуть —
    /// отпустил, и точки внутри рамки добавляются к выбору (<see cref="LiveBuilderContourSelection"/>).
    /// Рамка начинается, только когда мышь сдвинулась на несколько пикселей: Shift+клик без протягивания достаётся обычному выделению.
    /// </summary>
    public sealed class LiveBuilderContourBoxSelect
    {
        // Сдвиг мыши, после которого Shift+нажатие — уже рамка, а не клик, пикс. Как у инструмента: он тоже ждёт этот сдвиг.
        private const float DragThresholdPixels = 4f;

        private bool isSelecting;

        // Shift+нажатие было, но мышь ещё не сдвинулась: может, это просто клик.
        private bool isWaitingForDrag;

        // Кому досталось нажатие — у него рамка и забирает мышь, когда протянули.
        private int pressedControl;

        // Элемент рамки, пока она держит мышь.
        private int heldBoxSelectControl;

        // Углы рамки на экране.
        private Vector2 start;
        private Vector2 end;


        public bool IsSelecting => isSelecting;


        // Shift + нажатие по пустому месту (не по точке, «+» или усу — их ручки в ownControls) — запомнить: протянут — будет рамка.
        // Нажатие не забираем: если мышь не сдвинется, это Shift+клик для обычного выделения.
        public void TryBegin(HashSet<int> ownControls)
        {
            var current = Event.current;

            if (current.type != EventType.MouseDown || current.button != 0 || current.alt || !current.shift || ownControls.Contains(HandleUtility.nearestControl))
                return;

            isWaitingForDrag = true;
            pressedControl = HandleUtility.nearestControl;
            start = current.mousePosition;
            end = current.mousePosition;
        }


        // Тянут рамку; отпустил — точки внутри неё добавляются к выбору. Возвращает, забрал ли событие.
        public bool Handle(Component owner, SplineContainer container, int boxSelectControl)
        {
            var current = Event.current;

            // Новое нажатие — прежнее ожидание забыто, даже если его отпускание ушло мимо окна сцены.
            if (current.type == EventType.MouseDown)
                isWaitingForDrag = false;

            if (isWaitingForDrag)
                return TryStartSelecting(current, boxSelectControl);

            if (!isSelecting)
                return false;

            // Мышь отобрали, не дав отпустить (окно потеряло фокус), — рамки больше нет.
            if (GUIUtility.hotControl != boxSelectControl)
            {
                isSelecting = false;
                return false;
            }

            switch (current.GetTypeForControl(boxSelectControl))
            {
                case EventType.MouseDrag:
                    end = current.mousePosition;
                    current.Use();
                    return true;

                case EventType.MouseUp:
                    SelectInside(owner, container);
                    isSelecting = false;
                    GUIUtility.hotControl = 0;
                    current.Use();
                    return true;

                case EventType.Repaint:
                    LiveBuilderContourDrawing.DrawContourLines(owner);
                    LiveBuilderContourDrawing.DrawSelectedKnots(owner, container);
                    Handles.BeginGUI();
                    EditorStyles.selectionRect.Draw(ScreenRect(), GUIContent.none, false, false, false, false);
                    Handles.EndGUI();
                    return true;
            }

            return false;
        }

        // Мышь сдвинулась дальше порога — рамка забирает мышь у того, кому досталось нажатие. Отпустили раньше — это был клик.
        private bool TryStartSelecting(Event current, int boxSelectControl)
        {
            if (current.type == EventType.MouseUp)
            {
                isWaitingForDrag = false;
                return false;
            }

            var isPressStillHeld = GUIUtility.hotControl == 0 || GUIUtility.hotControl == pressedControl;

            if (current.type != EventType.MouseDrag || !isPressStillHeld || (current.mousePosition - start).magnitude <= DragThresholdPixels)
                return false;

            isWaitingForDrag = false;
            isSelecting = true;
            end = current.mousePosition;
            heldBoxSelectControl = boxSelectControl;
            GUIUtility.hotControl = boxSelectControl;
            current.Use();
            return true;
        }


        private void SelectInside(Component owner, SplineContainer container)
        {
            var rect = ScreenRect();

            for (var splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
            {
                var spline = container.Splines[splineIndex];

                for (var knotIndex = 0; knotIndex < spline.Count; knotIndex++)
                    if (rect.Contains(HandleUtility.WorldToGUIPoint(owner.transform.TransformPoint(spline[knotIndex].Position))))
                        LiveBuilderContourSelection.Add(splineIndex, knotIndex);
            }
        }

        private Rect ScreenRect()
        {
            return Rect.MinMaxRect(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y), Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y));
        }


        // Отмена: рамка пропадает, мышь освобождается, если её держит рамка.
        public void Cancel()
        {
            if (isSelecting && GUIUtility.hotControl == heldBoxSelectControl)
                GUIUtility.hotControl = 0;

            isSelecting = false;
            isWaitingForDrag = false;
        }
    }
}
