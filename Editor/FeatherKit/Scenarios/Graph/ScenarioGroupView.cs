using System.Collections.Generic;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Рамка на холсте вокруг нескольких нод с заголовком — чтобы читать граф кусками.
    /// - размер — по нодам внутри; тащишь рамку — едут и ноды
    /// - двойной клик по заголовку переименовывает, цвет — из контекстного меню
    /// - нода, брошенная в рамку или вытащенная из неё с Shift, меняет её состав в данных
    /// </summary>
    internal class ScenarioGroupView : Group
    {
        private const float BackgroundAlpha = 0.18f;

        private static readonly (string name, Color color)[] Palette =
        {
            ("Gray", new Color(0.5f, 0.5f, 0.5f)),
            ("Red", new Color(0.85f, 0.3f, 0.3f)),
            ("Orange", new Color(0.9f, 0.55f, 0.2f)),
            ("Yellow", new Color(0.9f, 0.8f, 0.25f)),
            ("Green", new Color(0.3f, 0.75f, 0.35f)),
            ("Blue", new Color(0.3f, 0.55f, 0.9f)),
            ("Purple", new Color(0.6f, 0.4f, 0.85f))
        };

        private readonly ScenarioGraphView graphView;
        private bool isFilling;


        public ScenarioGroupView(ScenarioGraphView graphView, ScenarioGroup group)
        {
            this.graphView = graphView;
            GroupId = group.Id;
            title = group.Title;
            viewDataKey = $"scenario-group-{group.Id}";

            AddToClassList("scenario-group");
            ApplyColor(group.Color);
            this.AddManipulator(new ContextualMenuManipulator(BuildMenu));
        }


        public string GroupId { get; }


        /// <summary>Ноды рамки при пересборке — без записи в данные: они оттуда и взяты.</summary>
        public void Fill(IEnumerable<GraphElement> nodeViews)
        {
            isFilling = true;
            AddElements(nodeViews);
            isFilling = false;
        }

        private void ApplyColor(Color color)
        {
            var background = color;
            background.a = BackgroundAlpha;
            style.backgroundColor = background;

            var border = color;
            border.a = 1f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = border;
        }


        // ── Правки с холста ──────────────────────────────────────────────

        protected override void OnElementsAdded(IEnumerable<GraphElement> elements)
        {
            base.OnElementsAdded(elements);
            SaveNodes();
        }

        protected override void OnElementsRemoved(IEnumerable<GraphElement> elements)
        {
            base.OnElementsRemoved(elements);
            SaveNodes();
        }

        // Состав рамки меняют и пересборка, и удаление ноды: пишем, только когда двигал человек.
        private void SaveNodes()
        {
            if (isFilling || graphView.IsRebuilding || graphView.Editing == null)
                return;

            var nodeIds = containedElements.OfType<ScenarioNodeView>().Select(nodeView => nodeView.NodeId).ToList();
            graphView.Editing.Groups.SetNodes(GroupId, nodeIds);
            graphView.RememberShownData();
        }

        protected override void OnGroupRenamed(string oldName, string newName)
        {
            base.OnGroupRenamed(oldName, newName);

            if (!graphView.IsRebuilding)
                graphView.ApplyEdit(editing => editing.Groups.Rename(GroupId, newName));
        }


        private void BuildMenu(ContextualMenuPopulateEvent menuEvent)
        {
            foreach (var (name, color) in Palette)
            {
                var chosen = color;
                menuEvent.menu.AppendAction($"Color/{name}", _ => graphView.ApplyEdit(editing => editing.Groups.SetColor(GroupId, chosen)));
            }

            menuEvent.menu.AppendAction("Ungroup", _ => graphView.ApplyEdit(editing => editing.Groups.Remove(new[] { GroupId })));
            menuEvent.menu.AppendSeparator();
        }
    }
}
