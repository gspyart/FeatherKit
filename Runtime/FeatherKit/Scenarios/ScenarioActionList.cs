using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Вложенный столбик шагов внутри блока: ветки «Then» и «Else», тело повтора, ветка «In Parallel».
    /// Шаги идут по очереди, фоновые — не дожидаясь; остановленный сценарий не начинает следующий шаг.
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
                    yield return run.PerformStep(actions[index]);
            }
        }
    }
}
