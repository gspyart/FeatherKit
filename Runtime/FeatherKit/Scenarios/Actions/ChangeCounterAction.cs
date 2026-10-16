using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Изменить счётчик: прибавить (отрицательное — отнять) или задать значение.</summary>
    [Serializable]
    [ScenarioBlock("Переменные/Изменить счётчик")]
    public class ChangeCounterAction : ScenarioInstantAction
    {
        [ScenarioLabel("Счётчик")]
        [SerializeField] private ScenarioCounter counter;

        [ScenarioLabel("Действие")]
        [SerializeField] private CounterChange change;

        [Tooltip("Сколько прибавить или какое значение задать")]
        [ScenarioLabel("Число")]
        [SerializeField] private int amount = 1;


        public override string Summary => counter == null
            ? "счётчик не выбран"
            : change == CounterChange.Add ? $"{counter.name} {amount:+0;-0;+0}" : $"{counter.name} = {amount}";


        protected override void Apply(ScenarioRun run)
        {
            if (counter == null)
                throw new InvalidOperationException("Счётчик не выбран");

            counter.Value = change == CounterChange.Add ? counter.Value + amount : amount;
        }
    }


    public enum CounterChange
    {
        [InspectorName("Прибавить")] Add,
        [InspectorName("Задать")] Set
    }
}
