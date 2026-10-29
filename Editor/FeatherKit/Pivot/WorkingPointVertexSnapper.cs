using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Привязка рабочей точки к вершинам: пока зажат Ctrl (на Mac — Cmd), у курсора видна
    /// ближайшая вершина любого меша сцены, клик ставит туда рабочую точку.
    /// Ручки при этом работают как обычно: клик по ручке — это ручка, мимо — вершина.
    /// </summary>
    public static class WorkingPointVertexSnapper
    {
        private static readonly Color VertexMarkerColor = new(0.3f, 0.9f, 1f);


        public static void HandleSceneInput(SceneView sceneView)
        {
            var current = Event.current;

            // Номер берём на каждом событии, даже без Ctrl: иначе собьются номера ручек.
            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            if (!EditorGUI.actionKey)
                return;

            switch (current.type)
            {
                // Клик мимо ручек — наш, а не выделение объекта.
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(controlId);
                    break;

                case EventType.MouseMove:
                    sceneView.Repaint();
                    break;

                case EventType.Repaint:
                    if (TryFindVertexUnderCursor(current.mousePosition, out var hoveredVertex))
                        DrawVertexMarker(hoveredVertex, sceneView.camera.transform.forward);
                    break;

                // С Alt левая кнопка вращает камеру — её не перехватываем.
                case EventType.MouseDown when current.button == 0 && !current.alt && HandleUtility.nearestControl == controlId:
                    if (TryFindVertexUnderCursor(current.mousePosition, out var clickedVertex))
                        WorkingPoint.MoveTo(clickedVertex);

                    current.Use();
                    break;
            }
        }

        // Вершина только того объекта, что виден под курсором: без этого цеплялись
        // вершины мешей позади него — точка уезжала вглубь сцены.
        private static bool TryFindVertexUnderCursor(Vector2 mousePosition, out Vector3 vertex)
        {
            vertex = default;
            var underCursor = HandleUtility.PickGameObject(mousePosition, false);

            return underCursor != null
                   && HandleUtility.FindNearestVertex(mousePosition, new[] { underCursor.transform }, out vertex);
        }


        private static void DrawVertexMarker(Vector3 vertex, Vector3 viewDirection)
        {
            float size = HandleUtility.GetHandleSize(vertex);

            using (new Handles.DrawingScope(VertexMarkerColor))
            {
                Handles.DrawWireDisc(vertex, viewDirection, size * 0.08f, 2f);
                Handles.DotHandleCap(0, vertex, Quaternion.identity, size * 0.02f, EventType.Repaint);
            }
        }
    }
}
