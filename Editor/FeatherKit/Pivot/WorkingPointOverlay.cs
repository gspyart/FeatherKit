using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Панель инструмента «Своя точка» в окне сцены, видна только пока он включён:
    /// режим ручек, быстрые места для точки и перенос настоящего пивота в неё.
    /// </summary>
    [Overlay(typeof(SceneView), OverlayId, "Working Point")]
    public class WorkingPointOverlay : Overlay, ITransientOverlay
    {
        private const string OverlayId = "featherkit-working-point";
        private const float PanelWidth = 230f;

        private static readonly GUIContent[] ModeTitles =
        {
            new("Точка", "Двигать саму точку"),
            new("Сдвиг", "Двигать выделенное"),
            new("Поворот", "Вращать выделенное вокруг точки"),
            new("Масштаб", "Растягивать выделенное от точки")
        };

        private IMGUIContainer content;


        // ── ITransientOverlay ────────────────────────────

        public bool visible => ToolManager.activeToolType == typeof(WorkingPointTool);


        // ── Содержимое ───────────────────────────────────

        public override VisualElement CreatePanelContent()
        {
            content = new IMGUIContainer(DrawContent);
            content.style.width = PanelWidth;
            return content;
        }


        private static void DrawContent()
        {
            var mode = (WorkingPointMode)GUILayout.Toolbar((int)WorkingPoint.Mode, ModeTitles);

            if (mode != WorkingPoint.Mode)
                WorkingPoint.SwitchMode(mode);

            EditorGUILayout.LabelField("Ctrl + клик — точка на вершину", EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent("К пивоту", "Точка — на пивот активного объекта")))
                WorkingPoint.MoveToActivePivot();

            if (GUILayout.Button(new GUIContent("К центру", "Точка — в середину выделенного")))
                WorkingPoint.MoveToSelectionCenter();

            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(Selection.transforms.Length == 0))
            {
                if (GUILayout.Button(new GUIContent("Сделать пивотом", "Перенести настоящий пивот выделенного в точку, не двигая детей")))
                    PivotMover.MovePivots(Selection.transforms, _ => WorkingPoint.Position, "Пивот в свою точку");
            }
        }


        // ── Перерисовка ──────────────────────────────────

        // Точку двигают и в сцене — тогда панель перерисовывается следом.
        public override void OnCreated() => WorkingPoint.Changed += RepaintContent;

        public override void OnWillBeDestroyed() => WorkingPoint.Changed -= RepaintContent;

        private void RepaintContent() => content?.MarkDirtyRepaint();
    }
}
