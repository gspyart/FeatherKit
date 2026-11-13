using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Поставить флаг — общий или переменную графа: включить, выключить или переключить.</summary>
    [Serializable]
    [ScenarioBlock("Variables/Change Flag")]
    public class SetFlagAction : ScenarioInstantAction
    {
        [ScenarioLabel("Flag")]
        [SerializeField] private ScenarioFlagReference flag = new ScenarioFlagReference();


        [ScenarioLabel("Action")]
        [SerializeField] private ScenarioSwitch change;


        public override string Summary => flag.IsEmpty ? flag.Summary : $"{change.Describe()} {flag.Summary}";


        protected override void Apply(ScenarioRun run)
        {
            if (flag.IsEmpty)
                throw new InvalidOperationException("No flag selected");

            flag.Set(run, change.StateAfter(flag.Get(run)));
        }
    }
}
