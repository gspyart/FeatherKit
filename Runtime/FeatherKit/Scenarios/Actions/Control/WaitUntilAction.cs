using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Ждать, пока условия сойдутся: игрок посмотрел, флаг включился. Проверка каждый кадр.</summary>
    [Serializable]
    [ScenarioBlock("Control/Wait Until")]
    public class WaitUntilAction : ScenarioAction
    {
        [ScenarioLabel("Conditions")]
        [SerializeField] private ScenarioConditionList conditions = new ScenarioConditionList();

        [Tooltip("Don't wait longer than this, seconds: when it runs out, the scenario moves on. 0 — wait as long as it takes")]
        [Min(0f)]
        [ScenarioLabel("Timeout, s")]
        [SerializeField] private float timeout;


        public override string Summary => $"conditions {conditions.Count}" + (timeout > 0f ? $" · timeout {timeout:0.##} s" : "");


        public override IEnumerator Perform(ScenarioRun run)
        {
            var endTime = Time.time + timeout;

            while (!run.IsStopped && !conditions.IsMet(run))
            {
                if (timeout > 0f && Time.time >= endTime)
                    yield break;

                yield return null;
            }
        }
    }
}
