using System;
using System.Collections;
using System.Collections.Generic;
using FeatherKit.Attributes;
using AppContext = FeatherKit.Contexts.AppContext;
using FeatherKit.Coroutines;
using FeatherKit.Logging;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Сценарий без кода: когда что-то случилось и условия сошлись — выполнить шаги по очереди.
    /// - слушает свои события, пока включён; запуск поверх идущего пропускается
    /// - запуск идёт, пока идут и основные шаги, и фоновые — пущенные без «Wait Until Done»
    /// - шаги идут на общем раннере корутин: выключение объекта их не обрывает, удаление — обрывает
    /// - пересылает блокам свои Unity-сообщения: старт, вход в триггер и выход из него
    /// - сообщает, чем кончилась каждая попытка запуска, — для журнала в редакторе
    /// </summary>
    [FDescription("A scenario without code: “When” something happens and the “If” conditions are met — run the “Do” steps in order. Add blocks with the “+” button.")]
    public class Scenario : MonoBehaviour
    {
        [SerializeReference] private List<ScenarioEvent> events = new List<ScenarioEvent>();

        [SerializeField] private ScenarioConditionsMode conditionsMode;
        [SerializeReference] private List<ScenarioCondition> conditions = new List<ScenarioCondition>();

        [SerializeReference] private List<ScenarioAction> actions = new List<ScenarioAction>();

        [Tooltip("Run only once; after that, events are ignored")]
        [SerializeField] private bool runsOnce;

        private bool isRunning;
        private bool hasRun;
        private int runNumber;
        private CoroutineToken runToken;
        private ScenarioRun currentRun;
        private float runStartTime;


        /// <summary>Чем кончилась попытка запуска любого сценария — для журнала в редакторе.</summary>
        public static event Action<ScenarioRunRecord> RunReported;


        /// <summary>Объект проснулся впервые — Unity-событие Start.</summary>
        public event Action Started;

        public event Action<Collider> TriggerEntered;

        public event Action<Collider> TriggerExited;


        public bool IsRunning => isRunning;

        public bool HasRun => hasRun;

        /// <summary>Номер идущего основного шага; -1 — сценарий стоит или доигрывают только фоновые шаги.</summary>
        public int CurrentActionIndex { get; private set; } = -1;

        public IReadOnlyList<ScenarioEvent> Events => events;


        // ── Запуск ───────────────────────────────────────────────────────

        /// <summary>Запустить, если условия сошлись. False — уже идёт, уже отработал разовый или условия не сошлись.</summary>
        public bool TryRun(GameObject instigator = null)
        {
            if (isRunning || (runsOnce && hasRun))
                return false;

            var run = new ScenarioRun(this, instigator);

            if (!AreConditionsMet(run))
            {
                Report(run, Time.time, ScenarioRunOutcome.ConditionsNotMet);
                return false;
            }

            var coroutines = AppContext.Current?.Coroutines;

            if (coroutines == null)
            {
                FLog.Error(this, ("Scenario did not start: no AppContext — ", FLogColor.Bad), (name, FLogColor.Value));
                return false;
            }

            hasRun = true;
            isRunning = true;
            runNumber++;
            currentRun = run;
            runStartTime = Time.time;

            runToken = coroutines.Run(PerformRun(run, runNumber));

            return true;
        }

        private bool AreConditionsMet(ScenarioRun run)
        {
            return ScenarioConditionList.AreMet(conditions, conditionsMode, run);
        }

        // Основные шаги — своей веткой; запуск кончается, когда кончились все его ветки или одна упала.
        // Номер запуска нужен, чтобы оборванная корутина не затёрла состояние следующего запуска.
        private IEnumerator PerformRun(ScenarioRun run, int number)
        {
            run.StartBranch(PerformActions(run));

            while (number == runNumber && run.Failure == null && run.HasRunningBranches)
                yield return null;

            if (number != runNumber)
                yield break;

            if (run.Failure == null)
            {
                FinishRun(number, ScenarioRunOutcome.Finished);
                yield break;
            }

            var failedIndex = FailedActionIndexOf(run);

            run.Stop();
            ReportFailedAction(run, failedIndex);
            FinishRun(number, ScenarioRunOutcome.Failed, failedIndex, run.Failure.Message);
        }

        private IEnumerator PerformActions(ScenarioRun run)
        {
            for (var index = 0; index < actions.Count; index++)
            {
                if (run.IsStopped)
                    yield break;

                if (actions[index] == null)
                    continue;

                CurrentActionIndex = index;
                yield return run.PerformStep(actions[index]);
            }

            CurrentActionIndex = -1;
        }

        // Упал основной шаг или вложенный в него — номер идущего основного шага.
        // Упал фоновый — его номер, если он сам основной шаг; вложенный фоновый номера не имеет.
        private int FailedActionIndexOf(ScenarioRun run)
        {
            return run.HasFailedInBackground ? actions.IndexOf(run.FailedAction) : CurrentActionIndex;
        }

        private void ReportFailedAction(ScenarioRun run, int failedIndex)
        {
            var stepText = failedIndex >= 0 ? $" at step {failedIndex + 1}" : run.HasFailedInBackground ? " in a background step" : "";
            var blockText = run.FailedAction != null ? $" ({run.FailedAction.GetType().Name})" : "";

            FLog.Error(this, ("Scenario ", FLogColor.Bad), (name, FLogColor.Value),
                ($" failed{stepText}{blockText}: ", FLogColor.Bad),
                (run.Failure.Message, FLogColor.Muted));

            Debug.LogException(run.Failure, this);
        }

        private void FinishRun(int number, ScenarioRunOutcome outcome, int failedActionIndex = -1, string failureMessage = null)
        {
            if (number != runNumber)
                return;

            isRunning = false;
            CurrentActionIndex = -1;

            Report(currentRun, runStartTime, outcome, failedActionIndex, failureMessage);
        }

        private void Report(ScenarioRun run, float startTime, ScenarioRunOutcome outcome,
            int failedActionIndex = -1, string failureMessage = null)
        {
            if (RunReported == null)
                return;

            var instigatorName = run.Instigator != null ? run.Instigator.name : null;

            RunReported(new ScenarioRunRecord(this, instigatorName, startTime, Time.time, outcome,
                failedActionIndex, failureMessage));
        }


        /// <summary>Запуск для кнопок UnityEvent и сигналов Timeline: им нужен метод без результата.</summary>
        public void Run()
        {
            TryRun();
        }


        /// <summary>
        /// Оборвать идущие шаги, фоновые тоже. Начатое шагами на других объектах —
        /// звук, Timeline, другой сценарий — доигрывает само.
        /// </summary>
        public void Stop()
        {
            if (!isRunning)
                return;

            runNumber++;
            isRunning = false;
            CurrentActionIndex = -1;

            currentRun.Stop();
            runToken.Stop();
            Report(currentRun, runStartTime, ScenarioRunOutcome.Stopped);
        }


        /// <summary>Шаг сейчас идёт — основной, вложенный или фоновый. Для подсветки в инспекторе.</summary>
        public bool IsPerforming(ScenarioAction action)
        {
            return isRunning && currentRun.IsPerforming(action);
        }


        // ── Unity ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            foreach (var scenarioEvent in events)
                scenarioEvent?.Attach(this);
        }

        private void Start()
        {
            Started?.Invoke();
        }

        private void OnDisable()
        {
            foreach (var scenarioEvent in events)
                scenarioEvent?.Release();
        }

        private void OnDestroy()
        {
            Stop();
        }

        private void OnTriggerEnter(Collider other)
        {
            TriggerEntered?.Invoke(other);
        }

        private void OnTriggerExit(Collider other)
        {
            TriggerExited?.Invoke(other);
        }
    }
}
