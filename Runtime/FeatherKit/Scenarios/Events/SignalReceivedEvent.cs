using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Кто-то послал сигнал. «Caller» — тот же, что у пославшего сценария.</summary>
    [Serializable]
    [ScenarioBlock("Signals/Signal Received")]
    public class SignalReceivedEvent : ScenarioEvent
    {
        [ScenarioLabel("Signal")]
        [SerializeField] private ScenarioSignal signal;


        public override string Summary => signal != null ? signal.name : "no signal selected";


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
