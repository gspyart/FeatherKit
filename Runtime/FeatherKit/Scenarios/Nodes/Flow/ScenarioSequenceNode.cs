using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Нода «Sequence»: выходы Then 1..N по очереди — следующий, когда всё выросшее из прошлого кончилось; затем Done.</summary>
    [Serializable]
    [ScenarioBlock("Flow/Sequence")]
    public class ScenarioSequenceNode : ScenarioFlowNode
    {
        private const string ThenIdPrefix = "then";

        public static readonly ScenarioOutput Done = new ScenarioOutput("done", "Done");

        [ScenarioLabel("Steps")]
        [Min(1)]
        [SerializeField] private int thenCount = 2;


        public int ThenCount
        {
            get => thenCount;
            set => thenCount = Mathf.Max(1, value);
        }


        public static ScenarioOutput ThenOutput(int number)
        {
            return ScenarioNumberedOutputs.Create(ThenIdPrefix, "Then", number);
        }


        public override void DescribePorts(ScenarioPortList ports)
        {
            ScenarioNumberedOutputs.Describe(this, ports, ThenIdPrefix, "Then", thenCount);
        }


        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            for (var number = 1; number <= thenCount; number++)
                yield return FireAndWait(run, ThenOutput(number));

            Fire(run, Done);
        }
    }
}
