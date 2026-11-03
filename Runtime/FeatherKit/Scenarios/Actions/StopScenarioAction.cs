using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Оборвать идущий сценарий — другой или этот же: шаги после этого блока не выполнятся.</summary>
    [Serializable]
    [ScenarioBlock("Scenario/Stop Scenario")]
    public class StopScenarioAction : ScenarioInstantAction
    {
        [Tooltip("Which scenario to stop. Empty — this one")]
        [ScenarioLabel("Scenario")]
        [SerializeField] private Scenario scenario;


        public override string Summary => scenario != null ? scenario.name : "this one";


        protected override void Apply(ScenarioRun run)
        {
            (scenario != null ? scenario : run.Scenario).Stop();
        }
    }
}
