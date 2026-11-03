using System;
using FeatherKit.Logging;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Строка в консоль — проверить, что сценарий дошёл до этого места. В релизе молчит.</summary>
    [Serializable]
    [ScenarioBlock("Debug/Write to Log")]
    public class LogAction : ScenarioInstantAction
    {
        [ScenarioLabel("Text")]
        [SerializeField] private string message;


        public override string Summary => string.IsNullOrEmpty(message) ? "empty" : message;


        protected override void Apply(ScenarioRun run)
        {
            var instigator = run.Instigator != null ? run.Instigator.name : "nobody";

            FLog.Info(run.Scenario, (run.Scenario.name + ": ", FLogColor.Muted), (message, FLogColor.Value),
                ($"  (called by: {instigator})", FLogColor.Muted));
        }
    }
}
