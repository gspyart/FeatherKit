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
    /// - буфер — системный: вставка работает между окнами и графами; переменная с тем же именем и видом — общая, иначе создаётся
    /// - объект сцены, которого граф не удержит (граф-файл, другая сцена), сбрасывается — заметно, плашкой в окне
    /// - ноды пропавших классов не копируются: о них — тоже плашкой
    /// </summary>
    internal class ScenarioGraphClipboard
    {
        private const string ClipboardPrefix = "FeatherKit.ScenarioGraph:";
        private const string DuplicateOperation = "Duplicate";
        private const double NotificationSeconds = 5d;
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
            WarnAboutMissingNodes(selected.OfType<ScenarioMissingNodeView>().ToList());
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

        // Ноду без класса не прочесть, а значит и не скопировать: сказать, а не терять молча.
        private void WarnAboutMissingNodes(List<ScenarioMissingNodeView> missingNodes)
        {
            if (missingNodes.Count == 0)
                return;

            var message = $"Not copied — nodes of missing classes: {string.Join(", ", missingNodes.Select(node => node.title))}";
            Debug.LogWarning(message, graphView.Host.Owner);
            graphView.Window?.ShowNotification(new GUIContent(message), NotificationSeconds);
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
            var cleared = new List<string>();
            graphView.ApplyEdit(editing => pasted = Paste(editing, content, shift, cleared));
            Object.DestroyImmediate(content);

            if (cleared.Count > 0)
                graphView.Window?.ShowNotification(new GUIContent($"Scene objects this graph can't keep were cleared: {string.Join(", ", cleared.Take(5))}"),
                    NotificationSeconds);

            graphView.SelectAfterRebuild(pasted);
            graphView.Rebuild();
        }

        /// <summary>
        /// Вставить кусок в граф одной записью undo со сдвигом. Ноды куска уходят в граф — кусок дважды не вставить.
        /// Отдаёт id новых нод; в clearedObjects — сброшенные объекты сцены.
        /// </summary>
        public static List<int> Paste(ScenarioGraphEditing editing, ScenarioClipboardContent content, Vector2 shift, List<string> clearedObjects = null)
        {
            var graph = editing.Host.Graph;
            var canKeep = SceneObjectFilterOf(editing.Host);
            var cleared = new List<string>();
            var newIdsByOldId = new Dictionary<int, int>();

            var missingCount = ScenarioSceneReferences.Restore(new SerializedObject(content), content.SceneReferences);

            if (missingCount > 0)
                cleared.Add($"{missingCount} not found — their scene is closed or they were deleted");

            BindToSameVariables(graph, content);

            editing.Record("Paste Scenario Nodes", () =>
            {
                AddMissingVariables(graph, content.Variables, editing.Host.IsGraphFile, canKeep, cleared);

                foreach (var node in content.Nodes.Where(node => node != null))
                {
                    var oldId = node.Id;
                    newIdsByOldId[oldId] = graph.AddNode(node, ScenarioGraphEditing.RoundPosition(node.Position + shift)).Id;
                }

                ReconnectWires(graph, content.Wires, newIdsByOldId);
                AddNotes(graph, content.Notes, shift);
                AddGroups(editing, content.Groups, newIdsByOldId);
            });

            cleared.AddRange(ClearSceneObjects(editing.Host, newIdsByOldId.Values, canKeep));

            if (cleared.Count > 0)
                Debug.LogWarning($"Pasted: scene objects this graph can't keep were cleared — {string.Join(", ", cleared)}. "
                                 + "A graph file gets them through a Set on Object variable; another scene — only by hand.", editing.Host.Owner);

            clearedObjects?.AddRange(cleared);

            return newIdsByOldId.Values.ToList();
        }

        // Граф-файл не держит объектов сцены вовсе, граф объекта — только из своей сцены.
        private static Func<Object, bool> SceneObjectFilterOf(ScenarioGraphHost host)
        {
            if (host.Owner is not Component owner || EditorUtility.IsPersistent(owner))
                return EditorUtility.IsPersistent;

            var ownerScene = owner.gameObject.scene;

            return candidate => EditorUtility.IsPersistent(candidate) || SceneOf(candidate) == ownerScene;
        }

        private static UnityEngine.SceneManagement.Scene SceneOf(Object sceneObject)
        {
            return sceneObject switch
            {
                GameObject gameObject => gameObject.scene,
                Component component => component.gameObject.scene,
                _ => default
            };
        }


        // ── Переменные ───────────────────────────────────────────────────

        // Чужая переменная с тем же именем, видом и типом — та же по смыслу («Lamp» в двух графах): берём её, а не заводим «Lamp 2».
        private static void BindToSameVariables(ScenarioGraph graph, ScenarioClipboardContent content)
        {
            var newIdsByOldId = new Dictionary<string, string>();

            foreach (var variable in content.Variables.Where(variable => variable != null && graph.FindVariable(variable.Id) == null))
            {
                var same = graph.Variables.FirstOrDefault(candidate => candidate != null && candidate.Name == variable.Name
                                                                                       && candidate.Kind == variable.Kind
                                                                                       && candidate.ObjectType == variable.ObjectType);

                if (same != null)
                    newIdsByOldId[variable.Id] = same.Id;
            }

            if (newIdsByOldId.Count == 0)
                return;

            content.Variables.RemoveAll(variable => variable != null && newIdsByOldId.ContainsKey(variable.Id));

            var serializedContent = new SerializedObject(content);
            var property = serializedContent.GetIterator();

            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.String && newIdsByOldId.TryGetValue(property.stringValue, out var newId))
                    property.stringValue = newId;
            }

            serializedContent.ApplyModifiedPropertiesWithoutUndo();
        }

        // Своя переменная графа (тот же id) остаётся своей; чужая заводится заново, имя — без повторов.
        private static void AddMissingVariables(ScenarioGraph graph, List<ScenarioVariableDefinition> variables, bool isGraphFile,
            Func<Object, bool> canKeep, List<string> cleared)
        {
            foreach (var variable in variables.Where(variable => variable != null && graph.FindVariable(variable.Id) == null))
            {
                variable.Name = ScenarioGraphEditing.UniqueVariableName(graph, variable.Name);

                // Объект сцены граф-файл получает только привязкой: переменная сразу открытая.
                if (isGraphFile && variable.Kind is ScenarioVariableKind.Object or ScenarioVariableKind.ObjectList
                                && ScenarioBindingsEditing.IsSceneType(variable.ObjectType))
                    variable.IsExposed = true;

                ClearInitialValue(variable, canKeep, cleared);
                graph.AddVariable(variable);
            }
        }

        private static void ClearInitialValue(ScenarioVariableDefinition variable, Func<Object, bool> canKeep, List<string> cleared)
        {
            var initial = variable.InitialValue;

            if (initial.ObjectValue != null && !canKeep(initial.ObjectValue))
            {
                cleared.Add($"{initial.ObjectValue.name} ({variable.Name})");
                initial.ObjectValue = null;
            }

            foreach (var item in initial.ObjectListValue.Where(item => item != null && !canKeep(item)))
                cleared.Add($"{item.name} ({variable.Name})");

            initial.ObjectListValue.RemoveAll(item => item != null && !canKeep(item));
        }


        // ── Ноды, провода, заметки, рамки ────────────────────────────────

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
        private static List<string> ClearSceneObjects(ScenarioGraphHost host, IEnumerable<int> nodeIds, Func<Object, bool> canKeep)
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
                                                                                         || canKeep(property.objectReferenceValue))
                        continue;

                    cleared.Add($"{property.objectReferenceValue.name} ({property.displayName})");
                    property.objectReferenceValue = null;
                }
            }

            host.SerializedGraph.ApplyModifiedPropertiesWithoutUndo();

            return cleared;
        }
    }
}
