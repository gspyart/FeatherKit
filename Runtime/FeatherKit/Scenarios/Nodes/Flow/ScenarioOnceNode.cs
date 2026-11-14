using System;
using System.Collections;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «Once»: пропускает в Out только первый вход; Reset открывает снова.
    /// Пройдена ли — общее для ноды, а не для запуска. Stop возвращает начальное.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Flow/Once", Description = "Let the flow through only the first time")]
    public class ScenarioOnceNode : ScenarioFlowNode, IScenarioLiveNode
    {
        public static readonly ScenarioInput Reset = new ScenarioInput("reset", "Reset");
        public static readonly ScenarioOutput Out = new ScenarioOutput("out", "Out");

        [NonSerialized] private bool hasPassed;


        public bool HasPassed => hasPassed;


        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            if (input == Reset)
            {
                hasPassed = false;
                yield break;
            }

            if (hasPassed)
                yield break;

            hasPassed = true;
            Fire(run, Out);
        }


        // IScenarioLiveNode
        void IScenarioLiveNode.ResetLiveState()
        {
            hasPassed = false;
        }
    }
}
