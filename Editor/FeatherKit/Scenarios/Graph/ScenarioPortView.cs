using System.Collections.Generic;
using FeatherKit.Scenarios;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Порт ноды на холсте: знает id своей ноды и порта — по ним холст тянет провод в данных.
    /// - подсказка, куда ведёт выход; «плохие» выходы (Not Met, Broken, Timed Out) — тёплого цвета
    /// - провод, брошенный в пустоту, открывает поиск нод; перетащенный на другой порт — переезжает
    /// </summary>
    internal class ScenarioPortView : Port
    {
        private const string CaseOutputHint = "this case happened first";

        private static readonly Color FailureOutputColor = new Color(0.93f, 0.55f, 0.3f);

        private static readonly Dictionary<string, string> HintsByPortId = new Dictionary<string, string>
        {
            { "in", "In — starts this node" },
            { "then", "Then — an event fired and the conditions are met" },
            { "notMet", "Not Met — an event fired, but the conditions are not met" },
            { "done", "Done — everything in this node is finished" },
            { "body", "Body — runs while the conditions hold" },
            { "broken", "Broken — the conditions stopped holding; Body was stopped" },
            { "timedOut", "Timed Out — none of the cases happened in time" }
        };

        private static readonly HashSet<string> FailureOutputIds = new HashSet<string> { "notMet", "broken", "timedOut" };


        public ScenarioPortView(Direction direction, int nodeId, string portId, string label)
            : base(Orientation.Horizontal, direction, Capacity.Multi, typeof(ScenarioWire))
        {
            NodeId = nodeId;
            PortId = portId;
            portName = label;
            tooltip = HintOf(portId, label);

            if (FailureOutputIds.Contains(portId))
                portColor = FailureOutputColor;

            m_EdgeConnector = new EdgeConnector<Edge>(new ScenarioWireDropListener());
            this.AddManipulator(m_EdgeConnector);
        }


        public int NodeId { get; }

        public string PortId { get; }


        private static string HintOf(string portId, string label)
        {
            if (HintsByPortId.TryGetValue(portId, out var hint))
                return hint;

            return portId.StartsWith(ScenarioWaitCase.OutputIdPrefix) ? $"{label} — {CaseOutputHint}" : label;
        }


        /// <summary>Куда отпустили провод: на порт — соединить в данных, в пустоту — поиск нод.</summary>
        private class ScenarioWireDropListener : IEdgeConnectorListener
        {
            // IEdgeConnectorListener
            public void OnDrop(GraphView graphView, Edge edge)
            {
                if (graphView is ScenarioGraphView scenarioGraphView)
                    scenarioGraphView.ConnectPorts(edge.output, edge.input, edge.userData as ScenarioWire);
            }

            // IEdgeConnectorListener
            public void OnDropOutsidePort(Edge edge, Vector2 position)
            {
                var draggedPort = edge.output ?? edge.input;

                draggedPort?.GetFirstAncestorOfType<ScenarioGraphView>()?.NodeCreation.HandleWireDroppedOutside(edge, position);
            }
        }
    }
}
