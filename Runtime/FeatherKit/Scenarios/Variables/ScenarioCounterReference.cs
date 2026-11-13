using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Поле блока «какой счётчик»: общий счётчик-ассет или переменная Counter этого графа.
    /// Переменная графа своя у каждого экземпляра, ассет — один на игру или уровень.
    /// </summary>
    [Serializable]
    public class ScenarioCounterReference
    {
        [SerializeField] private ScenarioCounter counter;

        [Tooltip("The graph's Counter variable to use instead of the asset. Empty — the asset is used")]
        [SerializeField] private string variableId;


        public ScenarioCounterReference()
        {
        }

        public ScenarioCounterReference(ScenarioCounter counter)
        {
            this.counter = counter;
        }


        public ScenarioCounter Counter => counter;

        public string VariableId => variableId;

        public bool UsesVariable => !string.IsNullOrEmpty(variableId);

        /// <summary>Ни ассета, ни переменной — блок не настроен.</summary>
        internal bool IsEmpty => !UsesVariable && counter == null;

        public string Summary => UsesVariable ? "graph variable" : counter != null ? counter.name : "no counter selected";


        public static ScenarioCounterReference FromVariable(string graphVariableId)
        {
            return new ScenarioCounterReference { variableId = graphVariableId };
        }


        public int Get(ScenarioRun run)
        {
            return UsesVariable ? run.Scenario.GraphVariables.GetCounter(variableId) : ScenarioCounters.Get(counter);
        }

        public void Set(ScenarioRun run, int value)
        {
            if (UsesVariable)
                run.Scenario.GraphVariables.SetCounter(variableId, value);
            else
                ScenarioCounters.Set(counter, value);
        }
    }
}
