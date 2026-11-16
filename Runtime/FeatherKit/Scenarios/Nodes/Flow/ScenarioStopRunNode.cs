using System;
using System.Collections;

namespace FeatherKit.Scenarios
{
    /// <summary>Нода «Stop This Run»: обрывает свой запуск целиком и плавно отпускает его долгие эффекты. Другие запуски графа идут дальше.</summary>
    [Serializable]
    [ScenarioBlock("Flow/Stop This Run", Description = "Stop this run and let its long effects go")]
    public class ScenarioStopRunNode : ScenarioFlowNode
    {
        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            run.Scenario.Runs.Stop(run);
            yield break;
        }
    }
}
