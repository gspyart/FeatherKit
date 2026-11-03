using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Послать сигнал всем сценариям, которые его ждут. «Caller» передаётся им дальше.</summary>
    [Serializable]
    [ScenarioBlock("Signals/Send Signal")]
    public class SendSignalAction : ScenarioInstantAction
    {
        [ScenarioLabel("Signal")]
        [SerializeField] private ScenarioSignal signal;


        public override string Summary => signal != null ? signal.name : "no signal selected";


        protected override void Apply(ScenarioRun run)
        {
            if (signal == null)
                throw new InvalidOperationException("No signal selected");

            ScenarioSignals.Send(signal, run.Instigator);
        }
    }
}
