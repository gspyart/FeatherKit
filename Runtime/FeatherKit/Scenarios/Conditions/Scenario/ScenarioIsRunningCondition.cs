using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Другой сценарий — или этот же — сейчас идёт: хоть один его запуск ещё не кончился.</summary>
    [Serializable]
    [ScenarioBlock("Scenario/Scenario Is Running", Description = "A scenario has a run going right now")]
    public class ScenarioIsRunningCondition : ScenarioCondition
    {
        [Tooltip("Which scenario. Empty — this one")]
        [ScenarioLabel("Scenario")]
        [SerializeField] private ScenarioValue<Scenario> scenario = new ScenarioValue<Scenario>();


        public ScenarioValue<Scenario> TargetScenario => scenario;

        public override string Summary => scenario.UsesVariable ? "graph variable"
            : scenario.Value != null ? scenario.Value.name : "this one";


        protected override bool Check(ScenarioRun run)
        {
            var target = scenario.Get(run);

            return (target != null ? target : run.Scenario).IsRunning;
        }
    }
}
