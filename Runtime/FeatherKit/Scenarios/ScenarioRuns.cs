using System;
using System.Collections;
using System.Collections.Generic;
using FeatherKit.Coroutines;
using FeatherKit.Logging;
using UnityEngine;
using AppContext = FeatherKit.Contexts.AppContext;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Идущие запуски графа сценария — с какой ноды When каждый начался. Часть Scenario.
    /// - сработала When: Only Once, пропуск идущей, условия, Restart → выход Then или Not Met
    /// - следит за запуском до конца и сообщает в журнал, чем он кончился, с нодой
    /// - останавливает запуск или все разом и отпускает их долгие эффекты
    /// - не даёт сценариям запускать друг друга без ожидания бесконечно вглубь
    /// </summary>
    public class ScenarioRuns
    {
        // Глубже — сценарии зовут друг друга по кругу без ожидания: стек кончился бы раньше любого счётчика.
        private const int MaxNestedStarts = 16;

        // Кто сейчас начинается внутри чьего начала — на все сценарии разом.
        private static readonly List<string> NestedStartNames = new List<string>();

        private readonly Scenario owner;
        private readonly List<TrackedGraphRun> trackedRuns = new List<TrackedGraphRun>();
        private readonly HashSet<int> firedOnceNodeIds = new HashSet<int>();

        // Куда складывать начатые запуски, пока сценарий зовут: так Run Scenario ждёт только свои.
        private List<ScenarioRun> startedRunsCollector;


        public ScenarioRuns(Scenario owner)
        {
            this.owner = owner;
        }


        public bool HasRunningRuns => trackedRuns.Count > 0;

        /// <summary>Хоть раз When пошла в Then.</summary>
        public bool HasStarted { get; private set; }


        // ── Запуск ───────────────────────────────────────────────────────

        /// <summary>Событие ноды When сработало. Вернёт начатый запуск — в Then или Not Met; null — запуска нет.</summary>
        public ScenarioRun StartFrom(ScenarioWhenNode whenNode, GameObject instigator)
        {
            if (whenNode.RunsOnlyOnce && firedOnceNodeIds.Contains(whenNode.Id))
                return null;

            // Пропуск решаем до условий: Every Frame не должен каждый кадр гонять проверки и тратить случайное.
            if (whenNode.RerunRule == ScenarioRerunRule.SkipIfRunning && HasRunsFrom(whenNode))
                return null;

            var coroutines = AppContext.Current?.Coroutines;

            if (coroutines == null)
            {
                FLog.Error(owner, ("Scenario did not start: no AppContext — ", FLogColor.Bad), (owner.name, FLogColor.Value));
                return null;
            }

            var trackedRun = new TrackedGraphRun(new ScenarioRun(owner, instigator, whenNode), Time.time);

            if (NestedStartNames.Count >= MaxNestedStarts)
            {
                RefuseNestedStart(trackedRun);
                return null;
            }

            NestedStartNames.Add(owner.name);

            try
            {
                return Start(whenNode, trackedRun, coroutines) ? trackedRun.Run : null;
            }
            finally
            {
                NestedStartNames.RemoveAt(NestedStartNames.Count - 1);
            }
        }

        // Restart — после условий: идущий запуск не обрывается ради несошедшихся условий.
        private bool Start(ScenarioWhenNode whenNode, TrackedGraphRun trackedRun, CoroutineRunner coroutines)
        {
            if (!AreConditionsMet(whenNode, trackedRun))
                return false;

            if (whenNode.RerunRule == ScenarioRerunRule.Restart && trackedRun.HasMetConditions)
                StopRunsFrom(whenNode);

            if (trackedRun.HasMetConditions)
            {
                HasStarted = true;

                if (whenNode.RunsOnlyOnce)
                    firedOnceNodeIds.Add(whenNode.Id);
            }
            else
            {
                Report(trackedRun, ScenarioRunOutcome.ConditionsNotMet);
            }

            trackedRuns.Add(trackedRun);
            startedRunsCollector?.Add(trackedRun.Run);
            trackedRun.Run.Fire(whenNode, trackedRun.HasMetConditions ? ScenarioWhenNode.Then : ScenarioWhenNode.NotMet);

            if (!trackedRun.Run.IsCompleted)
                coroutines.Run(Supervise(trackedRun));

            return true;
        }

        // Упавшее условие — ошибка запуска, как упавший шаг: в журнал, а не наружу в событие.
        private bool AreConditionsMet(ScenarioWhenNode whenNode, TrackedGraphRun trackedRun)
        {
            try
            {
                trackedRun.HasMetConditions = whenNode.Conditions.IsMet(trackedRun.Run);
                return true;
            }
            catch (Exception exception)
            {
                trackedRun.Run.FailAtNode(exception, whenNode);
                Complete(trackedRun);
                return false;
            }
        }

        // Запуск от ноды идёт — в Then или в Not Met: для Skip If Running он тоже занимает ноду.
        private bool HasRunsFrom(ScenarioWhenNode whenNode)
        {
            foreach (var trackedRun in trackedRuns)
            {
                if (trackedRun.Run.StartNode == whenNode)
                    return true;
            }

            return false;
        }

        private void StopRunsFrom(ScenarioWhenNode whenNode)
        {
            foreach (var trackedRun in trackedRuns.ToArray())
            {
                if (trackedRun.Run.StartNode == whenNode)
                    Stop(trackedRun.Run);
            }
        }


        // Отказ — тоже запуск в журнале: иначе петля между сценариями молча обрывалась бы на полпути.
        private void RefuseNestedStart(TrackedGraphRun trackedRun)
        {
            var message = $"Scenario {owner.name} starts scenarios without waiting: "
                          + string.Join(" → ", NestedStartNames) + $" → {owner.name}";

            FLog.Error(owner, (message, FLogColor.Bad));
            trackedRun.Run.FailAtNode(new InvalidOperationException(message), trackedRun.Run.StartNode);
            trackedRun.Run.IsCompleted = true;
            Report(trackedRun, ScenarioRunOutcome.Failed, message);
        }


        /// <summary>Пока зовут сценарий, сложить в список запуски, начатые этим вызовом.</summary>
        public void CollectStartedRuns(List<ScenarioRun> startedRuns, Action call)
        {
            var previousCollector = startedRunsCollector;
            startedRunsCollector = startedRuns;

            try
            {
                call();
            }
            finally
            {
                startedRunsCollector = previousCollector;
            }
        }


        // Запуск кончился, когда кончились все его ветки, его оборвали или он упал.
        private IEnumerator Supervise(TrackedGraphRun trackedRun)
        {
            var run = trackedRun.Run;

            while (!run.IsCompleted && !run.IsStopped && run.HasRunningBranches)
                yield return null;

            Complete(trackedRun);
        }


        // ── Остановка ────────────────────────────────────────────────────

        /// <summary>Оборвать запуск и отпустить его долгие эффекты: нода Stop This Run, правило Restart.</summary>
        public void Stop(ScenarioRun run)
        {
            run.Stop();

            var trackedRun = FindTracked(run);

            if (trackedRun != null)
                Complete(trackedRun);
            else
                owner.HeldEffects.ReleaseTakenBy(run);
        }

        public void StopAll()
        {
            foreach (var trackedRun in trackedRuns.ToArray())
                Stop(trackedRun.Run);
        }

        private TrackedGraphRun FindTracked(ScenarioRun run)
        {
            foreach (var trackedRun in trackedRuns)
            {
                if (trackedRun.Run == run)
                    return trackedRun;
            }

            return null;
        }


        // ── Подсветка ────────────────────────────────────────────────────

        public bool IsPerforming(int nodeId)
        {
            foreach (var trackedRun in trackedRuns)
            {
                if (trackedRun.Run.IsPerforming(nodeId))
                    return true;
            }

            return false;
        }


        // ── Конец запуска ────────────────────────────────────────────────

        // Оборванный или упавший запуск отпускает свои эффекты; доигравший сам — оставляет до своей ноды Stop.
        private void Complete(TrackedGraphRun trackedRun)
        {
            var run = trackedRun.Run;

            if (run.IsCompleted)
                return;

            run.IsCompleted = true;
            trackedRuns.Remove(trackedRun);

            if (run.Failure != null)
            {
                run.Stop();
                owner.HeldEffects.ReleaseTakenBy(run);
                ReportFailure(trackedRun);
            }
            else if (run.IsStopped)
            {
                owner.HeldEffects.ReleaseTakenBy(run);
                Report(trackedRun, ScenarioRunOutcome.Stopped);
            }
            else if (trackedRun.HasMetConditions)
            {
                Report(trackedRun, ScenarioRunOutcome.Finished);
            }
        }

        private void ReportFailure(TrackedGraphRun trackedRun)
        {
            var run = trackedRun.Run;
            var nodeText = run.FailedNode != null ? $" at node “{run.FailedNode.Title}”" : "";
            var blockText = run.FailedAction != null ? $" ({run.FailedAction.GetType().Name})" : "";

            FLog.Error(owner, ("Scenario ", FLogColor.Bad), (owner.name, FLogColor.Value),
                ($" failed{nodeText}{blockText}: ", FLogColor.Bad),
                (run.Failure.Message, FLogColor.Muted));

            Debug.LogException(run.Failure, owner);

            Report(trackedRun, ScenarioRunOutcome.Failed, run.Failure.Message);
        }

        private void Report(TrackedGraphRun trackedRun, ScenarioRunOutcome outcome, string failureMessage = null)
        {
            var run = trackedRun.Run;
            var instigatorName = run.Instigator != null ? run.Instigator.name : null;
            var failedNodeId = run.FailedNode != null ? run.FailedNode.Id : -1;

            owner.ReportRun(new ScenarioRunRecord(owner, instigatorName, trackedRun.StartTime, Time.time, outcome,
                failureMessage: failureMessage, startNodeId: run.StartNode.Id, failedNodeId: failedNodeId));
        }


        // Счётчик вложенности статический: без перезагрузки домена оборванный запуск прошлой игры оставил бы его ненулевым.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            NestedStartNames.Clear();
        }


        /// <summary>Запуск под присмотром: когда начался и пошёл ли в Then.</summary>
        private class TrackedGraphRun
        {
            public TrackedGraphRun(ScenarioRun run, float startTime)
            {
                Run = run;
                StartTime = startTime;
            }


            public ScenarioRun Run { get; }

            public float StartTime { get; }

            /// <summary>Пошёл в Then; иначе — в Not Met, и доигравший такой запуск в журнал не пишется.</summary>
            public bool HasMetConditions { get; set; }
        }
    }
}
