using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Сравнить счётчик с числом: «рычагов ≥ 3».</summary>
    [Serializable]
    [ScenarioBlock("Variables/Compare Counter")]
    public class CounterCondition : ScenarioCondition
    {
        [ScenarioLabel("Counter")]
        [SerializeField] private ScenarioCounter counter;

        [ScenarioLabel("Comparison")]
        [SerializeField] private ScenarioComparison comparison = ScenarioComparison.GreaterOrEqual;

        [ScenarioLabel("Number")]
        [SerializeField] private int value = 1;


        public override string Summary => counter != null ? $"{counter.name} {comparison.Symbol()} {value}" : "no counter selected";


        protected override bool Check(ScenarioRun run)
        {
            return counter != null && comparison.Holds(ScenarioCounters.Get(counter), value);
        }
    }
}
