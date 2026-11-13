using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Нода «Stop Scenario»: останавливает весь сценарий — другой или этот же, со всеми запусками и долгими эффектами.</summary>
    [Serializable]
    [ScenarioBlock("Flow/Stop Scenario")]
    public class ScenarioStopScenarioNode : ScenarioFlowNode
    {
        public static readonly ScenarioOutput Done = new ScenarioOutput("done", "Done");

        [Tooltip("Which scenario to stop. Empty — this one")]
        [ScenarioLabel("Scenario")]
        [SerializeField] private ScenarioValue<Scenario> scenario = new ScenarioValue<Scenario>();


        public ScenarioValue<Scenario> TargetScenario => scenario;

        public override string Summary => scenario.UsesVariable ? "graph variable"
            : scenario.Value != null ? scenario.Value.name : "this one";


        // Остановка своего сценария обрывает и эту ветку: Done тогда уже не сработает.
        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            var target = scenario.Get(run);
            (target != null ? target : run.Scenario).Stop();

            Fire(run, Done);
            yield break;
        }
    }
}
