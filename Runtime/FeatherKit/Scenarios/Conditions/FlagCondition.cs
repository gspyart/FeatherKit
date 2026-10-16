using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Флаг включён. С галкой «НЕ» — выключен.</summary>
    [Serializable]
    [ScenarioBlock("Переменные/Флаг включён")]
    public class FlagCondition : ScenarioCondition
    {
        [ScenarioLabel("Флаг")]
        [SerializeField] private ScenarioFlag flag;


        public override string Summary => flag != null ? flag.name : "флаг не выбран";


        protected override bool Check(ScenarioRun run)
        {
            return ScenarioFlags.Get(flag);
        }
    }
}
