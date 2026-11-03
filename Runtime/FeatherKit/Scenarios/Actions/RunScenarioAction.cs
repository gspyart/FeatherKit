using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Запустить другой сценарий — со своими условиями.
    /// «Caller» передаётся дальше: тот, кто вошёл в триггер здесь, будет им и там.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Scenario/Run Scenario")]
    public class RunScenarioAction : ScenarioLongAction
    {
        [ScenarioLabel("Scenario")]
        [SerializeField] private Scenario scenario;


        public override string Summary => scenario != null ? scenario.name : "no scenario selected";


        public override IEnumerator Perform(ScenarioRun run)
        {
            if (scenario == null)
                throw new InvalidOperationException("No scenario selected");

            if (!scenario.TryRun(run.Instigator))
                yield break;

            while (scenario != null && scenario.IsRunning)
                yield return null;
        }
    }
}
