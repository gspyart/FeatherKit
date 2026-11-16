using System;
using System.Collections.Generic;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Копировать, вырезать, вставить и дублировать на холсте — часть ScenarioGraphView; Cmd/Ctrl+C, X, V, D.
    /// - ноды с проводами между ними, заметки, рамки; глубокая копия через EditorJsonUtility, у вставленного — новые id
    /// - буфер — системный: вставка работает между окнами и графами; недостающие переменные создаются
    /// - объекты сцены при вставке в граф-файл сбрасываются с предупреждением: файлу их не удержать
    /// </summary>
    internal class ScenarioGraphClipboard
    {
        private const string ClipboardPrefix = "FeatherKit.ScenarioGraph:";
        private const string DuplicateOperation = "Duplicate";
        private static readonly Vector2 DuplicateShift = new Vector2(40f, 40f);

        private readonly ScenarioGraphView graphView;


        public ScenarioGraphClipboard(ScenarioGraphView graphView)
        {
            this.graphView = graphView;

            graphView.serializeGraphElements = Copy;
            graphView.canPasteSerializedData = CanPaste;
            graphView.unserializeAndPaste = PasteFromCanvas;
        }


        // ── Копировать ───────────────────────────────────────────────────

        private string Copy(IEnumerable<GraphElement> elements)
        {
            var host = graphView.Host;

            if (host is not { IsAlive: true })
                return null;

            var selected = elements.ToList();
            var groupIds = selected.OfType<ScenarioGroupView>().Select(groupView => groupView.GroupId).ToList();
            var nodeIds = selected.OfType<ScenarioNodeView>().Select(nodeView => nodeView.NodeId)
                .Concat(groupIds.SelectMany(groupId => host.Graph.Groups.First(group => group.Id == groupId).NodeIds))
                .Distinct();
            var noteIds = selected.OfType<ScenarioNoteView>().Select(noteView => noteView.NoteId);

            var content = Collect(host, nodeIds, noteIds, groupIds);
            var text = ClipboardPrefix + content.ToJson();
            Object.DestroyImmediate(content);

            return text;
        }

        /// <summary>Кусок графа для буфера: ноды, провода между ними, заметки, рамки и переменные, которые ноды читают. Уничтожить после.</summary>
        public static ScenarioClipboardContent Collect(ScenarioGraphHost host, IEnumerable<int> nodeIds, IEnumerable<string> noteIds,
            IEnumerable<string> groupIds)
        {
            var graph = host.Graph;
            var content = ScenarioClipboardContent.Create();
            var copiedIds = new HashSet<int>(nodeIds);

            content.Nodes.AddRange(graph.Nodes.Where(node => node != null && copiedIds.Contains(node.Id)));
            content.Wires.AddRange(graph.Wires.Where(wire => copiedIds.Contains(wire.FromNodeId) && copiedIds.Contains(wire.ToNodeId)));
            content.Notes.AddRange(graph.Notes.Where(note => note != null && noteIds.Contains(note.Id)));
            content.Groups.AddRange(graph.Groups.Where(group => group != null && groupIds.Contains(group.Id)));

            // Id переменной — 32 знака guid: если он есть в тексте нод, нода её читает.
            var nodesJson = string.Concat(content.Nodes.Select(node => EditorJsonUtility.ToJson(node)));
            content.Variables.AddRange(graph.Variables.Where(variable => variable != null && nodesJson.Contains(variable.Id)));
            CollectSceneReferences(host, content);

            return content;
        }


        private static void CollectSceneReferences(ScenarioGraphHost host, ScenarioClipboardContent content)
        {
            host.SerializedGraph.Update();

            for (var index = 0; index < content.Nodes.Count; index++)
                content.SceneReferences.AddRange(ScenarioSceneReferences.Collect(host.NodeProperty(content.Nodes[index].Id), $"nodes.Array.data[{index}]"));

            for (var index = 0; index < content.Variables.Count; index++)
                content.SceneReferences.AddRange(ScenarioSceneReferences.Collect(host.VariableProperty(content.Variables[index].Id),
                    $"variables.Array.data[{index}]"));
        }


        // ── Вставить ─────────────────────────────────────────────────────

        private bool CanPaste(string data)
        {
            return graphView.Editing != null && data != null && data.StartsWith(ClipboardPrefix, StringComparison.Ordinal);
        }

        private void PasteFromCanvas(string operationName, string data)
        {
            var content = ScenarioClipboardContent.FromJson(data.Substring(ClipboardPrefix.Length));

            var origin = content.TopLeft;
            var mousePosition = graphView.contentViewContainer.WorldToLocal(graphView.LocalToWorld(graphView.Navigator.LastMousePosition));
            var shift = operationName == DuplicateOperation ? DuplicateShift : mousePosition - origin;

            var pasted = new List<int>();
            graphView.ApplyEdit(editing => pasted = Paste(editing, content, shift));
            Object.DestroyImmediate(content);
            graphView.SelectAfterRebuild(pasted);
            graphView.Rebuild();
        }

        /// <summary>Вставить кусок в граф одной записью undo со сдвигом. Ноды куска уходят в граф — кусок дважды не вставить. Отдаёт id новых нод.</summary>
        public static List<int> Paste(ScenarioGraphEditing editing, ScenarioClipboardContent content, Vector2 shift)
        {
            var graph = editing.Host.Graph;
            var isGraphFile = editing.Host.IsGraphFile;
            var newIdsByOldId = new Dictionary<int, int>();

            if (ScenarioSceneReferences.Restore(new SerializedObject(content), content.SceneReferences) > 0)
                Debug.LogWarning("Pasted: some scene objects were not found — their scene is closed or they were deleted.", editing.Host.Owner);

            editing.Record("Paste Scenario Nodes", () =>
            {
                AddMissingVariables(graph, content.Variables, isGraphFile);

                foreach (var node in content.Nodes.Where(node => node != null))
                {
                    var oldId = node.Id;
                    newIdsByOldId[oldId] = graph.AddNode(node, ScenarioGraphEditing.RoundPosition(node.Position + shift)).Id;
                }

                ReconnectWires(graph, content.Wires, newIdsByOldId);
                AddNotes(graph, content.Notes, shift);
                AddGroups(editing, content.Groups, newIdsByOldId);
            });

            if (isGraphFile)
                ClearSceneObjects(editing.Host, newIdsByOldId.Values);

            return newIdsByOldId.Values.ToList();
        }

        // Своя переменная графа (тот же id) остаётся своей; чужая заводится заново, имя — без повторов.
        private static void AddMissingVariables(ScenarioGraph graph, List<ScenarioVariableDefinition> variables, bool isGraphFile)
        {
            foreach (var variable in variables.Where(variable => variable != null && graph.FindVariable(variable.Id) == null))
            {
                variable.Name = ScenarioGraphEditing.UniqueVariableName(graph, variable.Name);

                if (isGraphFile)
                    MoveSceneObjectsToBindings(variable);

                graph.AddVariable(variable);
            }
        }

        // Объект сцены граф-файл получает только привязкой: переменная становится открытой, значение по умолчанию — пустым.
        private static void MoveSceneObjectsToBindings(ScenarioVariableDefinition variable)
        {
            if (variable.Kind is ScenarioVariableKind.Object or ScenarioVariableKind.ObjectList && ScenarioBindingsEditing.IsSceneType(variable.ObjectType))
                variable.IsExposed = true;

            var initial = variable.InitialValue;

            if (initial.ObjectValue != null && !EditorUtility.IsPersistent(initial.ObjectValue))
                initial.ObjectValue = null;

            initial.ObjectListValue.RemoveAll(item => item != null && !EditorUtility.IsPersistent(item));
        }

        private static void ReconnectWires(ScenarioGraph graph, List<ScenarioWire> wires, Dictionary<int, int> newIdsByOldId)
        {
            foreach (var wire in wires)
            {
                if (!newIdsByOldId.TryGetValue(wire.FromNodeId, out var fromId) || !newIdsByOldId.TryGetValue(wire.ToNodeId, out var toId))
                    continue;

                var fromNode = graph.FindNode(fromId);
                var toNode = graph.FindNode(toId);
                var output = fromNode.CollectPorts().FindOutput(wire.FromPortId);
                var input = toNode.CollectPorts().FindInput(wire.ToPortId);

                if (output != null && input != null)
                    graph.Connect(fromNode, output, toNode, input);
            }
        }

        private static void AddNotes(ScenarioGraph graph, List<ScenarioNote> notes, Vector2 shift)
        {
            foreach (var note in notes.Where(note => note != null))
            {
                var area = note.Area;
                area.position = ScenarioGraphEditing.RoundPosition(area.position + shift);
                graph.AddNote(new ScenarioNote(note.Text, area) { Color = note.Color });
            }
        }

        private static void AddGroups(ScenarioGraphEditing editing, List<ScenarioGroup> groups, Dictionary<int, int> newIdsByOldId)
        {
            var graph = editing.Host.Graph;

            foreach (var copied in groups.Where(group => group != null))
            {
                var group = graph.AddGroup(new ScenarioGroup(copied.Title) { Color = copied.Color });

                foreach (var oldId in copied.NodeIds)
                {
                    if (newIdsByOldId.TryGetValue(oldId, out var newId))
                        editing.Groups.MoveNodeInto(group, graph.FindNode(newId));
                }
            }
        }

        // Ищем по SerializedObject: так видны поля любых блоков, и игровых тоже.
        private static void ClearSceneObjects(ScenarioGraphHost host, IEnumerable<int> nodeIds)
        {
            var cleared = new List<string>();
            host.SerializedGraph.Update();

            foreach (var nodeId in nodeIds)
            {
                var nodeProperty = host.NodeProperty(nodeId);
                var end = nodeProperty.GetEndProperty();
                var property = nodeProperty.Copy();

                while (property.Next(true) && !SerializedProperty.EqualContents(property, end))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null
                                                                                         || EditorUtility.IsPersistent(property.objectReferenceValue))
                        continue;

                    cleared.Add($"{property.objectReferenceValue.name} ({property.displayName})");
                    property.objectReferenceValue = null;
                }
            }

            host.SerializedGraph.ApplyModifiedPropertiesWithoutUndo();

            if (cleared.Count > 0)
                Debug.LogWarning($"Pasted into a graph file: scene objects can't be kept there and were cleared — {string.Join(", ", cleared)}. "
                                 + "Use a Set on Object variable instead.", host.Owner);
        }
    }
}
