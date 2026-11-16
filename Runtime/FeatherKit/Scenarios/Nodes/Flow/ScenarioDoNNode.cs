using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «Do N»: первые N входов идут в Out, дальше — в Exhausted; Reset начинает счёт заново.
    /// Счёт — память ноды, общая для всех запусков; Stop его не трогает, забывает Reset и Scenario.ResetNodeMemory.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Flow/Do N", Description = "Let the flow through N times, then go to Exhausted")]
    public class ScenarioDoNNode : ScenarioFlowNode, IScenarioNodeMemory
    {
        public static readonly ScenarioInput Reset = new ScenarioInput("reset", "Reset");
        public static readonly ScenarioOutput Out = new ScenarioOutput("out", "Out");
        public static readonly ScenarioOutput Exhausted = new ScenarioOutput("exhausted", "Exhausted");

        [ScenarioLabel("Times")]
        [Min(1)]
        [SerializeField] private int timesAllowed = 3;

        [NonSerialized] private int passCount;


        public int TimesAllowed
        {
            get => timesAllowed;
            set => timesAllowed = Mathf.Max(1, value);
        }

        public override string Summary => $"{timesAllowed} times";


        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            if (input == Reset)
            {
                passCount = 0;
                yield break;
            }

            if (passCount >= timesAllowed)
            {
                Fire(run, Exhausted);
                yield break;
            }

            passCount++;
            Fire(run, Out);
        }


        // IScenarioNodeMemory
        public void ForgetMemory()
        {
            passCount = 0;
        }
    }
}
