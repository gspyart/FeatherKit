using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Счётчик изменился и теперь подходит под сравнение: «рычагов стало ≥ 3».</summary>
    [Serializable]
    [ScenarioBlock("Переменные/Счётчик изменился")]
    public class CounterChangedEvent : ScenarioEvent
    {
        [ScenarioLabel("Счётчик")]
        [SerializeField] private ScenarioCounter counter;

        [ScenarioLabel("Сравнение")]
        [SerializeField] private ScenarioComparison comparison = ScenarioComparison.GreaterOrEqual;

        [ScenarioLabel("Число")]
        [SerializeField] private int value = 1;


        public override string Summary => counter != null ? $"{counter.name} {comparison.Symbol()} {value}" : "счётчик не выбран";


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
