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

        public string Summary => UsesVariable ? "graph variable" : flag != null ? flag.name : "no flag selected";


        public static ScenarioFlagReference FromVariable(string graphVariableId)
        {
            return new ScenarioFlagReference { variableId = graphVariableId };
        }


        public bool Get(ScenarioRun run)
        {
            // TODO этап 1, рантайм: переменная графа из экземпляра, которому принадлежит run.
            return !UsesVariable && ScenarioFlags.Get(flag);
        }

        public void Set(ScenarioRun run, bool value)
        {
            // TODO этап 1, рантайм: переменная графа из экземпляра, которому принадлежит run.
            if (!UsesVariable)
                ScenarioFlags.Set(flag, value);
        }
    }
}
