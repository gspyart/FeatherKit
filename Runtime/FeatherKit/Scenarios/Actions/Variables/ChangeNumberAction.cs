using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Изменить число в переменной Counter или Number: прибавить, отнять, умножить, поделить или взять случайное.
    /// Counter округляется до целого; деление на ноль — ошибка.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Variables/Change Number", Description = "Add, subtract, multiply, divide or roll a graph Counter or Number")]
    public class ChangeNumberAction : ScenarioInstantAction
    {
        [ScenarioLabel("Variable")]
        [ScenarioAllowedVariableKinds(ScenarioVariableKind.Counter, ScenarioVariableKind.Number)]
        [SerializeField] private ScenarioVariableChoice variable = new ScenarioVariableChoice();

        [ScenarioLabel("Operation")]
        [SerializeField] private ScenarioNumberOperation operation;

        [Tooltip("By how much. For “Random Range” — the smallest value")]
        [ScenarioLabel("Amount")]
        [SerializeField] private ScenarioValue<float> amount = new ScenarioValue<float>(1f);

        [Tooltip("For “Random Range” — the largest value")]
        [ScenarioLabel("Max")]
        [SerializeField] private ScenarioValue<float> maxAmount = new ScenarioValue<float>(1f);


        public ScenarioVariableChoice Variable => variable;

        public ScenarioNumberOperation Operation
        {
            get => operation;
            set => operation = value;
        }

        public ScenarioValue<float> Amount => amount;

        public ScenarioValue<float> MaxAmount => maxAmount;

        public override string Summary => operation switch
        {
            ScenarioNumberOperation.Add => "add",
            ScenarioNumberOperation.Subtract => "subtract",
            ScenarioNumberOperation.Multiply => "multiply",
            ScenarioNumberOperation.Divide => "divide",
            _ => "random range"
        };


        protected override void Apply(ScenarioRun run)
        {
            if (!variable.IsChosen)
                throw new InvalidOperationException("No variable chosen");

            var variables = run.Scenario.GraphVariables;
            var current = variables.GetLiveValue(variable.VariableId, out var definition);

            if (!ScenarioVariableValues.TryGetNumber(definition.Kind, current, out var number))
                throw new InvalidOperationException($"“{definition.Name}” is a {definition.Kind}, not a number");

            var result = Calculate(number, amount.Get(run), run);

            if (definition.Kind == ScenarioVariableKind.Counter)
                variables.SetCounter(variable.VariableId, Mathf.RoundToInt(result));
            else
                variables.SetNumber(variable.VariableId, result);
        }

        private float Calculate(float number, float by, ScenarioRun run)
        {
            switch (operation)
            {
                case ScenarioNumberOperation.Add: return number + by;
                case ScenarioNumberOperation.Subtract: return number - by;
                case ScenarioNumberOperation.Multiply: return number * by;

                case ScenarioNumberOperation.Divide:
                    if (Mathf.Approximately(by, 0f))
                        throw new InvalidOperationException("Division by zero");
                    return number / by;

                default:
                    return Random.Range(by, maxAmount.Get(run));
            }
        }
    }


    /// <summary>Что сделать с числом. Новые — только в конец: в данных хранится число.</summary>
    public enum ScenarioNumberOperation
    {
        [InspectorName("Add")] Add,
        [InspectorName("Subtract")] Subtract,
        [InspectorName("Multiply")] Multiply,
        [InspectorName("Divide")] Divide,
        [InspectorName("Random Range")] RandomRange
    }
}
