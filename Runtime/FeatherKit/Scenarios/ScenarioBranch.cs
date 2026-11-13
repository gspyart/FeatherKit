using System;
using System.Collections;
using System.Collections.Generic;
using FeatherKit.Coroutines;
using AppContext = FeatherKit.Contexts.AppContext;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Отдельно идущая ветка запуска: нода графа, основные шаги сценария, ветка «In Parallel» или фоновый шаг.
    /// - идёт своей корутиной на общем раннере, пока её или весь запуск не оборвали
    /// - ошибку шага не роняет, а отдаёт запуску — тот обрывает весь сценарий
    /// - помнит свою ноду, идущие шаги и кусок запуска, к которому принадлежит, — для обрыва и подсветки
    /// - Stop обрывает ветку и её ветки «In Parallel»; фоновые шаги, пущенные из неё, идут дальше
    /// </summary>
    public class ScenarioBranch
    {
        private readonly ScenarioRun run;
        private readonly ScenarioBranch parent;
        private readonly List<ScenarioBranch> parallelBranches = new List<ScenarioBranch>();

        // Снаружи внутрь: последний — самый вложенный из идущих шагов.
        private readonly List<ScenarioAction> performingActions = new List<ScenarioAction>();

        // Ветка, пущенная проводом: до старта её кусок — само срабатывание, на старте — ближайший, кого ждут.
        private readonly bool isFiringRoot;

        private List<Action> finishedCallbacks;
        private CoroutineToken token;


        /// <param name="parent">Из какой ветки выросла: фоновый шаг или «In Parallel». У веток проводов — null.</param>
        /// <param name="firing">Срабатывание выхода, пустившее ветку по проводу; null — ветка в куске родителя.</param>
        internal ScenarioBranch(ScenarioRun run, ScenarioBranch parent, bool isBackground, ScenarioFiring firing = null)
        {
            this.run = run;
            this.parent = parent;
            IsBackground = isBackground;
            Node = parent?.Node;
            Firing = firing ?? parent?.Firing;
            isFiringRoot = firing != null;

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

        /// <summary>Нода графа, которую исполняет ветка; фоновые шаги и «In Parallel» берут ноду родителя. Null — старый список шагов.</summary>
        internal ScenarioNode Node { get; set; }

        /// <summary>Ближайший кусок запуска, которого кто-то ждёт или может оборвать. Null — вне таких кусков.</summary>
        internal ScenarioFiring Firing { get; private set; }

        /// <summary>Ветка сейчас делает свой ход: пущенное ею по проводам начнётся, когда ход кончится.</summary>
        internal bool IsTakingStep { get; private set; }

        private ScenarioAction InnermostAction => performingActions.Count > 0 ? performingActions[performingActions.Count - 1] : null;


        // ── Ход ветки ────────────────────────────────────────────────────

        internal void Begin(IEnumerator steps)
        {
            if (IsFinished)
                return;

            // Срабатывание, которого никто не ждёт, в цепочке не держим: петля по проводам растила бы её каждым витком.
            if (isFiringRoot && !Firing.IsWatched)
                Firing = Firing.OuterFiring;

            token = AppContext.Current.Coroutines.Run(Perform(ScenarioSteps.Flatten(steps)));

            if (IsFinished)
                token.Stop();
        }

        // После каждого хода начинаются ветки, пущенные им по проводам: к этому моменту нода решила, ждёт ли их.
        private IEnumerator Perform(IEnumerator steps)
        {
            while (!IsFinished && !run.IsStopped)
            {
                var hasNextStep = TakeStep(steps);
                run.StartPendingBranches();

                if (!hasNextStep || IsFinished)
                    break;

                yield return steps.Current;
            }

            Finish();
        }

        // На время хода ветка — активная у запуска: так её шаги и ноды знают, где они идут.
        private bool TakeStep(IEnumerator steps)
        {
            var previousBranch = run.ActiveBranch;
            run.ActiveBranch = this;
            IsTakingStep = true;

            try
            {
                return steps.MoveNext();
            }
            catch (Exception exception)
            {
                run.Fail(exception, InnermostAction, this);
                return false;
            }
            finally
            {
                IsTakingStep = false;
                run.ActiveBranch = previousBranch;
            }
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


        /// <summary>Позвать, когда ветка кончится любым путём: оборванная корутина не доходит до finally ноды.</summary>
        internal void CallWhenFinished(Action callback)
        {
            if (IsFinished)
            {
                callback();
                return;
            }

            finishedCallbacks ??= new List<Action>();
            finishedCallbacks.Add(callback);
        }


        // Ветка, оборванная посреди своего же хода, приходит сюда второй раз — второй раз молчим.
        private void Finish()
        {
            if (IsFinished)
                return;

            IsFinished = true;
            performingActions.Clear();

            if (finishedCallbacks == null)
                return;

            var callbacks = finishedCallbacks;
            finishedCallbacks = null;

            foreach (var callback in callbacks)
                callback();
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


        // ── Происхождение ────────────────────────────────────────────────

        /// <summary>Эта ветка — та самая или выросла из неё: фоновые шаги и «In Parallel».</summary>
        internal bool IsInside(ScenarioBranch branch)
        {
            for (var current = this; current != null; current = current.parent)
            {
                if (current == branch)
                    return true;
            }

            return false;
        }

        /// <summary>Ветка выросла из этого куска запуска. Цепочка кусков длиной во вложенность ожиданий, а не в число витков.</summary>
        internal bool IsInside(ScenarioFiring firing)
        {
            for (var current = Firing; current != null; current = current.OuterFiring)
            {
                if (current == firing)
                    return true;
            }

            return false;
        }
    }
}
