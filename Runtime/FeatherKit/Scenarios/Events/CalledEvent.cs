using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Сценарий позвали: кнопка UnityEvent, Timeline, нода Run Scenario из другого графа или код через TryRun.</summary>
    [Serializable]
    [ScenarioBlock("Scenario/Called")]
    public class CalledEvent : ScenarioEvent
    {
        protected override void OnAttached()
        {
            Scenario.Called += HandleCalled;
        }

        protected override void OnReleased()
        {
            Scenario.Called -= HandleCalled;
        }

        private void HandleCalled(GameObject instigator)
        {
            Fire(instigator);
        }
    }
}
