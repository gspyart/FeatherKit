using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Флаг стал нужным значением — кем бы он ни был поставлен.</summary>
    [Serializable]
    [ScenarioBlock("Variables/Flag Changed")]
    public class FlagChangedEvent : ScenarioEvent
    {
        [ScenarioLabel("Flag")]
        [SerializeField] private ScenarioFlag flag;

        [Tooltip("Fire when the flag turns on; unchecked — when it turns off")]
        [ScenarioLabel("Turned On")]
        [SerializeField] private bool turnedOn = true;


        public override string Summary => flag != null
            ? $"{flag.name} → {(turnedOn ? "on" : "off")}"
            : "no flag selected";


        protected override void OnAttached()
        {
            ScenarioFlags.Changed += HandleFlagChanged;
        }

        protected override void OnReleased()
        {
            ScenarioFlags.Changed -= HandleFlagChanged;
        }

        private void HandleFlagChanged(ScenarioFlag changedFlag, bool value)
        {
            if (changedFlag == flag && value == turnedOn)
                Fire(null);
        }
    }
}
