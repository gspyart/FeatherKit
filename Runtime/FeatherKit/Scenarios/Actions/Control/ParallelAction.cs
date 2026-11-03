using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Ветки идут одновременно: звук, мигание и силуэт разом.
    /// Следующий шаг — когда закончатся все ветки или первая из них; тогда остальные обрываются.
    /// Ошибка в любой ветке обрывает сценарий, как ошибка обычного шага.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Control/In Parallel")]
    public class ParallelAction : ScenarioLongAction
    {
        [Tooltip("When the next step starts: after all branches are done, or after the first one — the rest are cut off")]
        [ScenarioLabel("Continue When")]
        [SerializeField] private ParallelCompletion completion;

        [ScenarioLabel("Branches")]
        [SerializeField] private List<ScenarioActionList> branches = new List<ScenarioActionList>();


        public override string Summary => $"branches {branches.Count}"
                                          + (completion == ParallelCompletion.FirstBranch ? " · until the first one" : "");


        public override IEnumerator Perform(ScenarioRun run)
        {
            if (branches.Count == 0)
                yield break;

            var startedBranches = new List<ScenarioBranch>(branches.Count);

            foreach (var branch in branches)
                startedBranches.Add(run.StartBranch(branch.Perform(run)));

            while (!IsComplete(startedBranches))
                yield return null;

            // Дошедшим до конца Stop ничего не делает — обрываются только отставшие.
            foreach (var branch in startedBranches)
                branch.Stop();
        }

        private bool IsComplete(List<ScenarioBranch> startedBranches)
        {
            return completion == ParallelCompletion.FirstBranch
                ? startedBranches.Exists(branch => branch.IsFinished)
                : startedBranches.TrueForAll(branch => branch.IsFinished);
        }
    }


    /// <summary>Когда «In Parallel» отпускает сценарий дальше.</summary>
    public enum ParallelCompletion
    {
        [InspectorName("All Branches Are Done")] AllBranches,
        [InspectorName("First Branch Is Done")] FirstBranch
    }
}
