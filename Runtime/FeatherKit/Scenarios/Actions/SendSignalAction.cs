using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Послать сигнал всем сценариям, которые его ждут. «Кто вызвал» передаётся им дальше.</summary>
    [Serializable]
    [ScenarioBlock("Сигналы/Послать сигнал")]
    public class SendSignalAction : ScenarioInstantAction
    {
        [ScenarioLabel("Сигнал")]
        [SerializeField] private ScenarioSignal signal;


        public override string Summary => signal != null ? signal.name : "сигнал не выбран";


        protected override void Apply(ScenarioRun run)
        {
            if (signal == null)
                throw new InvalidOperationException("Сигнал не выбран");

            ScenarioSignals.Send(signal, run.Instigator);
        }
    }
}
