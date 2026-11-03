using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Несколько условий как одно: «all of» (все) или «at least one of» (хотя бы одно). Группы вкладываются друг в друга —
    /// так собирается (a или b) и (b или c); галка «NOT» отрицает всю группу.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Logic/Condition Group")]
    public class ConditionGroup : ScenarioCondition
    {
        [ScenarioLabel("Conditions")]
        [SerializeField] private ScenarioConditionList conditions = new ScenarioConditionList();


        public override string Summary =>
            $"{(conditions.Mode == ScenarioConditionsMode.All ? "all of" : "at least one of")} {conditions.Count}";


        protected override bool Check(ScenarioRun run)
        {
            return conditions.IsMet(run);
        }
    }
}
