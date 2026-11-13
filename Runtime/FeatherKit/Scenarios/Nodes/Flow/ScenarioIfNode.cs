using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Нода «Branch»: условия сошлись — выход True, нет — False. Проверка разовая, в момент входа.</summary>
    [Serializable]
    [ScenarioBlock("Flow/Branch")]
    public class ScenarioIfNode : ScenarioFlowNode
    {
        public static readonly ScenarioOutput True = new ScenarioOutput("true", "True");
        public static readonly ScenarioOutput False = new ScenarioOutput("false", "False");

        [SerializeField] private ScenarioConditionList conditions = new ScenarioConditionList();


        public ScenarioConditionList Conditions => conditions;


        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            Fire(run, conditions.IsMet(run) ? True : False);
            yield break;
        }
    }
}
