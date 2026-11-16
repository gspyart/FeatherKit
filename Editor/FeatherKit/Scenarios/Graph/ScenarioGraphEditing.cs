using System;
using System.Collections.Generic;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Все правки графа из редактора — через API графа, с undo и пометкой «изменено».
    /// - ноды: создать (в том числе по блоку из поиска), сдвинуть, удалить; провода; исходы Wait For
    /// - переменные графа и заметки на холсте; рамки — частью ScenarioGroupEditing, вставка — ScenarioGraphClipboard
    /// - экземпляр префаба получает свои overrides
    /// Без UI: холст и панели только зовут его, он же под EditMode-тестами.
    /// </summary>
    internal class ScenarioGraphEditing
    {
        private const string NewVariableName = "New Variable";

        public static readonly Vector2 NewNoteSize = new Vector2(240f, 120f);

        private readonly ScenarioGraphHost host;


        public ScenarioGraphEditing(ScenarioGraphHost host)
        {
            this.host = host;
            Groups = new ScenarioGroupEditing(this);
        }


        public ScenarioGroupEditing Groups { get; }

        public ScenarioGraphHost Host => host;

        private ScenarioGraph Graph => host.Graph;


        // ── Ноды ─────────────────────────────────────────────────────────

        /// <summary>Добавить ноду по типу из поиска: тип ноды — сама нода, блок-событие — When с ним, блок-шаг — Do с ним.</summary>
        public ScenarioNode AddNode(Type nodeOrBlockType, Vector2 position)
        {
            var node = CreateNodeFor(nodeOrBlockType);

            return Record("Add Scenario Node", () => Graph.AddNode(node, RoundPosition(position)));
        }

        public static ScenarioNode CreateNodeFor(Type nodeOrBlockType)
        {
            if (typeof(ScenarioNode).IsAssignableFrom(nodeOrBlockType))
                return (ScenarioNode)Activator.CreateInstance(nodeOrBlockType);

            if (typeof(ScenarioEvent).IsAssignableFrom(nodeOrBlockType))
            {
                var when = new ScenarioWhenNode();
                when.AddEvent((ScenarioEvent)Activator.CreateInstance(nodeOrBlockType));
                return when;
            }

            if (typeof(ScenarioAction).IsAssignableFrom(nodeOrBlockType))
            {
                var steps = new ScenarioStepsNode();
                steps.AddStep((ScenarioAction)Activator.CreateInstance(nodeOrBlockType));
                return steps;
            }

            throw new ArgumentException($"{nodeOrBlockType?.Name} is neither a node nor an event or step block", nameof(nodeOrBlockType));
        }


        /// <summary>Позиции — целые: дробные хвосты от зума шумят в git при каждом сдвиге.</summary>
        public void MoveNode(int nodeId, Vector2 position)
        {
            var node = Graph.FindNode(nodeId);
            var rounded = RoundPosition(position);

            if (node != null && node.Position != rounded)
                Record("Move Scenario Node", () => node.Position = rounded);
        }

        public static Vector2 RoundPosition(Vector2 position)
        {
            return new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
        }


        /// <summary>Своя подпись ноды поверх названия; пустая — снова название блока.</summary>
        public void SetNodeTitle(int nodeId, string title)
        {
            var node = Graph.FindNode(nodeId);
            var trimmed = title?.Trim() ?? "";

            if (node != null && (node.CustomTitle ?? "") != trimmed)
                Record("Rename Scenario Node", () => node.CustomTitle = trimmed);
        }


        public void RemoveNodes(IEnumerable<int> nodeIds)
        {
            var existingIds = nodeIds.Where(nodeId => Graph.FindNode(nodeId) != null).ToList();

            if (existingIds.Count == 0)
                return;

            Record("Delete Scenario Nodes", () =>
            {
                foreach (var nodeId in existingIds)
                    Graph.RemoveNode(nodeId);
            });
        }


        /// <summary>Убрать ноду пропавшего класса с проводами. Её объекта нет — удаляется по месту в списке, через SerializedObject.</summary>
        public void RemoveMissingNode(int listIndex, int nodeId)
        {
            if (listIndex < 0 || listIndex >= Graph.Nodes.Count || Graph.Nodes[listIndex] != null)
                return;

            Disconnect(Graph.Wires.Where(wire => wire.FromNodeId == nodeId || wire.ToNodeId == nodeId).ToList());

            var nodes = host.NodesProperty;
            nodes.serializedObject.Update();
            nodes.DeleteArrayElementAtIndex(listIndex);
            nodes.serializedObject.ApplyModifiedProperties();
        }


        // ── Провода ──────────────────────────────────────────────────────

        /// <summary>Протянуть провод. Null — нет такой ноды или порта: холст держал устаревший порт.</summary>
        public ScenarioWire Connect(int fromNodeId, string outputId, int toNodeId, string inputId)
        {
            var fromNode = Graph.FindNode(fromNodeId);
            var toNode = Graph.FindNode(toNodeId);
            var output = fromNode?.CollectPorts().FindOutput(outputId);
            var input = toNode?.CollectPorts().FindInput(inputId);

            if (output == null || input == null)
                return null;

            return Record("Connect Scenario Nodes", () => Graph.Connect(fromNode, output, toNode, input));
        }


        public void Disconnect(IEnumerable<ScenarioWire> wires)
        {
            var existingWires = wires.Where(wire => Graph.Wires.Contains(wire)).ToList();

            if (existingWires.Count == 0)
                return;

            Record("Disconnect Scenario Nodes", () =>
            {
                foreach (var wire in existingWires)
                    Graph.Disconnect(wire);
            });
        }


        // ── Исходы Wait For ──────────────────────────────────────────────

        /// <summary>Новый исход в конце списка: блок-событие или блок-условие.</summary>
        public ScenarioWaitCase AddWaitCase(ScenarioWaitForNode waitForNode, object block)
        {
            var waitCase = block switch
            {
                ScenarioEvent awaitedEvent => new ScenarioWaitCase(awaitedEvent),
                ScenarioCondition condition => new ScenarioWaitCase(condition),
                _ => throw new ArgumentException("A wait case needs an event or a condition block", nameof(block))
            };

            return Record("Add Wait Case", () => waitForNode.AddCase(waitCase));
        }

        public void MoveWaitCase(ScenarioWaitForNode waitForNode, int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex || toIndex < 0 || toIndex >= waitForNode.Cases.Count)
                return;

            Record("Move Wait Case", () => waitForNode.MoveCase(fromIndex, toIndex));
        }

        public void RemoveWaitCase(ScenarioWaitForNode waitForNode, string caseId)
        {
            if (waitForNode.FindCase(caseId) != null)
                Record("Remove Wait Case", () => Graph.RemoveWaitCase(waitForNode, caseId));
        }


        // ── Переменные ───────────────────────────────────────────────────

        public ScenarioVariableDefinition AddVariable(ScenarioVariableKind kind)
        {
            var variable = new ScenarioVariableDefinition(UniqueVariableName(Graph, NewVariableName), kind);

            return Record("Add Scenario Variable", () => Graph.AddVariable(variable));
        }

        public void RenameVariable(string variableId, string newName)
        {
            var variable = Graph.FindVariable(variableId);

            if (variable == null || string.IsNullOrWhiteSpace(newName) || variable.Name == newName.Trim())
                return;

            var uniqueName = UniqueVariableName(Graph, newName.Trim(), variable);
            Record("Rename Scenario Variable", () => variable.Name = uniqueName);
        }

        /// <summary>Имя — то, что видит человек в списках выбора: два одинаковых там не различить.</summary>
        public static string UniqueVariableName(ScenarioGraph graph, string wantedName, ScenarioVariableDefinition renamed = null)
        {
            var takenNames = new HashSet<string>(graph.Variables.Where(variable => variable != null && variable != renamed)
                .Select(variable => variable.Name));

            var name = wantedName;

            for (var number = 2; takenNames.Contains(name); number++)
                name = $"{wantedName} {number}";

            return name;
        }


        public void SetVariableObjectType(string variableId, Type objectType)
        {
            var variable = Graph.FindVariable(variableId);

            if (variable != null)
                Record("Change Variable Type", () => variable.ObjectType = objectType);
        }

        public void RemoveVariable(string variableId)
        {
            if (Graph.FindVariable(variableId) != null)
                Record("Delete Scenario Variable", () => Graph.RemoveVariable(variableId));
        }


        // ── Заметки ──────────────────────────────────────────────────────

        public ScenarioNote AddNote(Vector2 position)
        {
            var note = new ScenarioNote("", new Rect(RoundPosition(position), NewNoteSize));

            return Record("Add Comment", () => Graph.AddNote(note));
        }

        public void SetNoteText(string noteId, string text)
        {
            var note = FindNote(noteId);

            if (note != null && note.Text != text)
                Record("Edit Comment", () => note.Text = text);
        }

        /// <summary>Место и размер — целые, как у нод.</summary>
        public void SetNoteArea(string noteId, Rect area)
        {
            var note = FindNote(noteId);
            var rounded = new Rect(RoundPosition(area.position), RoundPosition(area.size));

            if (note != null && note.Area != rounded)
                Record("Move Comment", () => note.Area = rounded);
        }

        public void SetNoteColor(string noteId, Color color)
        {
            var note = FindNote(noteId);

            if (note != null && note.Color != color)
                Record("Change Comment Color", () => note.Color = color);
        }

        public void RemoveNotes(IEnumerable<string> noteIds)
        {
            var existingIds = noteIds.Where(noteId => FindNote(noteId) != null).ToList();

            if (existingIds.Count == 0)
                return;

            Record("Delete Comments", () =>
            {
                foreach (var noteId in existingIds)
                    Graph.RemoveNote(noteId);
            });
        }

        private ScenarioNote FindNote(string noteId)
        {
            return Graph.Notes.FirstOrDefault(note => note != null && note.Id == noteId);
        }


        // ── Запись правки ────────────────────────────────────────────────

        /// <summary>Правка графа одной записью undo — и для частей: рамок, вставки.</summary>
        public void Record(string undoName, Action change)
        {
            Record(undoName, () =>
            {
                change();
                return true;
            });
        }

        public T Record<T>(string undoName, Func<T> change)
        {
            Undo.RecordObject(host.Owner, undoName);

            var result = change();

            if (PrefabUtility.IsPartOfPrefabInstance(host.Owner))
                PrefabUtility.RecordPrefabInstancePropertyModifications(host.Owner);

            EditorUtility.SetDirty(host.Owner);
            host.SerializedGraph.Update();

            return result;
        }
    }
}
