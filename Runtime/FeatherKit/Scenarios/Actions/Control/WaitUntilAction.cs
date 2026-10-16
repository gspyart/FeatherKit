using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Ждать, пока условия сойдутся: игрок посмотрел, флаг включился. Проверка каждый кадр.</summary>
    [Serializable]
    [ScenarioBlock("Управление/Ждать, пока")]
    public class WaitUntilAction : ScenarioAction
    {
        [ScenarioLabel("Условия")]
        [SerializeField] private ScenarioConditionList conditions = new ScenarioConditionList();

        [Tooltip("Дольше не ждать, секунды: истекло — сценарий идёт дальше. 0 — ждать сколько угодно")]
        [Min(0f)]
        [ScenarioLabel("Не дольше, с")]
        [SerializeField] private float timeout;


        public override string Summary => $"условий {conditions.Count}" + (timeout > 0f ? $" · не дольше {timeout:0.##} с" : "");


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
