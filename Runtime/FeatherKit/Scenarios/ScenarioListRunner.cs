using System.Collections;
using FeatherKit.Coroutines;
using FeatherKit.Logging;
using UnityEngine;
using AppContext = FeatherKit.Contexts.AppContext;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Старый режим Scenario — события, условия и шаги списками, пока граф пуст. Часть Scenario.
    /// - запускает шаги по очереди одним запуском; запуск поверх идущего пропускается
    /// - обрывает запуск, сообщает журналу, чем он кончился и на каком шаге упал
    /// - уходит вместе со старыми списками
    /// </summary>
    public class ScenarioListRunner
    {
        private readonly Scenario owner;

        private int runNumber;
        private CoroutineToken runToken;
        private ScenarioRun currentRun;
        private float runStartTime;


        public ScenarioListRunner(Scenario owner)
        {
            this.owner = owner;
        }


        public bool IsRunning { get; private set; }

        public bool HasRun { get; private set; }

        /// <summary>Номер идущего основного шага; -1 — запуск стоит или доигрывают только фоновые шаги.</summary>
        public int CurrentActionIndex { get; private set; } = -1;


        // ── Запуск ───────────────────────────────────────────────────────

        /// <summary>False — уже идёт, уже отработал разовый или условия не сошлись.</summary>
        public bool TryRun(GameObject instigator)
        {
            if (IsRunning || (owner.ListRunsOnce && HasRun))
                return false;

            var run = new ScenarioRun(owner, instigator);

            if (!ScenarioConditionList.AreMet(owner.ListConditions, owner.ListConditionsMode, run))
            {
                Report(run, Time.time, ScenarioRunOutcome.ConditionsNotMet);
                return false;
            }

            var coroutines = AppContext.Current?.Coroutines;

            if (coroutines == null)
            {
                FLog.Error(owner, ("Scenario did not start: no AppContext — ", FLogColor.Bad), (owner.name, FLogColor.Value));
                return false;
            }

            HasRun = true;
            IsRunning = true;
            runNumber++;
            currentRun = run;
            runStartTime = Time.time;

            runToken = coroutines.Run(PerformRun(run, runNumber));

            return true;
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
            owner.HeldEffects.ReleaseTakenBy(run);
            ReportFailedAction(run, failedIndex);
            FinishRun(number, ScenarioRunOutcome.Failed, failedIndex, run.Failure.Message);
        }

        private IEnumerator PerformActions(ScenarioRun run)
        {
            var actions = owner.ListActions;

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
            return run.HasFailedInBackground ? owner.ListActions.IndexOf(run.FailedAction) : CurrentActionIndex;
        }

        private void ReportFailedAction(ScenarioRun run, int failedIndex)
        {
            var stepText = failedIndex >= 0 ? $" at step {failedIndex + 1}" : run.HasFailedInBackground ? " in a background step" : "";
            var blockText = run.FailedAction != null ? $" ({run.FailedAction.GetType().Name})" : "";

            FLog.Error(owner, ("Scenario ", FLogColor.Bad), (owner.name, FLogColor.Value),
                ($" failed{stepText}{blockText}: ", FLogColor.Bad),
                (run.Failure.Message, FLogColor.Muted));

            Debug.LogException(run.Failure, owner);
        }

        private void FinishRun(int number, ScenarioRunOutcome outcome, int failedActionIndex = -1, string failureMessage = null)
        {
            if (number != runNumber)
                return;

            IsRunning = false;
            CurrentActionIndex = -1;

            Report(currentRun, runStartTime, outcome, failedActionIndex, failureMessage);
        }


        // ── Обрыв и подсветка ────────────────────────────────────────────

        public void Stop()
        {
            if (!IsRunning)
                return;

            runNumber++;
            IsRunning = false;
            CurrentActionIndex = -1;

            currentRun.Stop();
            runToken.Stop();
            Report(currentRun, runStartTime, ScenarioRunOutcome.Stopped);
        }


        /// <summary>Шаг сейчас идёт — основной, вложенный или фоновый. Для подсветки в инспекторе.</summary>
        public bool IsPerforming(ScenarioAction action)
        {
            return IsRunning && currentRun.IsPerforming(action);
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private void Report(ScenarioRun run, float startTime, ScenarioRunOutcome outcome,
            int failedActionIndex = -1, string failureMessage = null)
        {
            var instigatorName = run.Instigator != null ? run.Instigator.name : null;

            owner.ReportRun(new ScenarioRunRecord(owner, instigatorName, startTime, Time.time, outcome,
                failedActionIndex, failureMessage));
        }
    }
}
