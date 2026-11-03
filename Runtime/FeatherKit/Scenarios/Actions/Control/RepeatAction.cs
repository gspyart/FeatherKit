using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Повторить шаги N раз или без конца — и только пока сходятся условия «While».
    /// Без конца — не чаще раза в кадр, иначе мгновенные шаги подвесили бы игру.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Control/Repeat")]
    public class RepeatAction : ScenarioLongAction
    {
        [Tooltip("How many times. 0 — endlessly, until the scenario is stopped or the “while” conditions stop being met")]
        [Min(0)]
        [ScenarioLabel("Times")]
        [SerializeField] private int times = 3;

        [Tooltip("Repeat only while the conditions are met. Empty — no conditions")]
        [ScenarioLabel("While")]
        [SerializeField] private ScenarioConditionList whileConditions = new ScenarioConditionList();


        [ScenarioLabel("Steps")]
        [SerializeField] private ScenarioActionList actions = new ScenarioActionList();


        public override string Summary => (times > 0 ? $"{times} times" : "endlessly")
                                          + (whileConditions.Count > 0 ? $" · while conditions {whileConditions.Count}" : "");


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
