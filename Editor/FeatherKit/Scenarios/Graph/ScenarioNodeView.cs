using System.Collections.Generic;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Нода графа на холсте: шапка (ScenarioNodeHeader), порты, поля и стопки блоков прямо на ноде.
    /// - ширина по виду ноды: стопкам и исходам нужно место, нодам потока — нет
    /// - шапка и подписи выходов догоняют правки полей, не пересобирая ноду
    /// </summary>
    internal class ScenarioNodeView : Node
    {
        private const float StackNodeWidth = 320f;
        private const float CasesNodeWidth = 300f;
        private const float FlowNodeWidth = 240f;
        private const string SingleInputId = "in";

        private readonly SerializedProperty nodeProperty;
        private readonly Dictionary<string, ScenarioPortView> outputsById = new Dictionary<string, ScenarioPortView>();


        public ScenarioNodeView(ScenarioGraphView graphView, ScenarioNode node, SerializedProperty nodeProperty)
        {
            Node = node;
            this.nodeProperty = nodeProperty.Copy();
            viewDataKey = $"scenario-node-{node.Id}";

            AddToClassList("scenario-node");
            style.width = WidthOf(node);

            Header = new ScenarioNodeHeader(this, node, this.nodeProperty);
            CreatePorts();

            var fields = new VisualElement();
            fields.AddToClassList("scenario-node__fields");
            ScenarioNodeFields.AddTo(fields, graphView, node, nodeProperty, isInspector: false);
            extensionContainer.Add(fields);
            extensionContainer.AddToClassList("scenario-node__body");

            RefreshCaptions();
            RefreshExpandedState();
            SetPosition(new Rect(node.Position, Vector2.zero));

            this.TrackSerializedObjectValue(nodeProperty.serializedObject, _ => RefreshCaptions());
        }


        public ScenarioNode Node { get; }

        public int NodeId => Node.Id;

        public ScenarioNodeHeader Header { get; }


        // Стопкам When и Do и условиям Guard нужно место под поля блоков; ноде потока — только под порты.
        private static float WidthOf(ScenarioNode node)
        {
            return node switch
            {
                ScenarioWhenNode or ScenarioStepsNode or ScenarioGuardNode => StackNodeWidth,
                ScenarioWaitForNode => CasesNodeWidth,
                _ => FlowNodeWidth
            };
        }


        // ── Порты ────────────────────────────────────────────────────────

        // Единственный вход «In» — точка без подписи: куда входит провод, и так видно.
        private void CreatePorts()
        {
            var ports = Node.CollectPorts();
            var hasSingleInput = ports.Inputs.Count == 1 && ports.Inputs[0].Id == SingleInputId;

            foreach (var input in ports.Inputs)
                inputContainer.Add(new ScenarioPortView(Direction.Input, Node.Id, input.Id, hasSingleInput ? "" : input.Label));

            foreach (var output in ports.Outputs)
            {
                var port = new ScenarioPortView(Direction.Output, Node.Id, output.Id, output.Label);
                outputsById[output.Id] = port;
                outputContainer.Add(port);
            }
        }


        // ── Подписи ──────────────────────────────────────────────────────

        // Объект графа удалили или ноду убрали из данных: холст сейчас пересоберётся.
        private void RefreshCaptions()
        {
            if (nodeProperty.serializedObject.targetObject == null || nodeProperty.managedReferenceValue != Node)
                return;

            Header.Refresh();

            foreach (var output in Node.CollectPorts().Outputs)
            {
                if (outputsById.TryGetValue(output.Id, out var port))
                    port.portName = output.Label;
            }
        }
    }
}
