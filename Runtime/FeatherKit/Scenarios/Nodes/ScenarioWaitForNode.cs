using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «Wait For»: стоит, пока не наступит один из исходов, и идёт в его выход.
    /// - исход — событие или условие; первый наступивший побеждает, в одном кадре — верхний в списке
    /// - события слушает, условия проверяет только пока на ноде кто-то ждёт
    /// - не дождался за Timeout — выход Timed Out; 0 — ждать вечно
    /// </summary>
    [Serializable]
    [ScenarioBlock("Flow/Wait For")]
    public class ScenarioWaitForNode : ScenarioFlowNode, IScenarioEventListener
    {
        public static readonly ScenarioOutput TimedOut = new ScenarioOutput("timedOut", "Timed Out");

        [SerializeField] private List<ScenarioWaitCase> cases = new List<ScenarioWaitCase>();

        [Tooltip("Give up after this many seconds and go to “Timed Out”. 0 — wait forever")]
        [Min(0f)]
        [ScenarioLabel("Timeout, s")]
        [SerializeField] private float timeout;

        [Tooltip("How often to check the conditions, in seconds. 0 — every frame")]
        [Min(0f)]
        [ScenarioLabel("Check Interval, s")]
        [ScenarioInspectorOnly]
        [SerializeField] private float checkInterval;

        [NonSerialized] private Scenario listeningScenario;


        public IReadOnlyList<ScenarioWaitCase> Cases => cases;

        public float Timeout
        {
            get => timeout;
            set => timeout = Mathf.Max(0f, value);
        }

        public float CheckInterval
        {
            get => checkInterval;
            set => checkInterval = Mathf.Max(0f, value);
        }

        // IScenarioEventListener
        public Scenario Scenario => listeningScenario;


        // ── Исходы ───────────────────────────────────────────────────────

        public ScenarioWaitCase AddCase(ScenarioWaitCase waitCase)
        {
            return InsertCase(cases.Count, waitCase);
        }


        /// <summary>Вставить исход на место index. Исход с уже занятым id получает новый.</summary>
        public ScenarioWaitCase InsertCase(int index, ScenarioWaitCase waitCase)
        {
            if (waitCase == null)
                throw new ArgumentNullException(nameof(waitCase));

            if (string.IsNullOrEmpty(waitCase.Id) || FindCase(waitCase.Id) != null)
                waitCase.AssignNewId();

            cases.Insert(Mathf.Clamp(index, 0, cases.Count), waitCase);

            return waitCase;
        }


        /// <summary>Переставить исход: порядок решает, кто победит, если два наступили в одном кадре.</summary>
        public void MoveCase(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= cases.Count)
                throw new ArgumentOutOfRangeException(nameof(fromIndex));

            var waitCase = cases[fromIndex];
            cases.RemoveAt(fromIndex);
            cases.Insert(Mathf.Clamp(toIndex, 0, cases.Count), waitCase);
        }


        /// <summary>Исход с этим id или null.</summary>
        public ScenarioWaitCase FindCase(string caseId)
        {
            foreach (var waitCase in cases)
            {
                if (waitCase != null && string.Equals(waitCase.Id, caseId, StringComparison.Ordinal))
                    return waitCase;
            }

            return null;
        }


        /// <summary>Выдать новые id пустым и повторным: копия элемента в инспекторе несёт id соседа. True — повторы были.</summary>
        public bool EnsureUniqueCaseIds()
        {
            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            var hadRepeats = false;

            foreach (var waitCase in cases)
            {
                if (waitCase == null)
                    continue;

                if (string.IsNullOrEmpty(waitCase.Id) || usedIds.Contains(waitCase.Id))
                {
                    waitCase.AssignNewId();
                    hadRepeats = true;
                }

                usedIds.Add(waitCase.Id);
            }

            return hadRepeats;
        }


        // Снаружи — только через ScenarioGraph.RemoveWaitCase: он же снимает провода выхода исхода.
        internal bool RemoveCase(string caseId)
        {
            var waitCase = FindCase(caseId);

            return waitCase != null && cases.Remove(waitCase);
        }


        // ── Порты ────────────────────────────────────────────────────────

        // Выходы исходов — до Timed Out: на ноде он стоит последним.
        public override void DescribePorts(ScenarioPortList ports)
        {
            foreach (var input in ScenarioNodePorts.InputsOf(GetType()))
                ports.AddInput(input);

            foreach (var waitCase in cases)
            {
                if (waitCase != null)
                    ports.AddOutput(waitCase.Output);
            }

            foreach (var output in ScenarioNodePorts.OutputsOf(GetType()))
                ports.AddOutput(output);
        }


        // ── Ожидание ─────────────────────────────────────────────────────

        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            // TODO этап 1, рантайм: подписать события исходов на первом ожидании, проверять условия, тайм-аут.
            // TODO этап 1, рантайм: listeningScenario заполнить до Attach событий исходов — им нужен Scenario.
            yield break;
        }


        // IScenarioEventListener
        public void HandleEventFired(ScenarioEvent firedEvent, GameObject instigator)
        {
            // TODO этап 1, рантайм: исход-событие наступил — у всех идущих ожиданий этой ноды.
        }
    }
}
