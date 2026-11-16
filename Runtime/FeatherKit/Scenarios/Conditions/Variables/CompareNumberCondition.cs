using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Сравнить два числа — каждое своё или из переменной: «задержка ≥ 2», «жизни < порога».</summary>
    [Serializable]
    [ScenarioBlock("Variables/Compare Number", Description = "Compare two numbers, each a value or a variable")]
    public class CompareNumberCondition : ScenarioCondition
    {
        [ScenarioLabel("Number")]
        [SerializeField] private ScenarioValue<float> number = new ScenarioValue<float>();

        [ScenarioLabel("Is")]
        [SerializeField] private ScenarioComparison comparison = ScenarioComparison.GreaterOrEqual;

        [ScenarioLabel("Than")]
        [SerializeField] private ScenarioValue<float> other = new ScenarioValue<float>();


        public ScenarioValue<float> Number => number;

        public ScenarioComparison Comparison
        {
            get => comparison;
            set => comparison = value;
        }

        public ScenarioValue<float> Other => other;

        public override string Summary => $"{Describe(number)} {comparison.Symbol()} {Describe(other)}";


        protected override bool Check(ScenarioRun run)
        {
            return comparison.Holds(number.Get(run), other.Get(run));
        }

        private static string Describe(ScenarioValue<float> value)
        {
            return value.UsesVariable ? "variable" : value.Value.ToString("0.##");
        }
    }
}
