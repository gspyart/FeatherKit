using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «Run Scenario»: зовёт другой сценарий — у графа срабатывают ноды When с событием Called.
    /// «Caller» передаётся дальше; с «Wait Until Done» Done — когда кончились запуски, начатые этим вызовом.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Flow/Run Scenario")]
    public class ScenarioRunScenarioNode : ScenarioFlowNode
    {
        public static readonly ScenarioOutput Done = new ScenarioOutput("done", "Done");

        [ScenarioLabel("Scenario")]
        [SerializeField] private ScenarioValue<Scenario> scenario = new ScenarioValue<Scenario>();

        [Tooltip("Done fires when the called scenario has finished. Unchecked — right after calling it")]
        [ScenarioLabel("Wait Until Done")]
        [SerializeField] private bool waitsForEnd = true;


        public ScenarioValue<Scenario> TargetScenario => scenario;

        public bool WaitsForEnd
        {
            get => waitsForEnd;
            set => waitsForEnd = value;
        }

        public override string Summary => scenario.UsesVariable ? "graph variable"
            : scenario.Value != null ? scenario.Value.name : "no scenario selected";


        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            var target = scenario.Get(run);

            if (target == null)
                throw new InvalidOperationException("No scenario selected");

            if (target.UsesGraph)
                yield return CallGraph(target, run);
            else
                yield return CallListScenario(target, run);

            Fire(run, Done);
        }

        // Ждём только свои запуски: у цели могут идти и другие, свои — без конца.
        private IEnumerator CallGraph(Scenario target, ScenarioRun run)
        {
            var startedRuns = new List<ScenarioRun>();
            target.CallGraph(run.Instigator, startedRuns);

            if (!waitsForEnd)
                yield break;

            while (HasRunning(startedRuns))
                yield return null;
        }

        private static bool HasRunning(List<ScenarioRun> runs)
        {
            foreach (var startedRun in runs)
            {
                if (!startedRun.IsCompleted)
                    return true;
            }

            return false;
        }

        private IEnumerator CallListScenario(Scenario target, ScenarioRun run)
        {
            if (!target.TryRun(run.Instigator) || !waitsForEnd)
                yield break;

            while (target != null && target.IsRunning)
                yield return null;
        }
    }
}
