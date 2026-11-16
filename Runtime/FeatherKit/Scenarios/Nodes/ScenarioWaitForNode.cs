using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «Wait For»: стоит, пока не наступит один из исходов, и идёт в его выход — в следующем кадре.
    /// - исход — событие или условие; первый наступивший побеждает, в одном кадре — верхний в списке
    /// - события слушает, условия проверяет, пока на ноде ждут; событие, на котором ожидание началось, не в счёт
    /// - не дождался за Timeout — выход Timed Out; 0 — ждать вечно
    /// - выключенный объект триггеров не получает: исход-триггер на нём дождётся только тайм-аута
    /// </summary>
    [Serializable]
    [ScenarioBlock("Flow/Wait For", Description = "Wait until one of the cases happens and go to its output")]
    public class ScenarioWaitForNode : ScenarioFlowNode, IScenarioEventListener, IScenarioLiveNode
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

        // Идущие ожидания: при Run Alongside на одной ноде ждут несколько запусков.
        [NonSerialized] private List<WaitForNodeWaiter> waiters;


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

        // Первая проверка — сразу: исход, наступивший до входа, пропускает ноду в том же кадре.
        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            var waiter = BeginWaiting(run);
            var schedule = new ScenarioCheckSchedule(checkInterval);
            var startTime = Time.time;

            while (true)
            {
                var winningIndex = waiter.HasEventFromEarlierFrame || !schedule.IsDue()
                    ? waiter.EventCaseIndex
                    : Mathf.Min(waiter.EventCaseIndex, FirstMetConditionIndex(run));

                if (winningIndex < cases.Count)
                {
                    var winningCase = cases[winningIndex];
                    EndWaiting(waiter);
                    Fire(run, winningCase.Output);
                    yield break;
                }

                if (timeout > 0f && Time.time - startTime >= timeout)
                {
                    EndWaiting(waiter);
                    Fire(run, TimedOut);
                    yield break;
                }

                yield return null;
            }
        }

        private int FirstMetConditionIndex(ScenarioRun run)
        {
            for (var index = 0; index < cases.Count; index++)
            {
                var condition = cases[index]?.Condition;

                if (condition != null && cases[index].AwaitedEvent == null && condition.IsMet(run))
                    return index;
            }

            return int.MaxValue;
        }


        // ── Подписка на события исходов ──────────────────────────────────

        // Конец ожидания ловим и у оборванной ветки: иначе события остались бы подписанными навсегда.
        private WaitForNodeWaiter BeginWaiting(ScenarioRun run)
        {
            waiters ??= new List<WaitForNodeWaiter>();

            var waiter = new WaitForNodeWaiter(ScenarioEventOccurrences.LatestStarted);
            waiters.Add(waiter);
            run.ActiveBranch?.CallWhenFinished(() => EndWaiting(waiter));

            if (waiters.Count == 1)
                AttachCaseEvents(run.Scenario);

            return waiter;
        }

        private void AttachCaseEvents(Scenario scenario)
        {
            listeningScenario = scenario;

            foreach (var waitCase in cases)
                waitCase?.AwaitedEvent?.Attach(this);
        }


        private void EndWaiting(WaitForNodeWaiter waiter)
        {
            if (waiters.Remove(waiter) && waiters.Count == 0)
                ReleaseCaseEvents();
        }

        private void ReleaseCaseEvents()
        {
            foreach (var waitCase in cases)
                waitCase?.AwaitedEvent?.Release();

            listeningScenario = null;
        }


        // IScenarioLiveNode
        void IScenarioLiveNode.ResetLiveState()
        {
            if (waiters == null || waiters.Count == 0)
                return;

            waiters.Clear();
            ReleaseCaseEvents();
        }


        // IScenarioEventListener
        public void HandleEventFired(ScenarioEvent firedEvent, GameObject instigator)
        {
            if (waiters == null)
                return;

            for (var caseIndex = 0; caseIndex < cases.Count; caseIndex++)
            {
                if (cases[caseIndex] == null || cases[caseIndex].AwaitedEvent != firedEvent)
                    continue;

                foreach (var waiter in waiters)
                    waiter.NoteEvent(caseIndex);

                return;
            }
        }


        /// <summary>Одно идущее ожидание на ноде: какое событие исходов пришло и в каком кадре.</summary>
        private class WaitForNodeWaiter
        {
            // Рассылки с номером не больше начались до ожидания: стук, начавший запуск, — не второй стук.
            private readonly int lastOccurrenceBeforeWait;

            private int eventFrame;


            public WaitForNodeWaiter(int lastOccurrenceBeforeWait)
            {
                this.lastOccurrenceBeforeWait = lastOccurrenceBeforeWait;
            }


            /// <summary>Исход-событие, пришедшее первым; в одном кадре — верхнее. int.MaxValue — ещё не было.</summary>
            public int EventCaseIndex { get; private set; } = int.MaxValue;

            /// <summary>Событие пришло кадром раньше проверки условий — оно наступило первым и побеждает.</summary>
            public bool HasEventFromEarlierFrame => EventCaseIndex != int.MaxValue && eventFrame < Time.frameCount;


            public void NoteEvent(int caseIndex)
            {
                var occurrence = ScenarioEventOccurrences.Current;

                if (HasEventFromEarlierFrame || (occurrence != 0 && occurrence <= lastOccurrenceBeforeWait))
                    return;

                eventFrame = Time.frameCount;
                EventCaseIndex = Mathf.Min(EventCaseIndex, caseIndex);
            }
        }
    }
}
