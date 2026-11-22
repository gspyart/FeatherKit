using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Панель Live Builder в окне сцены — всё для работы без окна Live Builder: инструменты, строка нового, карточка выделенного, этаж и сетка.
    /// Обычный оверлей Unity: пристыковывается, сворачивается, прячется (клавиша `); по умолчанию — правая колонка.
    /// - раскладка Panel — <see cref="LiveBuilderPanelView"/> внутри IMGUIContainer; полосы у края и в полосе окна — <see cref="LiveBuilderPanelToolbarElements"/>
    /// - перерисовывается, когда меняется выделенное, инструмент, этаж, место под мышью или то, что рисуешь
    /// </summary>
    [Overlay(typeof(SceneView), OverlayId, "Live Builder", defaultDisplay = true, defaultDockZone = DockZone.RightColumn, defaultLayout = Layout.Panel,
        defaultWidth = DefaultWidth, minWidth = MinWidth)]
    public sealed class LiveBuilderPanelOverlay : Overlay, ICreateHorizontalToolbar, ICreateVerticalToolbar
    {
        public const string OverlayId = "featherkit-live-builder-panel";

        private const float DefaultWidth = 300f;
        private const float MinWidth = 220f;

        // Сколько высоты окна сцены оставить под ряд инструментов, строку нового, нижнюю строку и поля окна, пикс.
        private const float ReservedHeight = 230f;
        private const float MinCardHeight = 120f;

        private LiveBuilderPanelView view;
        private IMGUIContainer container;
        private LiveBuilderPanelToolbarElements toolbarElements;
        private bool isShowingPanelLayout = true;
        private int lastSceneStateStamp;


        // Полная панель видна: показана, не свёрнута, не в полосе окна и не полосой сама (плавающая горизонтальная или вертикальная).
        public bool IsFullPanelShown => displayed && !collapsed && !isInToolbar && isShowingPanelLayout;


        // ── Раскладки ────────────────────────────────────────────────────

        public override VisualElement CreatePanelContent()
        {
            view ??= CreateView();
            isShowingPanelLayout = true;
            container = new IMGUIContainer(DrawView) { style = { minWidth = MinWidth } };
            return container;
        }

        private LiveBuilderPanelView CreateView()
        {
            var created = new LiveBuilderPanelView();
            created.RepaintRequested += RepaintPanel;
            return created;
        }

        private void DrawView()
        {
            var width = container.contentRect.width;
            view.Draw(float.IsNaN(width) || width < MinWidth ? DefaultWidth : width, MaxCardHeight);
        }

        private float MaxCardHeight => containerWindow != null ? Mathf.Max(MinCardHeight, containerWindow.position.height - ReservedHeight) : 400f;


        // ICreateHorizontalToolbar: полоса сверху или снизу окна сцены — инструменты, этаж, сетка.
        public OverlayToolbar CreateHorizontalToolbarContent()
        {
            return CreateToolbar(false);
        }


        // ICreateVerticalToolbar: колонка у края — инструменты и этаж.
        public OverlayToolbar CreateVerticalToolbarContent()
        {
            return CreateToolbar(true);
        }

        private OverlayToolbar CreateToolbar(bool isVertical)
        {
            isShowingPanelLayout = false;
            toolbarElements = new LiveBuilderPanelToolbarElements(isVertical);
            return toolbarElements.Root;
        }


        // ── Перерисовка ──────────────────────────────────────────────────

        private void RepaintPanel()
        {
            container?.MarkDirtyRepaint();
            toolbarElements?.Refresh();
        }


        // Окно сцены перерисовалось — панель тоже, но только если поменялось то, что она показывает: сетка, этаж, вид, жест, пересборка.
        // Перерисовка панели сама перерисовывает окно сцены — без сверки они крутили бы друг друга.
        private void RepaintWithScene(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint || sceneView != containerWindow)
                return;

            var stamp = SceneStateStamp();

            if (stamp == lastSceneStateStamp)
                return;

            lastSceneStateStamp = stamp;
            RepaintPanel();
        }

        private static int SceneStateStamp()
        {
            var tool = LiveBuilderToolPalette.Current;
            var drawingStamp = tool.IsBusy ? tool.DrawingReport?.Stamp() ?? 0 : 0;
            return System.HashCode.Combine(LiveBuilderGrid.CurrentStep, LiveBuilderPointSnap.IsEnabled, LiveBuilderViewPresets.Current(), LiveBuilderStoreys.CurrentStorey,
                tool.IsBusy, BoxModelRebuilder.SceneVersion, BoxModelWireframesSetting.IsShowingAll, drawingStamp);
        }


        // ── Unity-события ────────────────────────────────────────────────

        public override void OnCreated()
        {
            Selection.selectionChanged += RepaintPanel;
            LiveBuilderToolPalette.Changed += RepaintPanel;
            LiveBuilderStoreys.Changed += RepaintPanel;
            LiveBuilderIntoTarget.HoverChanged += RepaintPanel;
            ToolManager.activeToolChanged += RepaintPanel;
            Undo.undoRedoPerformed += RepaintPanel;
            SceneView.duringSceneGui += RepaintWithScene;
        }

        public override void OnWillBeDestroyed()
        {
            Selection.selectionChanged -= RepaintPanel;
            LiveBuilderToolPalette.Changed -= RepaintPanel;
            LiveBuilderStoreys.Changed -= RepaintPanel;
            LiveBuilderIntoTarget.HoverChanged -= RepaintPanel;
            ToolManager.activeToolChanged -= RepaintPanel;
            Undo.undoRedoPerformed -= RepaintPanel;
            SceneView.duringSceneGui -= RepaintWithScene;
            view?.Dispose();
            view = null;
            toolbarElements = null;
        }
    }
}
