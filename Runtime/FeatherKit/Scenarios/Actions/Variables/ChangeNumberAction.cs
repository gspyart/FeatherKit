using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Изменить число в переменной Counter или Number: прибавить, отнять, умножить, поделить или взять случайное.
    /// Counter — в целых: величина целая, деление отбрасывает дробь к нулю, случайное — от и до включительно.
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

        [Tooltip("By how much. For “Random Range” — the smallest value. A Counter takes whole numbers only: 7 / 2 = 3, −7 / 2 = −3")]
        [ScenarioLabel("Amount")]
        [SerializeField] private ScenarioValue<float> amount = new ScenarioValue<float>(1f);

        [Tooltip("For “Random Range” — the largest value; a Counter can get it too")]
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

            if (definition.Kind == ScenarioVariableKind.Counter)
                variables.SetCounter(variable.VariableId, CalculateCounter(current.CounterValue, run));
            else
                variables.SetNumber(variable.VariableId, CalculateNumber(number, amount.Get(run), run));
        }

        // В long, затем в пределы int: большой счётчик не теряет единицы, как в float, и не переполняется.
        private int CalculateCounter(int counter, ScenarioRun run)
        {
            var by = WholeNumberOf(amount.Get(run));

            long result = operation switch
            {
                ScenarioNumberOperation.Add => (long)counter + by,
                ScenarioNumberOperation.Subtract => (long)counter - by,
                ScenarioNumberOperation.Multiply => (long)counter * by,
                ScenarioNumberOperation.Divide => by != 0 ? counter / by : throw new InvalidOperationException("Division by zero"),
                _ => Random.Range(by, WholeNumberOf(maxAmount.Get(run)) + 1)
            };

            return (int)Math.Clamp(result, int.MinValue, int.MaxValue);
        }

        private static int WholeNumberOf(float value)
        {
            var rounded = Mathf.Round(value);

            if (!Mathf.Approximately(value, rounded) || rounded > int.MaxValue || rounded < int.MinValue)
                throw new InvalidOperationException($"Counter changes by whole numbers, not by {value}");

            return (int)rounded;
        }

        private float CalculateNumber(float number, float by, ScenarioRun run)
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
