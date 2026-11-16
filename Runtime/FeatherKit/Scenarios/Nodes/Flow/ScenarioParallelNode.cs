using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «Parallel»: пускает выходы Branch 1..N разом и ждёт их.
    /// - All Done — дальше, когда кончились все; First Done — когда первая, отставшие обрываются
    /// - выход без проводов в ожидании не участвует
    /// </summary>
    [Serializable]
    [ScenarioBlock("Flow/Parallel", Description = "Run all branches at once and wait for all or the first")]
    public class ScenarioParallelNode : ScenarioFlowNode
    {
        private const string BranchIdPrefix = "branch";

        public static readonly ScenarioOutput Done = new ScenarioOutput("done", "Done");

        [ScenarioLabel("Branches")]
        [Min(1)]
        [SerializeField] private int branchCount = 2;

        [Tooltip("When to go on: after all branches are done, or after the first one — the rest are cut off")]
        [ScenarioLabel("Continue When")]
        [SerializeField] private ScenarioParallelCompletion completion;


        public int BranchCount
        {
            get => branchCount;
            set => branchCount = Mathf.Max(1, value);
        }

        public ScenarioParallelCompletion Completion
        {
            get => completion;
            set => completion = value;
        }

        public override string Summary => completion == ScenarioParallelCompletion.FirstDone ? "until the first one" : null;


        public static ScenarioOutput BranchOutput(int number)
        {
            return ScenarioNumberedOutputs.Create(BranchIdPrefix, "Branch", number);
        }


        public override void DescribePorts(ScenarioPortList ports)
        {
            ScenarioNumberedOutputs.Describe(this, ports, BranchIdPrefix, "Branch", branchCount);
        }


        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            var startedBranches = new List<ScenarioFiring>(branchCount);

            for (var number = 1; number <= branchCount; number++)
            {
                var firing = Fire(run, BranchOutput(number));

                if (firing.HasBranches)
                    startedBranches.Add(firing);
            }

            // Каждый кусок спрашиваем в этом же ходе: так он запоминает, что его ждут, и узнаёт свои ветки.
            var runningCount = CountRunning(startedBranches);

            if (completion == ScenarioParallelCompletion.AllDone)
            {
                while (runningCount > 0)
                {
                    yield return null;
                    runningCount = CountRunning(startedBranches);
                }
            }
            else
            {
                while (startedBranches.Count > 0 && runningCount == startedBranches.Count)
                {
                    yield return null;
                    runningCount = CountRunning(startedBranches);
                }

                foreach (var firing in startedBranches)
                {
                    if (firing.IsRunning)
                        firing.Cancel();
                }
            }

            Fire(run, Done);
        }

        private static int CountRunning(List<ScenarioFiring> firings)
        {
            var runningCount = 0;

            foreach (var firing in firings)
            {
                if (firing.IsRunning)
                    runningCount++;
            }

            return runningCount;
        }
    }


    /// <summary>Когда нода Parallel идёт дальше.</summary>
    public enum ScenarioParallelCompletion
    {
        [InspectorName("All Done")] AllDone,
        [InspectorName("First Done")] FirstDone
    }
}
