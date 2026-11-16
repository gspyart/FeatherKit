using System.Collections.Generic;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Правки рамок графа — часть ScenarioGraphEditing: создать из нод, переименовать, перекрасить, сменить состав, удалить.
    /// Нода живёт не больше чем в одной рамке: попав в новую, из старой уходит.
    /// </summary>
    internal class ScenarioGroupEditing
    {
        public const string NewGroupTitle = "Group";

        private readonly ScenarioGraphEditing editing;


        public ScenarioGroupEditing(ScenarioGraphEditing editing)
        {
            this.editing = editing;
        }


        private ScenarioGraph Graph => editing.Host.Graph;


        public ScenarioGroup Add(IEnumerable<int> nodeIds, string title = NewGroupTitle)
        {
            var nodes = nodeIds.Select(Graph.FindNode).Where(node => node != null).ToList();

            return editing.Record("Group Scenario Nodes", () =>
            {
                var group = Graph.AddGroup(new ScenarioGroup(title));

                foreach (var node in nodes)
                    MoveNodeInto(group, node);

                return group;
            });
        }

        public void Rename(string groupId, string title)
        {
            var group = Find(groupId);

            if (group != null && !string.IsNullOrWhiteSpace(title) && group.Title != title.Trim())
                editing.Record("Rename Group", () => group.Title = title.Trim());
        }

        public void SetColor(string groupId, Color color)
        {
            var group = Find(groupId);

            if (group != null && group.Color != color)
                editing.Record("Change Group Color", () => group.Color = color);
        }


        /// <summary>Состав рамки целиком — так его сообщает холст после перетаскивания нод внутрь и наружу.</summary>
        public void SetNodes(string groupId, IEnumerable<int> nodeIds)
        {
            var group = Find(groupId);
            var wanted = new HashSet<int>(nodeIds.Where(nodeId => Graph.FindNode(nodeId) != null));

            if (group == null || wanted.SetEquals(group.NodeIds))
                return;

            editing.Record("Change Group", () =>
            {
                foreach (var nodeId in group.NodeIds.Where(nodeId => !wanted.Contains(nodeId)).ToList())
                    group.RemoveNode(nodeId);

                foreach (var nodeId in wanted)
                    MoveNodeInto(group, Graph.FindNode(nodeId));
            });
        }

        public void Remove(IEnumerable<string> groupIds)
        {
            var existingIds = groupIds.Where(groupId => Find(groupId) != null).ToList();

            if (existingIds.Count == 0)
                return;

            editing.Record("Delete Groups", () =>
            {
                foreach (var groupId in existingIds)
                    Graph.RemoveGroup(groupId);
            });
        }


        /// <summary>Нода в рамку — из прежней рамки она уходит.</summary>
        public void MoveNodeInto(ScenarioGroup group, ScenarioNode node)
        {
            foreach (var other in Graph.Groups)
            {
                if (other != null && other != group)
                    other.RemoveNode(node.Id);
            }

            group.AddNode(node);
        }

        public ScenarioGroup Find(string groupId)
        {
            return Graph.Groups.FirstOrDefault(group => group != null && group.Id == groupId);
        }
    }
}
