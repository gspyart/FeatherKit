using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Поле блока «какой флаг»: общий флаг-ассет или переменная Flag этого графа.
    /// Переменная графа своя у каждого экземпляра, ассет — один на игру или уровень.
    /// </summary>
    [Serializable]
    public class ScenarioFlagReference
    {
        [SerializeField] private ScenarioFlag flag;

        [Tooltip("The graph's Flag variable to use instead of the asset. Empty — the asset is used")]
        [SerializeField] private string variableId;


        public ScenarioFlagReference()
        {
        }

        public ScenarioFlagReference(ScenarioFlag flag)
        {
            this.flag = flag;
        }


        public ScenarioFlag Flag => flag;

        public string VariableId => variableId;

        public bool UsesVariable => !string.IsNullOrEmpty(variableId);

        /// <summary>Ни ассета, ни переменной — блок не настроен.</summary>
        internal bool IsEmpty => !UsesVariable && flag == null;

        public string Summary => UsesVariable ? "graph variable" : flag != null ? flag.name : "no flag selected";


        public static ScenarioFlagReference FromVariable(string graphVariableId)
        {
            return new ScenarioFlagReference { variableId = graphVariableId };
        }


        public bool Get(ScenarioRun run)
        {
            return UsesVariable ? run.Scenario.GraphVariables.GetFlag(variableId) : ScenarioFlags.Get(flag);
        }

        public void Set(ScenarioRun run, bool value)
        {
            if (UsesVariable)
                run.Scenario.GraphVariables.SetFlag(variableId, value);
            else
                ScenarioFlags.Set(flag, value);
        }
    }
}
