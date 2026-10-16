using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AppContext = FeatherKit.Contexts.AppContext;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Ветки идут одновременно: звук, мигание и силуэт разом. Следующий шаг — когда закончатся все.
    /// Ошибка в любой ветке обрывает сценарий, как ошибка обычного шага.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Управление/Одновременно")]
    public class ParallelAction : ScenarioAction
    {
        [ScenarioLabel("Ветки")]
        [SerializeField] private List<ScenarioActionList> branches = new List<ScenarioActionList>();


        public override string Summary => $"веток {branches.Count}";


        public override IEnumerator Perform(ScenarioRun run)
        {
            var progress = new ParallelBranchesProgress { RunningCount = branches.Count };

            foreach (var branch in branches)
                AppContext.Current.Coroutines.Run(PerformBranch(branch, run, progress));

            while (progress.RunningCount > 0 && progress.Failure == null && !run.IsStopped)
                yield return null;

            if (progress.Failure != null)
                throw new InvalidOperationException("Ветка «одновременно» оборвалась: " + progress.Failure.Message, progress.Failure);
        }

        // Ветка идёт отдельной корутиной, поэтому ошибку ловит сама и отдаёт блоку через progress.
        private static IEnumerator PerformBranch(ScenarioActionList branch, ScenarioRun run, ParallelBranchesProgress progress)
        {
            var steps = ScenarioSteps.Flatten(branch.Perform(run));

            while (!run.IsStopped && progress.Failure == null)
            {
                bool hasNextStep;

                try
                {
                    hasNextStep = steps.MoveNext();
                }
                catch (Exception exception)
                {
                    progress.Failure = exception;
                    break;
                }

                if (!hasNextStep)
                    break;

                yield return steps.Current;
            }

            progress.RunningCount--;
        }


        /// <summary>Сколько веток ещё идёт и чем оборвалась первая упавшая.</summary>
        private class ParallelBranchesProgress
        {
            public int RunningCount;
            public Exception Failure;
        }
    }
}
