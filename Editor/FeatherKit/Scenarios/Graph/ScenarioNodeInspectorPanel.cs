using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Инспектор выделенной ноды справа от холста: подпись и все поля ноды, в том числе [ScenarioInspectorOnly].
    /// - шире ноды — удобнее править длинные стопки блоков и исходы
    /// - пересборка холста не сбрасывает прокрутку, если нода та же
    /// </summary>
    internal class ScenarioNodeInspectorPanel : VisualElement
    {
        private const string CustomTitlePropertyName = "customTitle";

        private readonly ScrollView content;
        private int shownNodeId;


        public ScenarioNodeInspectorPanel()
        {
            style.flexGrow = 1f;
            style.minWidth = 220f;

            Add(new Label("Node") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13, paddingLeft = 6f, paddingTop = 4f, paddingBottom = 4f } });

            content = new ScrollView { style = { flexGrow = 1f, paddingLeft = 6f, paddingRight = 6f } };
            Add(content);

            ShowNothing();
        }


        public void Show(ScenarioGraphView graphView, ScenarioNodeView nodeView)
        {
            if (nodeView == null || graphView.Host is not { IsAlive: true })
            {
                ShowNothing();
                return;
            }

            var nodeProperty = graphView.Host.NodeProperty(nodeView.NodeId);

            if (nodeProperty == null)
            {
                ShowNothing();
                return;
            }

            var keptScroll = nodeView.NodeId == shownNodeId ? content.scrollOffset : Vector2.zero;
            shownNodeId = nodeView.NodeId;

            content.Clear();
            content.Add(new Label($"{nodeView.Node.DefaultTitle} · id {nodeView.NodeId}") { style = { opacity = 0.7f, marginBottom = 4f } });

            var customTitle = nodeProperty.FindPropertyRelative(CustomTitlePropertyName);
            var titleField = new PropertyField(customTitle, "Caption");
            titleField.BindProperty(customTitle);
            content.Add(titleField);

            ScenarioNodeFields.AddTo(content, graphView, nodeView.Node, nodeProperty, isInspector: true);

            // Высота содержимого известна только после раскладки — прокрутку возвращаем кадром позже.
            content.schedule.Execute(() => content.scrollOffset = keptScroll);
        }


        private void ShowNothing()
        {
            shownNodeId = 0;
            content.Clear();
            content.Add(new Label("Select one node to see all its settings.") { style = { opacity = 0.6f, whiteSpace = WhiteSpace.Normal } });
        }
    }
}
