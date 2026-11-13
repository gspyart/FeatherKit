using System;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Исходы ноды Wait For: у каждого подпись, блок-событие или блок-условие и свой выход на ноде.
    /// Добавить, убрать, переставить — через ScenarioGraphEditing: провода выхода уходят вместе с исходом.
    /// </summary>
    internal class ScenarioWaitCaseListView : VisualElement
    {
        private const string CasesPropertyName = "cases";
        private const string LabelPropertyName = "label";
        private const string EventPropertyName = "awaitedEvent";
        private const string ConditionPropertyName = "condition";

        private readonly ScenarioGraphView graphView;
        private readonly ScenarioWaitForNode waitForNode;


        public ScenarioWaitCaseListView(ScenarioGraphView graphView, ScenarioWaitForNode waitForNode, SerializedProperty cases)
        {
            this.graphView = graphView;
            this.waitForNode = waitForNode;

            AddToClassList("scenario-wait-cases");
            Add(new Label("Wait For One Of") { name = "cases-title" });

            for (var index = 0; index < cases.arraySize && index < waitForNode.Cases.Count; index++)
                Add(CreateCaseRow(index, waitForNode.Cases[index], cases.GetArrayElementAtIndex(index)));

            if (cases.arraySize == 0)
                Add(new Label("No cases — the node waits only for its timeout") { name = "cases-empty" });

            Add(CreateAddButtons());
        }


        // ── Исход ────────────────────────────────────────────────────────

        private VisualElement CreateCaseRow(int index, ScenarioWaitCase waitCase, SerializedProperty caseProperty)
        {
            var row = new VisualElement();
            row.AddToClassList("scenario-wait-case");

            row.Add(CreateCaseHeader(index, waitCase));

            var label = new PropertyField(caseProperty.FindPropertyRelative(LabelPropertyName), "Output Label");
            label.BindProperty(caseProperty.FindPropertyRelative(LabelPropertyName));
            row.Add(label);

            var blockProperty = caseProperty.FindPropertyRelative(waitCase is { IsEvent: true } ? EventPropertyName : ConditionPropertyName);
            var block = (object)waitCase?.AwaitedEvent ?? waitCase?.Condition;

            if (block == null)
            {
                row.Add(new Label("Block lost: its class was renamed or removed") { name = "case-lost" });
                return row;
            }

            var blockField = new PropertyField(blockProperty, BlockLabelOf(block));
            blockField.BindProperty(blockProperty);
            blockField.TrackPropertyValue(blockProperty, _ => blockField.label = BlockLabelOf(block));
            row.Add(blockField);

            return row;
        }

        // Сводка рядом с именем — «Chance · 50 %»: свёрнутый исход понятен без раскрытия.
        private static string BlockLabelOf(object block)
        {
            var title = ScenarioBlockCatalog.TitleOf(block.GetType());
            var summary = block switch
            {
                ScenarioEvent scenarioEvent => scenarioEvent.Summary,
                ScenarioCondition condition => condition.Summary,
                _ => null
            };

            return string.IsNullOrEmpty(summary) ? title : $"{title} · {summary}";
        }

        private VisualElement CreateCaseHeader(int index, ScenarioWaitCase waitCase)
        {
            var header = new VisualElement();
            header.AddToClassList("scenario-wait-case__header");
            var kind = waitCase == null ? "Lost" : waitCase.IsEvent ? "Event" : "Condition";

            header.Add(new Label($"{index + 1}. {kind}"));
            header.Add(SmallButton("▲", "Move up: the upper case wins when two happen in one frame",
                () => graphView.ApplyEdit(editing => editing.MoveWaitCase(waitForNode, index, index - 1))));
            header.Add(SmallButton("▼", "Move down",
                () => graphView.ApplyEdit(editing => editing.MoveWaitCase(waitForNode, index, index + 1))));

            if (waitCase != null)
                header.Add(SmallButton("✕", "Remove the case together with its output wires",
                    () => graphView.ApplyEdit(editing => editing.RemoveWaitCase(waitForNode, waitCase.Id))));

            return header;
        }


        // ── Добавить ─────────────────────────────────────────────────────

        private VisualElement CreateAddButtons()
        {
            var buttons = new VisualElement();
            buttons.AddToClassList("scenario-wait-cases__add");
            Button addEvent = null;
            Button addCondition = null;

            addEvent = new Button(() => ShowBlockPicker(typeof(ScenarioEvent), addEvent)) { text = "+ Event" };
            addCondition = new Button(() => ShowBlockPicker(typeof(ScenarioCondition), addCondition)) { text = "+ Condition" };

            buttons.Add(addEvent);
            buttons.Add(addCondition);

            return buttons;
        }

        private void ShowBlockPicker(Type blockBaseType, VisualElement button)
        {
            new ScenarioBlockPicker(blockBaseType, blockType => graphView.ApplyEdit(editing =>
                {
                    var waitCase = editing.AddWaitCase(waitForNode, Activator.CreateInstance(blockType));
                    ExpandBlockOf(waitCase);
                }))
                .Show(button.worldBound);
        }

        // Новый исход сразу раскрыт: его только что добавили, чтобы настроить.
        private void ExpandBlockOf(ScenarioWaitCase waitCase)
        {
            var cases = graphView.Host.NodeProperty(waitForNode.Id)?.FindPropertyRelative(CasesPropertyName);
            var index = waitForNode.Cases.ToList().IndexOf(waitCase);

            if (cases == null || index < 0 || index >= cases.arraySize)
                return;

            cases.GetArrayElementAtIndex(index).FindPropertyRelative(waitCase.IsEvent ? EventPropertyName : ConditionPropertyName).isExpanded = true;
        }


        private static Button SmallButton(string text, string tooltip, Action clicked)
        {
            var button = new Button(clicked) { text = text, tooltip = tooltip };
            button.AddToClassList("scenario-wait-case__button");

            return button;
        }
    }
}
