using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Миникарта в правом нижнем углу холста — часть ScenarioGraphView; включается кнопкой в шапке окна.
    /// Включена или нет — помнится для всех окон графа.
    /// </summary>
    internal class ScenarioCanvasMiniMap
    {
        private const string ShownPreferenceKey = "FeatherKit.ScenarioGraph.MiniMapShown";
        private const float Margin = 10f;

        private static readonly Vector2 Size = new Vector2(200f, 140f);

        private readonly ScenarioGraphView graphView;
        private readonly MiniMap miniMap;


        public ScenarioCanvasMiniMap(ScenarioGraphView graphView)
        {
            this.graphView = graphView;

            miniMap = new MiniMap { anchored = true };
            miniMap.AddToClassList("scenario-minimap");
            graphView.Add(miniMap);

            graphView.RegisterCallback<GeometryChangedEvent>(_ => PlaceInCorner());
            Apply();
        }


        public bool IsShown
        {
            get => EditorPrefs.GetBool(ShownPreferenceKey, false);
            set
            {
                EditorPrefs.SetBool(ShownPreferenceKey, value);
                Apply();
            }
        }


        private void Apply()
        {
            miniMap.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
            PlaceInCorner();
        }

        private void PlaceInCorner()
        {
            var area = graphView.layout;

            if (float.IsNaN(area.width))
                return;

            miniMap.SetPosition(new Rect(area.width - Size.x - Margin, area.height - Size.y - Margin, Size.x, Size.y));
        }
    }
}
