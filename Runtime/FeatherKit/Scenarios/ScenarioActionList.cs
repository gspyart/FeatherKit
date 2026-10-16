using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Вложенный столбик шагов внутри блока: ветки «то» и «иначе», тело повтора, ветка «одновременно».
    /// Шаги идут по очереди; остановленный сценарий не начинает следующий шаг.
    /// </summary>
    [Serializable]
    public class ScenarioActionList
    {
        [SerializeReference] private List<ScenarioAction> actions = new List<ScenarioAction>();


        public int Count => actions.Count;


        public IEnumerator Perform(ScenarioRun run)
        {
            for (var index = 0; index < actions.Count; index++)
            {
                if (run.IsStopped)
                    yield break;

                if (actions[index] != null)
                    yield return actions[index].Perform(run);
            }
        }
    }
}
