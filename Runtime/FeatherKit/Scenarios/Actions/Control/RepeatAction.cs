using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Повторить шаги N раз или без конца — и только пока сходятся условия «пока».
    /// Без конца — не чаще раза в кадр, иначе мгновенные шаги подвесили бы игру.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Управление/Повторить")]
    public class RepeatAction : ScenarioAction
    {
        [Tooltip("Сколько раз. 0 — без конца, пока сценарий не остановят или не разойдутся условия «пока»")]
        [Min(0)]
        [ScenarioLabel("Сколько раз")]
        [SerializeField] private int times = 3;

        [Tooltip("Повторять, только пока условия сходятся. Пусто — без условий")]
        [ScenarioLabel("Пока")]
        [SerializeField] private ScenarioConditionList whileConditions = new ScenarioConditionList();


        [ScenarioLabel("Шаги")]
        [SerializeField] private ScenarioActionList actions = new ScenarioActionList();


        public override string Summary => (times > 0 ? $"{times} раз" : "без конца")
                                          + (whileConditions.Count > 0 ? $" · пока условий {whileConditions.Count}" : "");


        public override IEnumerator Perform(ScenarioRun run)
        {
            for (var round = 0; times == 0 || round < times; round++)
            {
                if (run.IsStopped || !whileConditions.IsMet(run))
                    yield break;

                yield return actions.Perform(run);

                if (times == 0)
                    yield return null;
            }
        }
    }
}
