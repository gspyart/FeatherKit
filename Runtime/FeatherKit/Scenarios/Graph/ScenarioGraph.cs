using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Данные графа сценария — только то, что сохраняется; исполняет его Scenario.
    /// - ноды, провода, переменные графа, рамки и заметки; id нод не повторяются даже после удаления
    /// - любая правка — через его методы: удаление само чистит провода и рамки
    /// - редактор: до правки Undo.RecordObject, в экземпляре префаба после — PrefabUtility.RecordPrefabInstancePropertyModifications
    /// </summary>
    [Serializable]
    public class ScenarioGraph
    {
        [SerializeReference] private List<ScenarioNode> nodes = new List<ScenarioNode>();
        [SerializeField] private List<ScenarioWire> wires = new List<ScenarioWire>();
        [SerializeField] private List<ScenarioVariableDefinition> variables = new List<ScenarioVariableDefinition>();
        [SerializeField] private List<ScenarioGroup> groups = new List<ScenarioGroup>();
        [SerializeField] private List<ScenarioNote> notes = new List<ScenarioNote>();

        [Tooltip("The id the next new node gets. Only grows, so ids of deleted nodes never come back")]
        [SerializeField] private int nextNodeId = 1;


        public IReadOnlyList<ScenarioNode> Nodes => nodes;

        public IReadOnlyList<ScenarioWire> Wires => wires;

        public IReadOnlyList<ScenarioVariableDefinition> Variables => variables;

        public IReadOnlyList<ScenarioGroup> Groups => groups;

        public IReadOnlyList<ScenarioNote> Notes => notes;


        // ── Поиск ────────────────────────────────────────────────────────

        /// <summary>Нода с этим id или null.</summary>
        public ScenarioNode FindNode(int nodeId)
        {
            foreach (var node in nodes)
            {
                if (node != null && node.Id == nodeId)
                    return node;
            }

            return null;
        }

        /// <summary>Переменная графа с этим id или null.</summary>
        public ScenarioVariableDefinition FindVariable(string variableId)
        {
            foreach (var variable in variables)
            {
                if (variable != null && string.Equals(variable.Id, variableId, StringComparison.Ordinal))
                    return variable;
            }

            return null;
        }


        // ── Ноды ─────────────────────────────────────────────────────────

        /// <summary>Добавить ноду с новым id — и новую, и вставленную из другого графа.</summary>
        public T AddNode<T>(T node, Vector2 position = default) where T : ScenarioNode
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            node.Id = nextNodeId++;
            node.Position = position;
            nodes.Add(node);

            return node;
        }


        /// <summary>Убрать ноду вместе с её проводами и местом в рамках. False — такой ноды нет.</summary>
        public bool RemoveNode(int nodeId)
        {
            var node = FindNode(nodeId);

            if (node == null)
                return false;

            nodes.Remove(node);
            wires.RemoveAll(wire => wire.FromNodeId == nodeId || wire.ToNodeId == nodeId);

            foreach (var group in groups)
                group?.RemoveNode(nodeId);

            return true;
        }


        // ── Провода ──────────────────────────────────────────────────────

        /// <summary>Протянуть провод. Чужая нода или несуществующий порт — ошибка сразу, а не тихий мёртвый провод.</summary>
        public ScenarioWire Connect(ScenarioNode fromNode, ScenarioOutput output, ScenarioNode toNode, ScenarioInput input)
        {
            RequireOwnNode(fromNode);
            RequireOwnNode(toNode);

            if (fromNode.CollectPorts().FindOutput(output.Id) == null)
                throw new ArgumentException($"Node “{fromNode.Title}” has no output “{output.Id}”", nameof(output));

            if (toNode.CollectPorts().FindInput(input.Id) == null)
                throw new ArgumentException($"Node “{toNode.Title}” has no input “{input.Id}”", nameof(input));

            foreach (var existingWire in wires)
            {
                if (existingWire.Connects(fromNode.Id, output.Id, toNode.Id, input.Id))
                    return existingWire;
            }

            var wire = new ScenarioWire(fromNode.Id, output.Id, toNode.Id, input.Id);
            wires.Add(wire);

            return wire;
        }


        public bool Disconnect(ScenarioWire wire)
        {
            return wires.Remove(wire);
        }


        /// <summary>Снять все провода порта ноды — входа или выхода. Вернёт, сколько снято.</summary>
        public int DisconnectPort(int nodeId, string portId)
        {
            return wires.RemoveAll(wire =>
                (wire.FromNodeId == nodeId && string.Equals(wire.FromPortId, portId, StringComparison.Ordinal))
                || (wire.ToNodeId == nodeId && string.Equals(wire.ToPortId, portId, StringComparison.Ordinal)));
        }


        // ── Исходы Wait For ──────────────────────────────────────────────

        /// <summary>Убрать исход ноды Wait For вместе с проводами его выхода. False — нет такого исхода.</summary>
        public bool RemoveWaitCase(ScenarioWaitForNode waitForNode, string caseId)
        {
            RequireOwnNode(waitForNode);

            var waitCase = waitForNode.FindCase(caseId);

            if (waitCase == null)
                return false;

            DisconnectPort(waitForNode.Id, waitCase.Output.Id);

            return waitForNode.RemoveCase(caseId);
        }


        // ── Переменные, рамки, заметки ───────────────────────────────────

        public ScenarioVariableDefinition AddVariable(ScenarioVariableDefinition variable)
        {
            variables.Add(variable);

            return variable;
        }

        /// <summary>Поля нод и привязки с этим id не трогаются: они станут «переменная не найдена».</summary>
        public bool RemoveVariable(string variableId)
        {
            return variables.RemoveAll(variable => variable != null
                                                   && string.Equals(variable.Id, variableId, StringComparison.Ordinal)) > 0;
        }


        public ScenarioGroup AddGroup(ScenarioGroup group)
        {
            groups.Add(group);

            return group;
        }

        /// <summary>Убрать только рамку — ноды остаются на холсте.</summary>
        public bool RemoveGroup(string groupId)
        {
            return groups.RemoveAll(group => group != null && string.Equals(group.Id, groupId, StringComparison.Ordinal)) > 0;
        }


        public ScenarioNote AddNote(ScenarioNote note)
        {
            notes.Add(note);

            return note;
        }

        public bool RemoveNote(string noteId)
        {
            return notes.RemoveAll(note => note != null && string.Equals(note.Id, noteId, StringComparison.Ordinal)) > 0;
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private void RequireOwnNode(ScenarioNode node)
        {
            if (node == null || FindNode(node.Id) != node)
                throw new ArgumentException("The node is not in this graph", nameof(node));
        }
    }
}
