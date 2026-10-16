using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Кто-то послал сигнал. «Кто вызвал» — тот же, что у пославшего сценария.</summary>
    [Serializable]
    [ScenarioBlock("Сигналы/Сигнал получен")]
    public class SignalReceivedEvent : ScenarioEvent
    {
        [ScenarioLabel("Сигнал")]
        [SerializeField] private ScenarioSignal signal;


        public override string Summary => signal != null ? signal.name : "сигнал не выбран";


        protected override void OnAttached()
        {
            ScenarioSignals.Sent += HandleSignalSent;
        }

        protected override void OnReleased()
        {
            ScenarioSignals.Sent -= HandleSignalSent;
        }

        private void HandleSignalSent(ScenarioSignal sentSignal, GameObject instigator)
        {
            if (sentSignal == signal)
                Fire(instigator);
        }
    }
}
