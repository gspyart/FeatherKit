using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Флаг включён. С галкой «NOT» — выключен.</summary>
    [Serializable]
    [ScenarioBlock("Variables/Flag Is On")]
    public class FlagCondition : ScenarioCondition
    {
        [ScenarioLabel("Flag")]
        [SerializeField] private ScenarioFlag flag;


        public override string Summary => flag != null ? flag.name : "no flag selected";


        protected override bool Check(ScenarioRun run)
        {
            return ScenarioFlags.Get(flag);
        }
    }
}
