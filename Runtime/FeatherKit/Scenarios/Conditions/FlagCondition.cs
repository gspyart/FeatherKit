using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Флаг включён — общий или переменная графа. С галкой «NOT» — выключен.</summary>
    [Serializable]
    [ScenarioBlock("Variables/Flag Is On")]
    public class FlagCondition : ScenarioCondition
    {
        [ScenarioLabel("Flag")]
        [SerializeField] private ScenarioFlagReference flag = new ScenarioFlagReference();


        public override string Summary => flag.Summary;


        protected override bool Check(ScenarioRun run)
        {
            return flag.Get(run);
        }
    }
}
