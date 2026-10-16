using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Поставить флаг сценариев: включить, выключить или переключить.</summary>
    [Serializable]
    [ScenarioBlock("Переменные/Изменить флаг")]
    public class SetFlagAction : ScenarioInstantAction
    {
        [ScenarioLabel("Флаг")]
        [SerializeField] private ScenarioFlag flag;


        [ScenarioLabel("Действие")]
        [SerializeField] private ScenarioSwitch change;


        public override string Summary => flag != null ? $"{change.Describe()} {flag.name}" : "флаг не выбран";


        protected override void Apply(ScenarioRun run)
        {
            if (flag == null)
                throw new InvalidOperationException("Флаг не выбран");

            flag.Value = change.StateAfter(flag.Value);
        }
    }
}
