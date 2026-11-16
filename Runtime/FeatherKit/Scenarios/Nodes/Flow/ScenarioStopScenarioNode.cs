using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «Stop Scenario»: останавливает весь сценарий — другой или этот же, со всеми запусками и долгими эффектами.
    /// Память нод (Gate, Once, Do N…) Stop не трогает; галка «Reset Node Memory» забывает и её — как в начале уровня.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Flow/Stop Scenario", Description = "Stop a whole scenario with all its runs")]
    public class ScenarioStopScenarioNode : ScenarioFlowNode
    {
        public static readonly ScenarioOutput Done = new ScenarioOutput("done", "Done");

        [Tooltip("Which scenario to stop. Empty — this one")]
        [ScenarioLabel("Scenario")]
        [SerializeField] private ScenarioValue<Scenario> scenario = new ScenarioValue<Scenario>();

        [Tooltip("Also forget what Gate, Once, Do N, Toggle and Cooldown remember — as at the start of the level")]
        [ScenarioLabel("Reset Node Memory")]
        [SerializeField] private bool resetsNodeMemory;


        public ScenarioValue<Scenario> TargetScenario => scenario;

        public bool ResetsNodeMemory
        {
            get => resetsNodeMemory;
            set => resetsNodeMemory = value;
        }

        public override string Summary => scenario.UsesVariable ? "graph variable"
            : scenario.Value != null ? scenario.Value.name : "this one";


        // Остановка своего сценария обрывает и эту ветку: Done тогда уже не сработает.
        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            var target = scenario.Get(run);
            var stopped = target != null ? target : run.Scenario;
            stopped.Stop();

            if (resetsNodeMemory)
                stopped.ResetNodeMemory();

            Fire(run, Done);
            yield break;
        }
    }
}
