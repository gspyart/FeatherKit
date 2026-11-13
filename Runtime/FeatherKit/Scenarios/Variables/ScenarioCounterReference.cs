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

        public string Summary => UsesVariable ? "graph variable" : counter != null ? counter.name : "no counter selected";


        public static ScenarioCounterReference FromVariable(string graphVariableId)
        {
            return new ScenarioCounterReference { variableId = graphVariableId };
        }


        public int Get(ScenarioRun run)
        {
            // TODO этап 1, рантайм: переменная графа из экземпляра, которому принадлежит run.
            return UsesVariable ? 0 : ScenarioCounters.Get(counter);
        }

        public void Set(ScenarioRun run, int value)
        {
            // TODO этап 1, рантайм: переменная графа из экземпляра, которому принадлежит run.
            if (!UsesVariable)
                ScenarioCounters.Set(counter, value);
        }
    }
}
