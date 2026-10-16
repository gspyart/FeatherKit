using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Оборвать идущий сценарий — другой или этот же: шаги после этого блока не выполнятся.</summary>
    [Serializable]
    [ScenarioBlock("Сценарий/Остановить сценарий")]
    public class StopScenarioAction : ScenarioInstantAction
    {
        [Tooltip("Какой сценарий остановить. Пусто — этот")]
        [ScenarioLabel("Сценарий")]
        [SerializeField] private Scenario scenario;


        public override string Summary => scenario != null ? scenario.name : "этот";


        protected override void Apply(ScenarioRun run)
        {
            (scenario != null ? scenario : run.Scenario).Stop();
        }
    }
}
