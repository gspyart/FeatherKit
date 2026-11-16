using System.Collections.Generic;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor.Experimental.GraphView;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Правки, сделанные мышью на холсте, — в данные графа: часть ScenarioGraphView.
    /// - сдвиг нод, заметок и рамок с нодами; новый провод; провод, перетащенный на другой порт
    /// - удаление проводов, нод, заметок, рамок и переменных
    /// </summary>
    internal class ScenarioCanvasChanges
    {
        private readonly ScenarioGraphView graphView;


        public ScenarioCanvasChanges(ScenarioGraphView graphView)
        {
            this.graphView = graphView;
            graphView.graphViewChanged = HandleGraphViewChanged;
        }


        private ScenarioGraphEditing Editing => graphView.Editing;


        // ── Изменения GraphView ──────────────────────────────────────────

        private GraphViewChange HandleGraphViewChanged(GraphViewChange change)
        {
            if (graphView.IsRebuilding || Editing == null)
                return change;

            if (change.movedElements != null)
                SaveMovedElements(change.movedElements);

            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            {
                var createdEdges = change.edgesToCreate.ToList();
                change.edgesToCreate.Clear();
                graphView.ApplyEdit(editing => createdEdges.ForEach(edge => ConnectInData(editing, edge.output, edge.input, null)));
            }

            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                // Сдёрнутый с порта провод ждёт выбора в поиске нод — его данные не трогаем.
                // Удалённая рамка уносит с собой ноды — оставляем те, что не выделены сами: удаляют рамку, а не содержимое.
                var keptNodes = KeptNodesOfRemovedGroups(change.elementsToRemove);
                var removedElements = change.elementsToRemove
                    .Where(element => element is not Edge edge || !ScenarioNodeCreation.IsDraggedOff(edge))
                    .Where(element => !keptNodes.Contains(element))
                    .ToList();
                change.elementsToRemove.Clear();
                graphView.ApplyEdit(editing => RemoveInData(editing, removedElements));
            }

            return change;
        }

        private HashSet<GraphElement> KeptNodesOfRemovedGroups(List<GraphElement> removedElements)
        {
            var keptNodes = new HashSet<GraphElement>(removedElements.OfType<ScenarioGroupView>()
                .SelectMany(groupView => groupView.containedElements)
                .Where(element => !graphView.selection.Contains(element)));

            if (keptNodes.Count == 0)
                return keptNodes;

            // Провод остаётся, если его не выделяли и обе его ноды остаются.
            var removedNodes = new HashSet<GraphElement>(removedElements.OfType<Node>().Where(node => !keptNodes.Contains(node)));

            foreach (var edge in removedElements.OfType<Edge>())
            {
                if (!graphView.selection.Contains(edge) && !removedNodes.Contains(edge.output?.node) && !removedNodes.Contains(edge.input?.node))
                    keptNodes.Add(edge);
            }

            return keptNodes;
        }

        private void SaveMovedElements(List<GraphElement> movedElements)
        {
            foreach (var nodeView in movedElements.OfType<ScenarioNodeView>())
                Editing.MoveNode(nodeView.NodeId, ScenarioGraphView.AreaFromStyle(nodeView).position);

            foreach (var noteView in movedElements.OfType<ScenarioNoteView>())
                Editing.SetNoteArea(noteView.NoteId, ScenarioGraphView.AreaFromStyle(noteView));

            // Рамку тащат за шапку: её ноды едут вместе с ней, но в списке сдвинутых их может не быть.
            foreach (var nodeView in movedElements.OfType<ScenarioGroupView>().SelectMany(groupView => groupView.containedElements)
                         .OfType<ScenarioNodeView>().Where(nodeView => !movedElements.Contains(nodeView)))
                Editing.MoveNode(nodeView.NodeId, ScenarioGraphView.AreaFromStyle(nodeView).position);

            graphView.RememberShownData();
        }

        // Провода — первыми: удалённая нода и так снимет свои, а оставшиеся найдутся по ссылке.
        private static void RemoveInData(ScenarioGraphEditing editing, List<GraphElement> removedElements)
        {
            editing.Disconnect(removedElements.OfType<Edge>().Select(edge => edge.userData).OfType<ScenarioWire>());
            editing.RemoveNodes(removedElements.OfType<ScenarioNodeView>().Select(nodeView => nodeView.NodeId));
            editing.RemoveNotes(removedElements.OfType<ScenarioNoteView>().Select(noteView => noteView.NoteId));
            editing.Groups.Remove(removedElements.OfType<ScenarioGroupView>().Select(groupView => groupView.GroupId));

            foreach (var variableField in removedElements.OfType<BlackboardField>())
            {
                if (variableField.userData is string variableId)
                    editing.RemoveVariable(variableId);
            }
        }


        // ── Провода ──────────────────────────────────────────────────────

        /// <summary>Провод отпустили на порт. Старый провод, если его перетащили с другого порта, переезжает.</summary>
        public void ConnectPorts(Port output, Port input, ScenarioWire movedWire)
        {
            graphView.ApplyEdit(editing => ConnectInData(editing, output, input, movedWire));
        }

        private static void ConnectInData(ScenarioGraphEditing editing, Port output, Port input, ScenarioWire movedWire)
        {
            if (output is not ScenarioPortView from || input is not ScenarioPortView to)
                return;

            if (movedWire != null && movedWire.Connects(from.NodeId, from.PortId, to.NodeId, to.PortId))
                return;

            if (movedWire != null)
                editing.Disconnect(new[] { movedWire });

            editing.Connect(from.NodeId, from.PortId, to.NodeId, to.PortId);
        }
    }
}
