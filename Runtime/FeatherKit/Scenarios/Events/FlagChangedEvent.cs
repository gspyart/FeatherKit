using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Флаг стал нужным значением — кем бы он ни был поставлен.</summary>
    [Serializable]
    [ScenarioBlock("Переменные/Флаг изменился")]
    public class FlagChangedEvent : ScenarioEvent
    {
        [ScenarioLabel("Флаг")]
        [SerializeField] private ScenarioFlag flag;

        [Tooltip("Срабатывать, когда флаг стал включён; без галки — когда стал выключен")]
        [ScenarioLabel("Стал включён")]
        [SerializeField] private bool turnedOn = true;


        public override string Summary => flag != null
            ? $"{flag.name} → {(turnedOn ? "вкл" : "выкл")}"
            : "флаг не выбран";


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
