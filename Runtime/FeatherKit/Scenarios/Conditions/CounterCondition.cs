using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Сравнить счётчик с числом: «рычагов ≥ 3».</summary>
    [Serializable]
    [ScenarioBlock("Переменные/Сравнить счётчик")]
    public class CounterCondition : ScenarioCondition
    {
        [ScenarioLabel("Счётчик")]
        [SerializeField] private ScenarioCounter counter;

        [ScenarioLabel("Сравнение")]
        [SerializeField] private ScenarioComparison comparison = ScenarioComparison.GreaterOrEqual;

        [ScenarioLabel("Число")]
        [SerializeField] private int value = 1;


        public override string Summary => counter != null ? $"{counter.name} {comparison.Symbol()} {value}" : "счётчик не выбран";


        protected override bool Check(ScenarioRun run)
        {
            return counter != null && comparison.Holds(ScenarioCounters.Get(counter), value);
        }
    }
}
