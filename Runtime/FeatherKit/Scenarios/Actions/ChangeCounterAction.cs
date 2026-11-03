using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Изменить счётчик: прибавить (отрицательное — отнять) или задать значение.</summary>
    [Serializable]
    [ScenarioBlock("Variables/Change Counter")]
    public class ChangeCounterAction : ScenarioInstantAction
    {
        [ScenarioLabel("Counter")]
        [SerializeField] private ScenarioCounter counter;

        [ScenarioLabel("Action")]
        [SerializeField] private CounterChange change;

        [Tooltip("How much to add, or which value to set")]
        [ScenarioLabel("Number")]
        [SerializeField] private int amount = 1;


        public override string Summary => counter == null
            ? "no counter selected"
            : change == CounterChange.Add ? $"{counter.name} {amount:+0;-0;+0}" : $"{counter.name} = {amount}";


        protected override void Apply(ScenarioRun run)
        {
            if (counter == null)
                throw new InvalidOperationException("No counter selected");

            counter.Value = change == CounterChange.Add ? counter.Value + amount : amount;
        }
    }


    public enum CounterChange
    {
        [InspectorName("Add")] Add,
        [InspectorName("Set")] Set
    }
}
