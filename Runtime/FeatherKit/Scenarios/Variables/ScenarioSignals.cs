using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Рассылка сигналов сценариев: послать сигнал — все подписанные получат его сразу, в этом же кадре.</summary>
    public static class ScenarioSignals
    {
        /// <summary>Сигнал и кто вызвал сценарий, пославший его; может быть null.</summary>
        public static event Action<ScenarioSignal, GameObject> Sent;


        public static void Send(ScenarioSignal signal, GameObject instigator)
        {
            if (signal == null || Sent == null)
                return;

            ScenarioEventOccurrences.Begin();

            try
            {
                Sent.Invoke(signal, instigator);
            }
            finally
            {
                ScenarioEventOccurrences.End();
            }
        }


        // Рассылка статическая: без перезагрузки домена подписчики прошлого запуска дожили бы до следующего.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            Sent = null;
        }
    }
}
