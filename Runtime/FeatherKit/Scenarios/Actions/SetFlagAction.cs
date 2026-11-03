using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Поставить флаг сценариев: включить, выключить или переключить.</summary>
    [Serializable]
    [ScenarioBlock("Variables/Change Flag")]
    public class SetFlagAction : ScenarioInstantAction
    {
        [ScenarioLabel("Flag")]
        [SerializeField] private ScenarioFlag flag;


        [ScenarioLabel("Action")]
        [SerializeField] private ScenarioSwitch change;


        public override string Summary => flag != null ? $"{change.Describe()} {flag.name}" : "no flag selected";


        protected override void Apply(ScenarioRun run)
        {
            if (flag == null)
                throw new InvalidOperationException("No flag selected");

            flag.Value = change.StateAfter(flag.Value);
        }
    }
}
