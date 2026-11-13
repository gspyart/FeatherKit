using System.Collections.Generic;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Шапка ноды на холсте — часть ScenarioNodeView: иконка, заголовок, под ним сводка мелким, цвет категории.
    /// - стопка из одного блока подписана самим блоком («Flicker»), из нескольких — «3 steps»
    /// - вид ноды и сводка — в подсказке; проблемы графа — значком справа
    /// </summary>
    internal class ScenarioNodeHeader
    {
        private const string StepsPath = "steps.actions";
        private const string EventsPath = "events";
        private const float StripeWidth = 3f;

        private static readonly Dictionary<System.Type, string> KindHintsByNodeType = new Dictionary<System.Type, string>
        {
            { typeof(ScenarioWhenNode), "When — starts the graph when one of its events fires and checks the conditions" },
            { typeof(ScenarioStepsNode), "Do — runs its steps top to bottom, then goes to Done" },
            { typeof(ScenarioWaitForNode), "Wait For — waits for the first of its cases, or times out" },
            { typeof(ScenarioGuardNode), "Guard While — runs Body while the conditions hold" }
        };

        private readonly Node nodeView;
        private readonly ScenarioNode node;
        private readonly SerializedProperty nodeProperty;
        private readonly Image icon;
        private readonly Label summaryLabel;
        private IconBadge problemBadge;


        public ScenarioNodeHeader(Node nodeView, ScenarioNode node, SerializedProperty nodeProperty)
        {
            this.nodeView = nodeView;
            this.node = node;
            this.nodeProperty = nodeProperty;

            var header = nodeView.titleContainer;
            header.AddToClassList("scenario-node__header");
            header.Q("title-button-container")?.RemoveFromHierarchy();

            icon = new Image { scaleMode = ScaleMode.ScaleToFit };
            icon.AddToClassList("scenario-node__icon");

            var titles = new VisualElement();
            titles.AddToClassList("scenario-node__titles");

            var titleLabel = header.Q<Label>("title-label");
            titles.Add(titleLabel);

            summaryLabel = new Label();
            summaryLabel.AddToClassList("scenario-node__summary");
            titles.Add(summaryLabel);

            header.Insert(0, icon);
            header.Insert(1, titles);
        }


        // ── Подписи ──────────────────────────────────────────────────────

        public void Refresh()
        {
            var blocks = StackBlocks();
            var singleBlock = blocks.Count == 1 ? blocks[0] : null;
            var hasCustomTitle = !string.IsNullOrEmpty(node.CustomTitle);

            nodeView.title = hasCustomTitle || singleBlock == null ? node.Title : ScenarioBlockCatalog.TitleOf(singleBlock.GetType());
            summaryLabel.text = SummaryOf(blocks, singleBlock, hasCustomTitle);
            summaryLabel.style.display = string.IsNullOrEmpty(summaryLabel.text) ? DisplayStyle.None : DisplayStyle.Flex;

            var styleType = singleBlock?.GetType() ?? node.GetType();
            icon.image = ScenarioIcons.IconOf(styleType);

            var color = ScenarioBlockStyle.ColorOf(styleType);
            nodeView.mainContainer.style.borderLeftColor = color;
            nodeView.mainContainer.style.borderLeftWidth = StripeWidth;
            nodeView.titleContainer.style.borderBottomColor = color;

            nodeView.titleContainer.tooltip = KindHintOf(node) + (string.IsNullOrEmpty(summaryLabel.text) ? "" : $"\n{summaryLabel.text}");
        }

        private string SummaryOf(List<object> blocks, object singleBlock, bool hasCustomTitle)
        {
            if (!string.IsNullOrEmpty(node.Summary))
                return node.Summary;

            if (singleBlock != null)
            {
                var blockSummary = BlockSummaryOf(singleBlock);
                return hasCustomTitle ? JoinNonEmpty(ScenarioBlockCatalog.TitleOf(singleBlock.GetType()), blockSummary) : blockSummary;
            }

            return node switch
            {
                ScenarioStepsNode => blocks.Count == 0 ? "no steps" : CountOf(blocks.Count, "step"),
                ScenarioWhenNode => blocks.Count == 0 ? "no events — runs only when called" : CountOf(blocks.Count, "event"),
                ScenarioWaitForNode waitFor => JoinNonEmpty(CountOf(waitFor.Cases.Count, "case"),
                    waitFor.Timeout > 0f ? $"timeout {waitFor.Timeout:0.##} s" : ""),
                _ => null
            };
        }

        private static string BlockSummaryOf(object block)
        {
            return block switch
            {
                ScenarioEvent scenarioEvent => scenarioEvent.Summary,
                ScenarioAction { WaitsForEnd: false } action => JoinNonEmpty(action.Summary, "in background"),
                ScenarioAction action => action.Summary,
                _ => null
            };
        }

        /// <summary>Блоки стопки When или Do; у остальных нод стопки в шапке не участвуют.</summary>
        private List<object> StackBlocks()
        {
            var path = node switch
            {
                ScenarioStepsNode => StepsPath,
                ScenarioWhenNode => EventsPath,
                _ => null
            };

            var blocks = new List<object>();
            var array = path != null ? nodeProperty.FindPropertyRelative(path) : null;

            if (array is not { isArray: true })
                return blocks;

            for (var index = 0; index < array.arraySize; index++)
            {
                var block = array.GetArrayElementAtIndex(index).managedReferenceValue;

                if (block != null)
                    blocks.Add(block);
            }

            return blocks;
        }

        private static string KindHintOf(ScenarioNode node)
        {
            return KindHintsByNodeType.TryGetValue(node.GetType(), out var hint) ? hint : node.DefaultTitle;
        }

        private static string CountOf(int count, string noun)
        {
            return count == 1 ? $"1 {noun}" : $"{count} {noun}s";
        }

        private static string JoinNonEmpty(string first, string second)
        {
            if (string.IsNullOrEmpty(first))
                return second;

            return string.IsNullOrEmpty(second) ? first : $"{first} · {second}";
        }


        // ── Проблемы ─────────────────────────────────────────────────────

        /// <summary>Значок справа в шапке: ошибка — красный, только предупреждения — серый. Пусто — значка нет.</summary>
        public void ShowProblems(IReadOnlyList<ScenarioGraphProblem> problems)
        {
            problemBadge?.Detach();
            problemBadge?.RemoveFromHierarchy();
            problemBadge = null;

            if (problems == null || problems.Count == 0)
                return;

            var text = string.Join("\n", problems.Select(problem => problem.Message));
            problemBadge = problems.Any(problem => problem.IsError) ? IconBadge.CreateError(text) : IconBadge.CreateComment(text);

            nodeView.Add(problemBadge);
            problemBadge.AttachTo(nodeView.titleContainer, SpriteAlignment.RightCenter);
        }
    }
}
