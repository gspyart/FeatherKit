using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Запустить другой сценарий — со своими условиями.
    /// «Кто вызвал» передаётся дальше: тот, кто вошёл в триггер здесь, будет им и там.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Сценарий/Запустить сценарий")]
    public class RunScenarioAction : ScenarioAction
    {
        [ScenarioLabel("Сценарий")]
        [SerializeField] private Scenario scenario;

        [Tooltip("Следующий шаг начнётся, когда тот сценарий закончит")]
        [ScenarioLabel("Ждать конца")]
        [SerializeField] private bool waitsForEnd;


        public override string Summary => scenario != null
            ? scenario.name + (waitsForEnd ? " · ждать конца" : "")
            : "сценарий не выбран";


        public override IEnumerator Perform(ScenarioRun run)
        {
            if (scenario == null)
                throw new InvalidOperationException("Сценарий не выбран");

            if (!scenario.TryRun(run.Instigator) || !waitsForEnd)
                yield break;

            while (scenario != null && scenario.IsRunning)
                yield return null;
        }
    }
}
