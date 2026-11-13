using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Не рисовать то, чего не видно или некогда разглядеть, — часть ScenarioGraphView. Касается стопок блоков (IMGUI):
    /// - за краем холста и при сильном отдалении — не рисуются
    /// - пока холст двигают и зумят — тоже, появляются, когда он замер
    /// Каждая IMGUI-стопка стоит ~0.65 мс на перерисовку окна. Размер нод не меняется — провода на месте.
    /// </summary>
    internal class ScenarioCanvasDetailCulling
    {
        /// <summary>Мельче этого масштаба поля блоков не прочесть — видны шапки, сводки и провода.</summary>
        public const float DetailScale = 0.6f;

        private const long SettleMilliseconds = 150;

        private readonly ScenarioGraphView graphView;
        private readonly IVisualElementScheduledItem settle;
        private bool isMoving;


        public ScenarioCanvasDetailCulling(ScenarioGraphView graphView)
        {
            this.graphView = graphView;

            settle = graphView.schedule.Execute(Settle);
            settle.Pause();

            graphView.viewTransformChanged += _ => HandleViewMoved();
            graphView.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            graphView.Rebuilt += () => graphView.schedule.Execute(Refresh);
        }


        public void Refresh()
        {
            var showsDetails = !isMoving && graphView.viewTransform.scale.x >= DetailScale;
            var visibleArea = graphView.worldBound;

            graphView.contentViewContainer.Query<IMGUIContainer>().ForEach(container =>
                container.visible = showsDetails && Overlaps(container.worldBound, visibleArea));
        }

        // До первой раскладки размеров нет — такую стопку лучше нарисовать, чем потерять.
        private static bool Overlaps(Rect area, Rect visibleArea)
        {
            return area.width <= 0f || float.IsNaN(area.x) || area.Overlaps(visibleArea);
        }


        // ── Движение холста ──────────────────────────────────────────────

        private void HandleViewMoved()
        {
            if (!isMoving)
            {
                isMoving = true;
                Refresh();
            }

            settle.ExecuteLater(SettleMilliseconds);
        }

        private void Settle()
        {
            isMoving = false;
            settle.Pause();
            Refresh();
        }
    }
}
