using System.Collections;
using System.Collections.Generic;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Очередь веток, пущенных проводами и ещё не начатых. Часть ScenarioRun.
    /// - ветка ждёт, пока нода, пустившая её, доделает ход: к этому моменту нода решила, ждёт ли кусок
    /// - очередь обходится по порядку, а не вглубь: цепочка мгновенных нод не растит стек
    /// - кусок, который ждут прямо в ходе, начинается сразу — мгновенные ветки кончаются в том же кадре
    /// </summary>
    internal class ScenarioBranchStarts
    {
        // Ожидание внутри ожидания начинает ветки само, но не глубже — дальше их начнёт внешний обход очереди.
        private const int MaxNestedEagerStarts = 8;

        private readonly ScenarioRun run;
        private readonly List<PendingBranchStart> pendingStarts = new List<PendingBranchStart>();

        private bool isStarting;
        private int eagerStartDepth;


        public ScenarioBranchStarts(ScenarioRun run)
        {
            this.run = run;
        }


        /// <param name="firingBranch">Ветка, чья нода сработала; null — сработала When, вне веток.</param>
        public void Add(ScenarioBranch branch, ScenarioBranch firingBranch, IEnumerator steps)
        {
            pendingStarts.Add(new PendingBranchStart(branch, firingBranch, steps));
        }

        public void Clear()
        {
            pendingStarts.Clear();
        }


        // ── Обход очереди ────────────────────────────────────────────────

        /// <summary>Начать всё, чья нода уже доделала ход. Внутри идущего обхода — ничего: его продолжит внешний.</summary>
        public void StartSettled()
        {
            if (isStarting)
                return;

            isStarting = true;

            try
            {
                StartWhere(eagerFiring: null);
            }
            finally
            {
                isStarting = false;
            }
        }

        public void StartEagerly(ScenarioFiring firing)
        {
            if (eagerStartDepth >= MaxNestedEagerStarts)
                return;

            var wasStarting = isStarting;
            isStarting = true;
            eagerStartDepth++;

            try
            {
                StartWhere(firing);
            }
            finally
            {
                eagerStartDepth--;
                isStarting = wasStarting;
            }
        }

        // Ветка ждёт, пока пустившая её нода в ходе; при раннем старте — только ветки ожидаемого куска.
        private void StartWhere(ScenarioFiring eagerFiring)
        {
            var index = 0;

            while (index < pendingStarts.Count)
            {
                var start = pendingStarts[index];

                if (run.IsStopped || start.Branch.IsFinished)
                {
                    pendingStarts.RemoveAt(index);
                    start.Branch.Stop();
                    continue;
                }

                var isSettled = start.FiringBranch == null || !start.FiringBranch.IsTakingStep;
                var canStart = eagerFiring == null
                    ? isSettled
                    : start.Branch.IsInside(eagerFiring) && (isSettled || start.Branch.Firing == eagerFiring);

                if (!canStart)
                {
                    index++;
                    continue;
                }

                pendingStarts.RemoveAt(index);
                start.Branch.Begin(start.Steps);
            }
        }


        /// <summary>Ветка провода ждёт начала: кто её пустил и с чего она начнётся.</summary>
        private readonly struct PendingBranchStart
        {
            public PendingBranchStart(ScenarioBranch branch, ScenarioBranch firingBranch, IEnumerator steps)
            {
                Branch = branch;
                FiringBranch = firingBranch;
                Steps = steps;
            }


            public ScenarioBranch Branch { get; }

            public ScenarioBranch FiringBranch { get; }

            public IEnumerator Steps { get; }
        }
    }
}
