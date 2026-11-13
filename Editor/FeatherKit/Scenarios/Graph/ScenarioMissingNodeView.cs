using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Нода, чей класс переименован или удалён: красная, с прежним именем класса и своими проводами.
    /// - данные не трогает — Unity хранит их как есть, пока класс не вернут или не добавят [MovedFrom]
    /// - сдвинуть нельзя: у ноды без класса нет объекта; удалить — только из меню, с подтверждением
    /// </summary>
    internal class ScenarioMissingNodeView : Node
    {
        private const float NodeWidth = 260f;

        private static readonly Color MissingColor = new Color(0.75f, 0.15f, 0.15f);
        // Поля верхнего уровня без отступа — их пишет база ScenarioNode, у блоков внутри нод такой тройки нет.
        private static readonly Regex IdPattern = new Regex(@"(?m)^id:\s*(-?\d+)");
        private static readonly Regex PositionPattern = new Regex(@"(?m)^position:\s*\{\s*x:\s*([-0-9.eE+]+),\s*y:\s*([-0-9.eE+]+)");
        private static readonly Regex CustomTitlePattern = new Regex(@"(?m)^customTitle:");

        private readonly HashSet<string> inputIds = new HashSet<string>();
        private readonly HashSet<string> outputIds = new HashSet<string>();
        private readonly ScenarioGraphView graphView;
        private readonly int listIndex;
        private readonly string typeName;


        private ScenarioMissingNodeView(ScenarioGraphView graphView, int listIndex, int nodeId, string typeName, Vector2 position,
            ScenarioGraph graph)
        {
            this.graphView = graphView;
            this.listIndex = listIndex;
            this.typeName = typeName;
            NodeId = nodeId;
            title = "Missing Node";
            style.width = NodeWidth;
            capabilities &= ~(Capabilities.Deletable | Capabilities.Movable | Capabilities.Copiable);
            this.AddManipulator(new ContextualMenuManipulator(BuildMenu));

            titleContainer.style.backgroundColor = MissingColor;
            extensionContainer.Add(new Label($"Class {typeName} was renamed or removed. The node's data is kept: "
                                             + "bring the class back or add [MovedFrom] to it.")
            {
                style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 6f, paddingRight = 6f, paddingTop = 4f, paddingBottom = 4f }
            });

            CreatePortsFromWires(graph);
            RefreshExpandedState();
            SetPosition(new Rect(position, Vector2.zero));
        }


        public int NodeId { get; }


        /// <summary>
        /// Ноды пропавших классов графа. SerializedProperty такой элемент показывает пустым,
        /// поэтому ноды ищутся среди пропавших объектов Unity по полям базы ноды.
        /// </summary>
        public static IEnumerable<ScenarioMissingNodeView> CreateAll(ScenarioGraphView graphView, Object owner, ScenarioGraph graph)
        {
            var emptySlots = Enumerable.Range(0, graph.Nodes.Count).Where(index => graph.Nodes[index] == null).ToList();

            if (emptySlots.Count == 0)
                yield break;

            // Пустое место в списке и пропавший объект — по порядку: ноды добавляются в конец, их объекты получают растущие id.
            var missingNodes = SerializationUtility.GetManagedReferencesWithMissingTypes(owner)
                .Where(missing => IsNodeData(missing.serializedData))
                .OrderBy(missing => missing.referenceId)
                .Take(emptySlots.Count)
                .ToList();

            for (var order = 0; order < missingNodes.Count; order++)
            {
                var missing = missingNodes[order];
                var data = missing.serializedData;
                var idMatch = IdPattern.Match(data);
                var positionMatch = PositionPattern.Match(data);

                var nodeId = int.Parse(idMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                var position = new Vector2(float.Parse(positionMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                    float.Parse(positionMatch.Groups[2].Value, CultureInfo.InvariantCulture));
                var typeName = string.IsNullOrEmpty(missing.namespaceName) ? missing.className : $"{missing.namespaceName}.{missing.className}";

                yield return new ScenarioMissingNodeView(graphView, emptySlots[order], nodeId, typeName, position, graph);
            }
        }

        private static bool IsNodeData(string data)
        {
            return data != null && IdPattern.IsMatch(data) && PositionPattern.IsMatch(data) && CustomTitlePattern.IsMatch(data);
        }


        // ── Удалить всё-таки ─────────────────────────────────────────────

        private void BuildMenu(ContextualMenuPopulateEvent menuEvent)
        {
            menuEvent.menu.AppendAction("Delete Anyway…", _ => DeleteAfterConfirmation());
            menuEvent.StopPropagation();
        }

        private void DeleteAfterConfirmation()
        {
            var isConfirmed = EditorUtility.DisplayDialog("Delete missing node",
                $"The class {typeName} is gone, so its settings can't be shown. Delete the node, its settings and its wires? "
                + "Undo brings them back.", "Delete", "Cancel");

            if (isConfirmed)
                graphView.ApplyEdit(editing => editing.RemoveMissingNode(listIndex, NodeId));
        }


        // ── Порты ────────────────────────────────────────────────────────

        // Портов класса не узнать — показываем те, к которым идут провода: видно, с чем нода была связана.
        private void CreatePortsFromWires(ScenarioGraph graph)
        {
            foreach (var wire in graph.Wires)
            {
                if (wire.ToNodeId == NodeId && inputIds.Add(wire.ToPortId))
                    AddPort(inputContainer, Direction.Input, wire.ToPortId);

                if (wire.FromNodeId == NodeId && outputIds.Add(wire.FromPortId))
                    AddPort(outputContainer, Direction.Output, wire.FromPortId);
            }
        }

        private void AddPort(VisualElement container, Direction direction, string portId)
        {
            var port = new ScenarioPortView(direction, NodeId, portId, portId);
            port.SetEnabled(false);
            container.Add(port);
        }
    }
}
