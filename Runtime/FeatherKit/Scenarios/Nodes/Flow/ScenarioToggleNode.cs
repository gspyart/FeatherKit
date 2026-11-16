using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «Toggle»: входы по очереди уходят то в A, то в B.
    /// Очередь — память ноды, общая для всех запусков; Stop её не трогает, забывает Scenario.ResetNodeMemory.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Flow/Toggle", Description = "Go to A, then to B, then to A again")]
    public class ScenarioToggleNode : ScenarioFlowNode, IScenarioNodeMemory
    {
        public static readonly ScenarioOutput A = new ScenarioOutput("a", "A");
        public static readonly ScenarioOutput B = new ScenarioOutput("b", "B");

        [ScenarioLabel("Starts With A")]
        [SerializeField] private bool startsWithA = true;

        [NonSerialized] private int passCount;


        public bool StartsWithA
        {
            get => startsWithA;
            set => startsWithA = value;
        }


        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            var isA = (passCount % 2 == 0) == startsWithA;
            passCount++;

            Fire(run, isA ? A : B);
            yield break;
        }


        // IScenarioNodeMemory
        public void ForgetMemory()
        {
            passCount = 0;
        }
    }
}
