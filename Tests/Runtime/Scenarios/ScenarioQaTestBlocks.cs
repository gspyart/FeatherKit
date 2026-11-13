using System;
using System.Collections;
using FeatherKit.Scenarios;
using UnityEngine;

// Блоки для тестов тестировщика: то, чего нет среди тестовых блоков программиста.
namespace FeatherKit.Tests.Scenarios
{
    /// <summary>Тестовое условие: верно первые N проверок, потом ложно — чтобы ограничить петлю.</summary>
    [Serializable]
    public class ScenarioQaCountdownCondition : ScenarioCondition
    {
        [SerializeField] private int remainingChecks;


        public ScenarioQaCountdownCondition(int remainingChecks)
        {
            this.remainingChecks = remainingChecks;
        }


        protected override bool Check(ScenarioRun run)
        {
            return remainingChecks-- > 0;
        }
    }


    /// <summary>Тестовое условие, которое всегда падает.</summary>
    [Serializable]
    public class ScenarioQaThrowingCondition : ScenarioCondition
    {
        public const string Message = "QA condition failed on purpose";

        [SerializeField] private string unusedName;


        public ScenarioQaThrowingCondition(string unusedName)
        {
            this.unusedName = unusedName;
        }


        protected override bool Check(ScenarioRun run)
        {
            throw new InvalidOperationException(Message);
        }
    }


    /// <summary>Тестовый шаг: посылает сигнал прямо сейчас, в том же кадре.</summary>
    [Serializable]
    public class ScenarioQaSendSignalAction : ScenarioInstantAction
    {
        [NonSerialized] private ScenarioSignal signal;


        public ScenarioQaSendSignalAction(ScenarioSignal signal)
        {
            this.signal = signal;
        }


        protected override void Apply(ScenarioRun run)
        {
            ScenarioSignals.Send(signal, run.Instigator);
        }
    }


    /// <summary>Тестовый шаг: ждёт секунды игрового времени (WaitForSeconds) — для проверки паузы.</summary>
    [Serializable]
    public class ScenarioQaWaitSecondsAction : ScenarioAction
    {
        [SerializeField] private float seconds;


        public ScenarioQaWaitSecondsAction(float seconds)
        {
            this.seconds = seconds;
        }


        public override IEnumerator Perform(ScenarioRun run)
        {
            yield return new WaitForSeconds(seconds);
        }
    }
}
