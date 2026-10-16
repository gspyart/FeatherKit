using System;

namespace FeatherKit.Scenarios
{
    /// <summary>Объект сценария появился и впервые включился — обычно это старт сцены.</summary>
    [Serializable]
    [ScenarioBlock("Объект/Старт")]
    public class ObjectStartedEvent : ScenarioEvent
    {
        protected override void OnAttached()
        {
            Scenario.Started += HandleStarted;
        }

        protected override void OnReleased()
        {
            Scenario.Started -= HandleStarted;
        }

        private void HandleStarted()
        {
            Fire(null);
        }
    }
}
