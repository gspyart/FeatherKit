using System;
using System.Collections;
using System.Collections.Generic;
using FeatherKit.Coroutines;
using AppContext = FeatherKit.Contexts.AppContext;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Отдельно идущая ветка запуска: основные шаги сценария, ветка «In Parallel» или фоновый шаг.
    /// - идёт своей корутиной на общем раннере, пока её или весь запуск не оборвали
    /// - ошибку шага не роняет, а отдаёт запуску — тот обрывает весь сценарий
    /// - помнит свои идущие шаги — по ним инспектор подсвечивает блоки
    /// - Stop обрывает ветку и её ветки «In Parallel»; фоновые шаги, пущенные из неё, идут дальше
    /// </summary>
    public class ScenarioBranch
    {
        private readonly ScenarioRun run;
        private readonly ScenarioBranch parent;
        private readonly List<ScenarioBranch> parallelBranches = new List<ScenarioBranch>();

        // Снаружи внутрь: последний — самый вложенный из идущих шагов.
        private readonly List<ScenarioAction> performingActions = new List<ScenarioAction>();

        private CoroutineToken token;


        internal ScenarioBranch(ScenarioRun run, ScenarioBranch parent, bool isBackground)
        {
            this.run = run;
            this.parent = parent;
            IsBackground = isBackground;

            if (parent != null && !isBackground)
            {
                parent.parallelBranches.RemoveAll(branch => branch.IsFinished);
                parent.parallelBranches.Add(this);
            }
        }


        /// <summary>Фоновый шаг — пущен без «Wait Until Done».</summary>
        public bool IsBackground { get; }

        /// <summary>Ветка дошла до конца, оборвана или упала.</summary>
        public bool IsFinished { get; private set; }

        /// <summary>Эта ветка — фоновая или выросла из фоновой.</summary>
        internal bool IsInBackground => IsBackground || (parent != null && parent.IsInBackground);

        private ScenarioAction InnermostAction => performingActions.Count > 0 ? performingActions[performingActions.Count - 1] : null;


        // ── Ход ветки ────────────────────────────────────────────────────

        internal void Begin(IEnumerator steps)
        {
            token = AppContext.Current.Coroutines.Run(Perform(ScenarioSteps.Flatten(steps)));
        }

        // На время каждого хода ветка — активная у запуска: так её шаги знают, где они идут.
        private IEnumerator Perform(IEnumerator steps)
        {
            while (!IsFinished && !run.IsStopped)
            {
                bool hasNextStep;
                var previousBranch = run.ActiveBranch;
                run.ActiveBranch = this;

                try
                {
                    hasNextStep = steps.MoveNext();
                }
                catch (Exception exception)
                {
                    run.Fail(exception, InnermostAction, this);
                    hasNextStep = false;
                }
                finally
                {
                    run.ActiveBranch = previousBranch;
                }

                if (!hasNextStep)
                    break;

                yield return steps.Current;
            }

            Finish();
        }


        /// <summary>Оборвать ветку и её ветки «In Parallel». Дошедшую до конца — ничего не делает.</summary>
        public void Stop()
        {
            if (IsFinished)
                return;

            token.Stop();
            Finish();

            foreach (var branch in parallelBranches.ToArray())
                branch.Stop();
        }

        private void Finish()
        {
            IsFinished = true;
            performingActions.Clear();
        }


        // ── Идущие шаги ──────────────────────────────────────────────────

        internal void MarkPerforming(ScenarioAction action)
        {
            performingActions.Add(action);
        }

        internal void UnmarkPerforming(ScenarioAction action)
        {
            var index = performingActions.LastIndexOf(action);

            if (index >= 0)
                performingActions.RemoveAt(index);
        }

        internal bool IsPerforming(ScenarioAction action)
        {
            return performingActions.Contains(action);
        }


        /// <summary>Эта ветка — та самая или выросла из неё.</summary>
        internal bool IsInside(ScenarioBranch branch)
        {
            for (var current = this; current != null; current = current.parent)
            {
                if (current == branch)
                    return true;
            }

            return false;
        }
    }
}
