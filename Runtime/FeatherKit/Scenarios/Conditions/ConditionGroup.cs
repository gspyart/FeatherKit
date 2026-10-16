using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Несколько условий как одно: «все из» или «хотя бы одно из». Группы вкладываются друг в друга —
    /// так собирается (a или b) и (b или c); галка «НЕ» отрицает всю группу.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Логика/Группа условий")]
    public class ConditionGroup : ScenarioCondition
    {
        [ScenarioLabel("Условия")]
        [SerializeField] private ScenarioConditionList conditions = new ScenarioConditionList();


        public override string Summary =>
            $"{(conditions.Mode == ScenarioConditionsMode.All ? "все из" : "хотя бы одно из")} {conditions.Count}";


        protected override bool Check(ScenarioRun run)
        {
            return conditions.IsMet(run);
        }
    }
}
