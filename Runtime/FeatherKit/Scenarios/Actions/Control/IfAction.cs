using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Развилка: условия сошлись — шаги «Then», нет — шаги «Else». Условия проверяются в момент развилки.</summary>
    [Serializable]
    [ScenarioBlock("Control/If — Else")]
    public class IfAction : ScenarioLongAction
    {
        [ScenarioLabel("If")]
        [SerializeField] private ScenarioConditionList conditions = new ScenarioConditionList();

        [Tooltip("Steps if the conditions are met")]
        [ScenarioLabel("Then")]
        [SerializeField] private ScenarioActionList then = new ScenarioActionList();

        [Tooltip("Steps if they are not met. Can be left empty")]
        [ScenarioLabel("Else")]
        [SerializeField] private ScenarioActionList otherwise = new ScenarioActionList();


        public override string Summary => $"conditions {conditions.Count} · then {then.Count} · else {otherwise.Count}";


        public override IEnumerator Perform(ScenarioRun run)
        {
            yield return conditions.IsMet(run) ? then.Perform(run) : otherwise.Perform(run);
        }
    }
}
