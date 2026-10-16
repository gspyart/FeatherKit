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
    /// - шаги идут на общем раннере корутин: выключение объекта их не обрывает, удаление — обрывает
    /// - пересылает блокам свои Unity-сообщения: старт, вход в триггер и выход из него
    /// - сообщает, чем кончилась каждая попытка запуска, — для журнала в редакторе
    /// </summary>
    [FDescription("Сценарий без кода: «Когда» что-то случилось и «Если» условия сошлись — выполнить шаги «Что сделать» по очереди. Блоки добавляются кнопкой «+».")]
    public class Scenario : MonoBehaviour
    {
        [SerializeReference] private List<ScenarioEvent> events = new List<ScenarioEvent>();

        [SerializeField] private ScenarioConditionsMode conditionsMode;
        [SerializeReference] private List<ScenarioCondition> conditions = new List<ScenarioCondition>();

        [SerializeReference] private List<ScenarioAction> actions = new List<ScenarioAction>();

        [Tooltip("Сработать только один раз; дальше события молчат")]
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

        /// <summary>Номер идущего шага; -1 — сценарий стоит. Для подсветки в инспекторе.</summary>
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
                FLog.Error(this, ("Сценарий не запустился: нет AppContext — ", FLogColor.Bad), (name, FLogColor.Value));
                return false;
            }

            hasRun = true;
            isRunning = true;
            runNumber++;
            currentRun = run;
            runStartTime = Time.time;

            runToken = coroutines.Run(PerformActions(run, runNumber));

            return true;
        }

        private bool AreConditionsMet(ScenarioRun run)
        {
            return ScenarioConditionList.AreMet(conditions, conditionsMode, run);
        }

        // Номер запуска нужен, чтобы оборванная корутина не затёрла состояние следующего запуска.
        private IEnumerator PerformActions(ScenarioRun run, int number)
        {
            for (var index = 0; index < actions.Count && number == runNumber; index++)
            {
                var action = actions[index];

                if (action == null)
                    continue;

                CurrentActionIndex = index;
                var steps = ScenarioSteps.Flatten(action.Perform(run));

                while (number == runNumber)
                {
                    bool hasNextStep;

                    try
                    {
                        hasNextStep = steps.MoveNext();
                    }
                    catch (Exception exception)
                    {
                        ReportFailedAction(index, action, exception);
                        FinishRun(number, ScenarioRunOutcome.Failed, index, exception.Message);
                        yield break;
                    }

                    if (!hasNextStep)
                        break;

                    yield return steps.Current;
                }
            }

            FinishRun(number, ScenarioRunOutcome.Finished);
        }

        private void ReportFailedAction(int index, ScenarioAction action, Exception exception)
        {
            FLog.Error(this, ("Сценарий ", FLogColor.Bad), (name, FLogColor.Value),
                ($" оборван на шаге {index + 1} ({action.GetType().Name}): ", FLogColor.Bad),
                (exception.Message, FLogColor.Muted));

            Debug.LogException(exception, this);
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


        /// <summary>Оборвать идущие шаги. Шаг, начатый на других объектах (звук, Timeline), доигрывает сам.</summary>
        public void Stop()
        {
            if (!isRunning)
                return;

            runNumber++;
            isRunning = false;
            CurrentActionIndex = -1;
            currentRun.IsStopped = true;

            runToken.Stop();
            Report(currentRun, runStartTime, ScenarioRunOutcome.Stopped);
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
