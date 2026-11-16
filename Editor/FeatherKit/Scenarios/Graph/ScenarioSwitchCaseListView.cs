using System;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Случаи ноды Switch on Counter: значение счётчика и свой выход у каждого — на ноде и в инспекторе ноды.
    /// Добавить, убрать, переставить — через ScenarioGraphEditing: провода выхода уходят вместе со случаем.
    /// </summary>
    internal class ScenarioSwitchCaseListView : VisualElement
    {
        private const string ValuePropertyName = "value";

        private readonly ScenarioGraphView graphView;
        private readonly ScenarioSwitchOnCounterNode switchNode;


        public ScenarioSwitchCaseListView(ScenarioGraphView graphView, ScenarioSwitchOnCounterNode switchNode, SerializedProperty cases)
        {
            this.graphView = graphView;
            this.switchNode = switchNode;

            AddToClassList("scenario-wait-cases");
            Add(new Label("Cases") { name = "cases-title" });

            for (var index = 0; index < cases.arraySize && index < switchNode.Cases.Count; index++)
                Add(CreateCaseRow(index, switchNode.Cases[index], cases.GetArrayElementAtIndex(index)));

            if (cases.arraySize == 0)
                Add(new Label("No cases — always goes to Default") { name = "cases-empty" });

            Add(new Button(AddCase) { text = "+ Case", tooltip = "A new case with the next free value" });
        }


        private VisualElement CreateCaseRow(int index, ScenarioSwitchCase switchCase, SerializedProperty caseProperty)
        {
            var row = new VisualElement();
            row.AddToClassList("scenario-wait-case__header");

            var value = new IntegerField { isDelayed = true, tooltip = "The counter value that goes to this case's output" };
            value.BindProperty(caseProperty.FindPropertyRelative(ValuePropertyName));
            value.style.flexGrow = 1f;
            row.Add(value);

            row.Add(SmallButton("▲", "Move up: with a repeated value the upper case wins",
                () => graphView.ApplyEdit(editing => editing.MoveSwitchCase(switchNode, index, index - 1))));
            row.Add(SmallButton("▼", "Move down", () => graphView.ApplyEdit(editing => editing.MoveSwitchCase(switchNode, index, index + 1))));

            if (switchCase != null)
                row.Add(SmallButton("✕", "Remove the case together with its output wires",
                    () => graphView.ApplyEdit(editing => editing.RemoveSwitchCase(switchNode, switchCase.Id))));

            return row;
        }

        // Следующее за самым большим: новый случай не повторяет уже заданный.
        private void AddCase()
        {
            var nextValue = 0;

            foreach (var switchCase in switchNode.Cases)
            {
                if (switchCase != null && switchCase.Value >= nextValue)
                    nextValue = switchCase.Value + 1;
            }

            graphView.ApplyEdit(editing => editing.AddSwitchCase(switchNode, nextValue));
        }


        private static Button SmallButton(string text, string tooltip, Action clicked)
        {
            var button = new Button(clicked) { text = text, tooltip = tooltip };
            button.AddToClassList("scenario-wait-case__button");

            return button;
        }
    }
}
