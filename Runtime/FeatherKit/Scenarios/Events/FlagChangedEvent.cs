using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Флаг — общий или переменная графа — стал нужным значением, кем бы он ни был поставлен.</summary>
    [Serializable]
    [ScenarioBlock("Variables/Flag Changed", Description = "Fires when a flag turns on or off")]
    public class FlagChangedEvent : ScenarioEvent
    {
        [ScenarioLabel("Flag")]
        [SerializeField] private ScenarioFlagReference flag = new ScenarioFlagReference();

        [Tooltip("Fire when the flag turns on; unchecked — when it turns off")]
        [ScenarioLabel("Turned On")]
        [SerializeField] private bool turnedOn = true;

        // Переменные того экземпляра графа, где событие подписано: отписываться надо от них же.
        [NonSerialized] private ScenarioGraphVariables listenedVariables;


        public override string Summary => flag.IsEmpty ? flag.Summary : $"{flag.Summary} → {(turnedOn ? "on" : "off")}";


        protected override void OnAttached()
        {
            if (flag.UsesVariable)
            {
                listenedVariables = Scenario.GraphVariables;
                listenedVariables.Changed += HandleVariableChanged;
            }
            else
            {
                ScenarioFlags.Changed += HandleFlagChanged;
            }
        }

        protected override void OnReleased()
        {
            ScenarioFlags.Changed -= HandleFlagChanged;

            if (listenedVariables != null)
                listenedVariables.Changed -= HandleVariableChanged;

            listenedVariables = null;
        }


        private void HandleFlagChanged(ScenarioFlag changedFlag, bool value)
        {
            if (changedFlag == flag.Flag && value == turnedOn)
                Fire(null);
        }

        private void HandleVariableChanged(ScenarioVariableDefinition variable)
        {
            if (variable.Id == flag.VariableId && listenedVariables.GetFlag(variable.Id) == turnedOn)
                Fire(null);
        }
    }
}
