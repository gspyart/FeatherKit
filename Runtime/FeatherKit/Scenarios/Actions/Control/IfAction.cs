using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Развилка: условия сошлись — шаги «то», нет — шаги «иначе». Условия проверяются в момент развилки.</summary>
    [Serializable]
    [ScenarioBlock("Управление/Если — иначе")]
    public class IfAction : ScenarioAction
    {
        [ScenarioLabel("Если")]
        [SerializeField] private ScenarioConditionList conditions = new ScenarioConditionList();

        [Tooltip("Шаги, если условия сошлись")]
        [ScenarioLabel("То")]
        [SerializeField] private ScenarioActionList then = new ScenarioActionList();

        [Tooltip("Шаги, если не сошлись. Можно оставить пустым")]
        [ScenarioLabel("Иначе")]
        [SerializeField] private ScenarioActionList otherwise = new ScenarioActionList();


        public override string Summary => $"условий {conditions.Count} · то {then.Count} · иначе {otherwise.Count}";


        public override IEnumerator Perform(ScenarioRun run)
        {
            yield return conditions.IsMet(run) ? then.Perform(run) : otherwise.Perform(run);
        }
    }
}
