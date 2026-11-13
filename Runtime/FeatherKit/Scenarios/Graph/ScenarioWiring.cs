using System;
using System.Collections.Generic;
using FeatherKit.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Индекс проводов графа «нода + выход → куда идти», собранный один раз при старте сценария.
    /// - провод к пропавшей ноде или порту, а также в ноду, куда войти нельзя, пропускается с предупреждением
    /// </summary>
    public class ScenarioWiring
    {
        private static readonly IReadOnlyList<ScenarioWireTarget> NoTargets = Array.Empty<ScenarioWireTarget>();

        private readonly Dictionary<(int nodeId, string outputId), List<ScenarioWireTarget>> targetsByOutput =
            new Dictionary<(int nodeId, string outputId), List<ScenarioWireTarget>>();

        private readonly List<ScenarioWire> skippedWires = new List<ScenarioWire>();


        /// <param name="logContext">Объект, к которому привязать предупреждения в консоли.</param>
        public ScenarioWiring(ScenarioGraph graph, Object logContext = null)
        {
            if (graph == null)
                return;

            var portsByNode = new Dictionary<ScenarioNode, ScenarioPortList>();

            foreach (var wire in graph.Wires)
            {
                if (wire == null)
                    continue;

                var problem = FindProblem(graph, wire, portsByNode, out var target);

                if (problem != null)
                {
                    skippedWires.Add(wire);
                    FLog.Warning(logContext, ("Scenario wire skipped: ", FLogColor.Bad), (problem, FLogColor.Value));
                    continue;
                }

                var key = (wire.FromNodeId, wire.FromPortId);

                if (!targetsByOutput.TryGetValue(key, out var targets))
                    targetsByOutput[key] = targets = new List<ScenarioWireTarget>();

                targets.Add(target);
            }
        }


        /// <summary>Провода, которые индекс пропустил: вели к пропавшей ноде или порту.</summary>
        public IReadOnlyList<ScenarioWire> SkippedWires => skippedWires;


        /// <summary>Куда ведут провода выхода, по порядку их протягивания. Нет проводов — пустой список.</summary>
        public IReadOnlyList<ScenarioWireTarget> TargetsOf(int nodeId, string outputId)
        {
            return targetsByOutput.TryGetValue((nodeId, outputId), out var targets) ? targets : NoTargets;
        }


        // ── Проверка провода ─────────────────────────────────────────────

        private static string FindProblem(ScenarioGraph graph, ScenarioWire wire,
            Dictionary<ScenarioNode, ScenarioPortList> portsByNode, out ScenarioWireTarget target)
        {
            target = default;

            var fromNode = graph.FindNode(wire.FromNodeId);
            var toNode = graph.FindNode(wire.ToNodeId);

            if (fromNode == null)
                return $"node {wire.FromNodeId} is missing";

            if (toNode == null)
                return $"node {wire.ToNodeId} is missing";

            if (PortsOf(fromNode, portsByNode).FindOutput(wire.FromPortId) == null)
                return $"“{fromNode.Title}” has no output “{wire.FromPortId}”";

            var input = PortsOf(toNode, portsByNode).FindInput(wire.ToPortId);

            if (input == null)
                return $"“{toNode.Title}” has no input “{wire.ToPortId}”";

            if (toNode is not ScenarioStepsNode && toNode is not ScenarioFlowNode)
                return $"“{toNode.Title}” cannot be entered by a wire";

            target = new ScenarioWireTarget(toNode, input);

            return null;
        }

        private static ScenarioPortList PortsOf(ScenarioNode node, Dictionary<ScenarioNode, ScenarioPortList> portsByNode)
        {
            if (!portsByNode.TryGetValue(node, out var ports))
                portsByNode[node] = ports = node.CollectPorts();

            return ports;
        }
    }


    /// <summary>Куда ведёт провод: нода и её вход.</summary>
    public readonly struct ScenarioWireTarget
    {
        public ScenarioWireTarget(ScenarioNode node, ScenarioInput input)
        {
            Node = node;
            Input = input;
        }


        public ScenarioNode Node { get; }

        public ScenarioInput Input { get; }
    }
}
