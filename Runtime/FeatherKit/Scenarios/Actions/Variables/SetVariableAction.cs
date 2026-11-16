using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Записать в переменную графа любого вида значение или значение другой переменной того же вида.</summary>
    [Serializable]
    [ScenarioBlock("Variables/Set Variable", Description = "Put a value or another variable's value into a graph variable")]
    public class SetVariableAction : ScenarioInstantAction
    {
        [ScenarioLabel("Variable")]
        [SerializeField] private ScenarioVariableChoice variable = new ScenarioVariableChoice();

        [Tooltip("The value to put; the field of the chosen variable's kind is used")]
        [ScenarioLabel("Value")]
        [SerializeField] private ScenarioVariableValue value = new ScenarioVariableValue();

        [Tooltip("Copy from this variable instead of the value. Empty — the value is used")]
        [ScenarioLabel("From Variable")]
        [SerializeField] private ScenarioVariableChoice sourceVariable = new ScenarioVariableChoice();


        public ScenarioVariableChoice Variable => variable;

        public ScenarioVariableValue Value => value;

        public ScenarioVariableChoice SourceVariable => sourceVariable;

        public override string Summary => !variable.IsChosen ? "no variable chosen" : sourceVariable.IsChosen ? "from variable" : null;


        protected override void Apply(ScenarioRun run)
        {
            if (!variable.IsChosen)
                throw new InvalidOperationException("No variable chosen");

            var variables = run.Scenario.GraphVariables;

            if (!sourceVariable.IsChosen)
            {
                variables.Assign(variable.VariableId, value);
                return;
            }

            var source = variables.GetLiveValue(sourceVariable.VariableId, out var sourceDefinition);
            variables.GetLiveValue(variable.VariableId, out var targetDefinition);

            if (sourceDefinition.Kind != targetDefinition.Kind)
                throw new InvalidOperationException(
                    $"“{sourceDefinition.Name}” is a {sourceDefinition.Kind}, “{targetDefinition.Name}” is a {targetDefinition.Kind}");

            variables.Assign(variable.VariableId, source);
        }
    }
}
