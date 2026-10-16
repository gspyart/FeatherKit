using System;
using FeatherKit.Logging;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Строка в консоль — проверить, что сценарий дошёл до этого места. В релизе молчит.</summary>
    [Serializable]
    [ScenarioBlock("Отладка/Записать в лог")]
    public class LogAction : ScenarioInstantAction
    {
        [ScenarioLabel("Текст")]
        [SerializeField] private string message;


        public override string Summary => string.IsNullOrEmpty(message) ? "пусто" : message;


        protected override void Apply(ScenarioRun run)
        {
            var instigator = run.Instigator != null ? run.Instigator.name : "никто";

            FLog.Info(run.Scenario, (run.Scenario.name + ": ", FLogColor.Muted), (message, FLogColor.Value),
                ($"  (вызвал: {instigator})", FLogColor.Muted));
        }
    }
}
