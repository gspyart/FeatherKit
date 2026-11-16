using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Счётчик — общий или переменная графа — изменился и теперь подходит под сравнение: «рычагов стало ≥ 3».</summary>
    [Serializable]
    [ScenarioBlock("Variables/Counter Changed", Description = "Fires when a counter changes and matches the comparison")]
    public class CounterChangedEvent : ScenarioEvent
    {
        [ScenarioLabel("Counter")]
        [SerializeField] private ScenarioCounterReference counter = new ScenarioCounterReference();

        [ScenarioLabel("Comparison")]
        [SerializeField] private ScenarioComparison comparison = ScenarioComparison.GreaterOrEqual;

        [ScenarioLabel("Number")]
        [SerializeField] private int value = 1;

        // Переменные того экземпляра графа, где событие подписано: отписываться надо от них же.
        [NonSerialized] private ScenarioGraphVariables listenedVariables;


        public override string Summary => counter.IsEmpty ? counter.Summary : $"{counter.Summary} {comparison.Symbol()} {value}";


        protected override void OnAttached()
        {
            if (counter.UsesVariable)
            {
                listenedVariables = Scenario.GraphVariables;
                listenedVariables.Changed += HandleVariableChanged;
            }
            else
            {
                ScenarioCounters.Changed += HandleCounterChanged;
            }
        }

        protected override void OnReleased()
        {
            ScenarioCounters.Changed -= HandleCounterChanged;

            if (listenedVariables != null)
                listenedVariables.Changed -= HandleVariableChanged;

            listenedVariables = null;
        }


        private void HandleCounterChanged(ScenarioCounter changedCounter, int newValue)
        {
            if (changedCounter == counter.Counter && comparison.Holds(newValue, value))
                Fire(null);
        }

        private void HandleVariableChanged(ScenarioVariableDefinition variable)
        {
            if (variable.Id == counter.VariableId && comparison.Holds(listenedVariables.GetCounter(variable.Id), value))
                Fire(null);
        }
    }
}
