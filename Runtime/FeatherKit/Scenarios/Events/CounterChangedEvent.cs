using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Счётчик изменился и теперь подходит под сравнение: «рычагов стало ≥ 3».</summary>
    [Serializable]
    [ScenarioBlock("Variables/Counter Changed")]
    public class CounterChangedEvent : ScenarioEvent
    {
        [ScenarioLabel("Counter")]
        [SerializeField] private ScenarioCounter counter;

        [ScenarioLabel("Comparison")]
        [SerializeField] private ScenarioComparison comparison = ScenarioComparison.GreaterOrEqual;

        [ScenarioLabel("Number")]
        [SerializeField] private int value = 1;


        public override string Summary => counter != null ? $"{counter.name} {comparison.Symbol()} {value}" : "no counter selected";


        protected override void OnAttached()
        {
            ScenarioCounters.Changed += HandleCounterChanged;
        }

        protected override void OnReleased()
        {
            ScenarioCounters.Changed -= HandleCounterChanged;
        }

        private void HandleCounterChanged(ScenarioCounter changedCounter, int newValue)
        {
            if (changedCounter == counter && comparison.Holds(newValue, value))
                Fire(null);
        }
    }
}
