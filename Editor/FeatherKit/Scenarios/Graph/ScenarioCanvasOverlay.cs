using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Подсказки поверх холста — часть ScenarioGraphView: что делать в пустом графе и строка состояния внизу.
    /// - масштаб (клик — 100 %), сколько нод и сколько выделено
    /// - «⚠ N» — ноды и переменные с проблемами; клик ведёт к следующей: к ноде на холсте или к строке панели переменных
    /// - справа — главные клавиши холста
    /// </summary>
    internal class ScenarioCanvasOverlay
    {
        private const string EmptyGraphHint = "Right-click or Space — add a node   ·   C — comment";
        private const string KeysHint = "Space add · C comment · A frame all · F frame selected · Del delete";

        private readonly ScenarioGraphView graphView;
        private readonly Label emptyHint;
        private readonly Button zoomButton;
        private readonly Label countsLabel;
        private readonly Button problemsButton;
        private int lastShownProblemIndex = -1;


        public ScenarioCanvasOverlay(ScenarioGraphView graphView)
        {
            this.graphView = graphView;

            emptyHint = new Label(EmptyGraphHint) { pickingMode = PickingMode.Ignore };
            emptyHint.AddToClassList("scenario-overlay__hint");
            graphView.Add(emptyHint);

            var statusBar = new VisualElement();
            statusBar.AddToClassList("scenario-status-bar");

            zoomButton = StatusButton(ZoomToActualSize, "Zoom — click for 100%");
            countsLabel = new Label();
            countsLabel.AddToClassList("scenario-status-bar__item");
            problemsButton = StatusButton(ShowNextProblem, "Nodes and variables with problems — click to go to the next one");

            var keys = new Label(KeysHint);
            keys.AddToClassList("scenario-status-bar__keys");

            statusBar.Add(zoomButton);
            statusBar.Add(countsLabel);
            statusBar.Add(problemsButton);
            statusBar.Add(keys);
            graphView.Add(statusBar);

            graphView.viewTransformChanged += _ => Refresh();
            graphView.Rebuilt += Refresh;
            graphView.SelectedNodeChanged += _ => Refresh();
        }


        public void Refresh()
        {
            var nodeCount = graphView.nodes.ToList().Count;
            var problemCount = ProblemTargets().Count;

            emptyHint.style.display = graphView.Editing != null && nodeCount == 0 && !graphView.graphElements.Any(element => element is ScenarioNoteView)
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            zoomButton.text = $"{Mathf.RoundToInt(graphView.viewTransform.scale.x * 100f)}%";
            countsLabel.text = $"{nodeCount} nodes · {graphView.selection.Count} selected";

            problemsButton.text = $"⚠ {problemCount}";
            problemsButton.style.display = problemCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }


        // ── Действия ─────────────────────────────────────────────────────

        private void ZoomToActualSize()
        {
            var scale = graphView.viewTransform.scale.x;
            graphView.Navigator.ZoomAround(graphView.layout.center - graphView.layout.position, 1f / scale);
        }

        private void ShowNextProblem()
        {
            var targets = ProblemTargets();

            if (targets.Count == 0)
                return;

            lastShownProblemIndex = (lastShownProblemIndex + 1) % targets.Count;
            var target = targets[lastShownProblemIndex];

            if (target is string variableId)
            {
                graphView.Blackboard.ShowVariable(variableId);
                return;
            }

            var nodeView = graphView.nodes.FirstOrDefault(node => NodeIdOf(node) == (int)target);

            if (nodeView == null)
                return;

            graphView.ClearSelection();
            graphView.AddToSelection(nodeView);
            graphView.Navigator.FrameSelectedOrAll();
        }

        // Ноды (id) и переменные (id строкой) с проблемами. Пропавшие ноды — тоже проблема, хоть проверка графа их и не видит.
        private List<object> ProblemTargets()
        {
            var nodeIds = graphView.Problems.Where(problem => problem.VariableId == null).Select(problem => problem.NodeId)
                .Concat(graphView.nodes.OfType<ScenarioMissingNodeView>().Select(node => node.NodeId))
                .Distinct()
                .Cast<object>();

            var variableIds = graphView.Problems.Where(problem => problem.VariableId != null).Select(problem => problem.VariableId)
                .Distinct();

            return nodeIds.Concat(variableIds).ToList();
        }

        private static int NodeIdOf(Node node)
        {
            return node switch
            {
                ScenarioNodeView scenarioNode => scenarioNode.NodeId,
                ScenarioMissingNodeView missingNode => missingNode.NodeId,
                _ => 0
            };
        }


        private static Button StatusButton(System.Action clicked, string tooltip)
        {
            var button = new Button(clicked) { tooltip = tooltip };
            button.AddToClassList("scenario-status-bar__button");

            return button;
        }
    }
}
