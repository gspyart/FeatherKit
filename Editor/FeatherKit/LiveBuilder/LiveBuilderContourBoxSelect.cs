using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Рамка выбора в правке контура (<see cref="LiveBuilderContourHandles"/>): Shift + нажатие по пустому месту и протянуть —
    /// отпустил, и точки внутри рамки добавляются к выбору (<see cref="LiveBuilderContourSelection"/>).
    /// </summary>
    public sealed class LiveBuilderContourBoxSelect
    {
        private bool isSelecting;

        // Углы рамки на экране.
        private Vector2 start;
        private Vector2 end;


        // Shift + нажатие по пустому месту (не по точке, «+» или усу — их ручки в ownControls) — начать рамку.
        public void TryBegin(int boxSelectControl, HashSet<int> ownControls)
        {
            var current = Event.current;

            if (current.type != EventType.MouseDown || current.button != 0 || current.alt || !current.shift || ownControls.Contains(HandleUtility.nearestControl))
                return;

            isSelecting = true;
            start = current.mousePosition;
            end = current.mousePosition;
            GUIUtility.hotControl = boxSelectControl;
            current.Use();
        }


        // Тянут рамку; отпустил — точки внутри неё добавляются к выбору. Возвращает, забрал ли событие.
        public bool Handle(Component owner, SplineContainer container, int boxSelectControl)
        {
            var current = Event.current;

            if (!isSelecting || GUIUtility.hotControl != boxSelectControl)
                return false;

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


        public void Cancel()
        {
            isSelecting = false;
        }
    }
}
