using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Сравнить счётчик — общий или переменную графа — с числом: «рычагов ≥ 3».</summary>
    [Serializable]
    [ScenarioBlock("Variables/Compare Counter")]
    public class CounterCondition : ScenarioCondition
    {
        [ScenarioLabel("Counter")]
        [SerializeField] private ScenarioCounterReference counter = new ScenarioCounterReference();

        [ScenarioLabel("Comparison")]
        [SerializeField] private ScenarioComparison comparison = ScenarioComparison.GreaterOrEqual;

        [ScenarioLabel("Number")]
        [SerializeField] private int value = 1;


        public override string Summary => counter.IsEmpty ? counter.Summary : $"{counter.Summary} {comparison.Symbol()} {value}";


        protected override bool Check(ScenarioRun run)
        {
            return !counter.IsEmpty && comparison.Holds(counter.Get(run), value);
        }
    }
}
