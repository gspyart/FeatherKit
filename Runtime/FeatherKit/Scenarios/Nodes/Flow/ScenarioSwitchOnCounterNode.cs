using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «Switch on Counter»: идёт в выход того значения счётчика, что сейчас; другого значения нет в списке — Default.
    /// Id выхода — само значение: поменяли число в списке — провод к старому выходу пропадёт.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Flow/Switch on Counter", Description = "Go to the output of the counter's current value")]
    public class ScenarioSwitchOnCounterNode : ScenarioFlowNode
    {
        private const string ValueIdPrefix = "value.";

        public static readonly ScenarioOutput Default = new ScenarioOutput("default", "Default");

        [ScenarioLabel("Counter")]
        [SerializeField] private ScenarioCounterReference counter = new ScenarioCounterReference();

        [Tooltip("One output per value; the same value twice makes one output")]
        [ScenarioLabel("Values")]
        [SerializeField] private List<int> values = new List<int> { 0, 1, 2 };


        public ScenarioCounterReference Counter => counter;

        public List<int> Values => values;

        public override string Summary => counter.Summary;


        public static ScenarioOutput ValueOutput(int value)
        {
            return new ScenarioOutput(ValueIdPrefix + value, value.ToString());
        }


        // Выходы значений — до Default: на ноде он стоит последним.
        public override void DescribePorts(ScenarioPortList ports)
        {
            foreach (var input in ScenarioNodePorts.InputsOf(GetType()))
                ports.AddInput(input);

            foreach (var value in values)
                ports.AddOutput(ValueOutput(value));

            ports.AddOutput(Default);
        }


        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            var current = counter.Get(run);

            Fire(run, values.Contains(current) ? ValueOutput(current) : Default);
            yield break;
        }
    }
}
