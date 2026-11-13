using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Изменить счётчик — общий или переменную графа: прибавить (отрицательное — отнять) или задать значение.</summary>
    [Serializable]
    [ScenarioBlock("Variables/Change Counter")]
    public class ChangeCounterAction : ScenarioInstantAction
    {
        [ScenarioLabel("Counter")]
        [SerializeField] private ScenarioCounterReference counter = new ScenarioCounterReference();

        [ScenarioLabel("Action")]
        [SerializeField] private CounterChange change;

        [Tooltip("How much to add, or which value to set")]
        [ScenarioLabel("Number")]
        [SerializeField] private int amount = 1;


        public override string Summary => counter.IsEmpty
            ? counter.Summary
            : change == CounterChange.Add ? $"{counter.Summary} {amount:+0;-0;+0}" : $"{counter.Summary} = {amount}";


        protected override void Apply(ScenarioRun run)
        {
            if (counter.IsEmpty)
                throw new InvalidOperationException("No counter selected");

            counter.Set(run, change == CounterChange.Add ? counter.Get(run) + amount : amount);
        }
    }


    public enum CounterChange
    {
        [InspectorName("Add")] Add,
        [InspectorName("Set")] Set
    }
}
