using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Инструмент «Своя точка» в панели инструментов сцены: ручки сдвига, поворота и масштаба
    /// стоят не на пивоте выделенного, а в рабочей точке — например, на вершине дверного косяка.
    /// Ctrl + клик ставит точку на ближайшую вершину. Режимы и быстрые места — на панели
    /// <see cref="WorkingPointOverlay"/>, сама точка — <see cref="WorkingPoint"/>.
    /// Оси ручек — как у штатных: Global или Local на панели сцены.
    /// </summary>
    [EditorTool("Working Point")]
    public class WorkingPointTool : EditorTool
    {
        private const float PointMarkerSize = 0.1f;
        private const float MinScaleHandleValue = 0.01f;

        private static readonly Color PointMarkerColor = new(1f, 0.55f, 0.1f);

        // Сколько ручка масштаба показывает с начала перетаскивания: растяжение считаем от прошлого кадра.
        private Vector3 scaleHandleValue = Vector3.one;


        public override GUIContent toolbarIcon =>
            new(EditorGUIUtility.IconContent("ToolHandlePivot").image,
                "Своя точка: ручки вращают и растягивают выделенное вокруг рабочей точки. Ctrl + клик — точка на вершину");

        // Оси ручек: Local — как у активного объекта, Global — мира.
        private static Quaternion HandleAxes =>
            Tools.pivotRotation == PivotRotation.Local && Selection.activeTransform != null
                ? Selection.activeTransform.rotation
                : Quaternion.identity;


        // Клавиши по умолчанию нет: W, E, R и соседние заняты. Назначить — Edit → Shortcuts → FeatherKit.
        [Shortcut("FeatherKit/Working Point Tool")]
        public static void Activate() => ToolManager.SetActiveTool<WorkingPointTool>();


        // ── Включение ────────────────────────────────────

        public override void OnActivated()
        {
            if (!WorkingPoint.IsPlaced)
                WorkingPoint.MoveToActivePivot();
        }


        // ── Ручки ────────────────────────────────────────

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView sceneView)
                return;

            WorkingPointVertexSnapper.HandleSceneInput(sceneView);
            DrawPointMarker();

            if (WorkingPoint.Mode == WorkingPointMode.MovePoint)
                DrawMovePointHandle();
            else if (Selection.transforms.Length > 0)
                DrawSelectionHandle();
        }

        private static void DrawPointMarker()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var point = WorkingPoint.Position;

            using (new Handles.DrawingScope(PointMarkerColor))
                Handles.SphereHandleCap(0, point, Quaternion.identity, HandleUtility.GetHandleSize(point) * PointMarkerSize, EventType.Repaint);
        }

        private static void DrawMovePointHandle()
        {
            EditorGUI.BeginChangeCheck();
            var newPoint = Handles.PositionHandle(WorkingPoint.Position, HandleAxes);

            if (EditorGUI.EndChangeCheck())
                WorkingPoint.MoveTo(newPoint);
        }

        private void DrawSelectionHandle()
        {
            switch (WorkingPoint.Mode)
            {
                case WorkingPointMode.MoveSelection:
                    DrawMoveSelectionHandle();
                    break;

                case WorkingPointMode.RotateSelection:
                    DrawRotateSelectionHandle();
                    break;

                case WorkingPointMode.ScaleSelection:
                    DrawScaleSelectionHandle();
                    break;
            }
        }

        // Точка едет вместе с выделенным, как пивот у штатного сдвига.
        private static void DrawMoveSelectionHandle()
        {
            var point = WorkingPoint.Position;

            EditorGUI.BeginChangeCheck();
            var newPoint = Handles.PositionHandle(point, HandleAxes);

            if (!EditorGUI.EndChangeCheck())
                return;

            WorkingPointTransformer.MoveSelection(newPoint - point);
            WorkingPoint.MoveTo(newPoint);
        }

        private static void DrawRotateSelectionHandle()
        {
            var axes = HandleAxes;

            EditorGUI.BeginChangeCheck();
            var newAxes = Handles.RotationHandle(axes, WorkingPoint.Position);

            if (EditorGUI.EndChangeCheck())
                WorkingPointTransformer.RotateSelection(WorkingPoint.Position, newAxes * Quaternion.Inverse(axes));
        }

        private void DrawScaleSelectionHandle()
        {
            if (GUIUtility.hotControl == 0)
                scaleHandleValue = Vector3.one;

            var axes = HandleAxes;

            EditorGUI.BeginChangeCheck();
            var newValue = Vector3.Max(Handles.ScaleHandle(scaleHandleValue, WorkingPoint.Position, axes), Vector3.one * MinScaleHandleValue);

            if (!EditorGUI.EndChangeCheck())
                return;

            var factor = new Vector3(newValue.x / scaleHandleValue.x, newValue.y / scaleHandleValue.y, newValue.z / scaleHandleValue.z);
            WorkingPointTransformer.ScaleSelection(WorkingPoint.Position, axes, factor);
            scaleHandleValue = newValue;
        }
    }
}
