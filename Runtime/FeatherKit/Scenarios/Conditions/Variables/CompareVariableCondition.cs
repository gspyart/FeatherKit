using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Сравнить переменную графа любого вида со значением или другой переменной того же вида.
    /// Больше-меньше — у Counter и Number; «пусто» — у Object, Object List и Text.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Variables/Compare Variable", Description = "Compare a graph variable with a value or another variable")]
    public class CompareVariableCondition : ScenarioCondition
    {
        [ScenarioLabel("Variable")]
        [SerializeField] private ScenarioVariableChoice variable = new ScenarioVariableChoice();

        [ScenarioLabel("Is")]
        [ScenarioValueKindFrom("variable")]
        [SerializeField] private ScenarioVariableComparison comparison;

        [Tooltip("What to compare with; the field of the chosen variable's kind is used")]
        [ScenarioLabel("Value")]
        [ScenarioValueKindFrom("variable", "otherVariable", ComparisonFieldName = "comparison")]
        [SerializeField] private ScenarioVariableValue value = new ScenarioVariableValue();

        [Tooltip("Compare with this variable instead of the value. Empty — the value is used")]
        [ScenarioLabel("Other Variable")]
        [SerializeField] private ScenarioVariableChoice otherVariable = new ScenarioVariableChoice();


        public ScenarioVariableChoice Variable => variable;

        public ScenarioVariableComparison Comparison
        {
            get => comparison;
            set => comparison = value;
        }

        public ScenarioVariableValue Value => value;

        public ScenarioVariableChoice OtherVariable => otherVariable;

        public override string Summary => variable.IsChosen ? comparison.Symbol() : "no variable chosen";


        protected override bool Check(ScenarioRun run)
        {
            if (!variable.IsChosen)
                throw new InvalidOperationException("No variable chosen");

            var variables = run.Scenario.GraphVariables;
            var current = variables.GetLiveValue(variable.VariableId, out var definition);

            if (comparison.IsEmptinessCheck())
                return ScenarioVariableValues.IsEmpty(definition.Kind, current) == (comparison == ScenarioVariableComparison.IsEmpty);

            var otherDefinition = definition;
            var other = otherVariable.IsChosen ? variables.GetLiveValue(otherVariable.VariableId, out otherDefinition) : value;

            // Counter и Number сравниваются числами — даже друг с другом.
            if (ScenarioVariableValues.TryGetNumber(definition.Kind, current, out var number)
                && ScenarioVariableValues.TryGetNumber(otherDefinition.Kind, other, out var otherNumber))
                return comparison.Holds(number, otherNumber);

            if (otherDefinition.Kind != definition.Kind)
                throw new InvalidOperationException(
                    $"“{definition.Name}” is a {definition.Kind}, “{otherDefinition.Name}” is a {otherDefinition.Kind}: they cannot be compared");

            var areEqual = ScenarioVariableValues.AreEqual(definition.Kind, current, other);

            return comparison switch
            {
                ScenarioVariableComparison.Equal => areEqual,
                ScenarioVariableComparison.NotEqual => !areEqual,
                _ => throw new InvalidOperationException($"“{definition.Name}” is a {definition.Kind}: only = and ≠ compare it")
            };
        }
    }
}
