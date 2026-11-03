using System;
using System.Collections;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Дождаться фоновых шагов — пущенных без «Wait Until Done».
    /// Пример: монстр идёт фоном, пока мигает свет и играет звук; перед криком — дождаться, пока он дойдёт.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Control/Wait for Background Steps")]
    public class WaitForBackgroundAction : ScenarioAction
    {
        public override IEnumerator Perform(ScenarioRun run)
        {
            while (run.HasBackgroundSteps)
                yield return null;
        }
    }
}
