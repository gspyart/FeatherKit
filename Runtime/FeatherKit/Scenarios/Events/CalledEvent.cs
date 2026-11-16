using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Сценарий позвали: кнопка UnityEvent, Timeline, нода Run Scenario из другого графа или код через TryRun.</summary>
    [Serializable]
    [ScenarioBlock("Scenario/Called", Description = "Fires when the scenario is called by a button, Timeline or Run Scenario")]
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
