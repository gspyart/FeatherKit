using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Один запуск сценария — то, что получает каждый его блок и нода.
    /// - чей это сценарий, на каком объекте и кто его вызвал: «Caller»; может не быть никого
    /// - его ветки: ноды графа, основные шаги, «In Parallel», фоновые шаги; запуск идёт, пока идёт хоть одна
    /// - пускает провода сработавшего выхода после хода ноды; мгновенные ноды проходят в том же кадре
    /// - первая ошибка в любой ветке обрывает весь запуск; берёт долгие эффекты под присмотр графа
    /// </summary>
    public class ScenarioRun
    {
        // На все запуски разом: петля может идти и через несколько запусков, зовущих друг друга.
        private const int MaxInstantTransitionsPerFrame = 10000;

        private static int transitionsFrame = -1;
        private static int transitionsInFrame;

        private readonly List<ScenarioBranch> branches = new List<ScenarioBranch>();
        private readonly ScenarioBranchStarts branchStarts;


        public ScenarioRun(Scenario scenario, GameObject instigator)
        {
            branchStarts = new ScenarioBranchStarts(this);
            Scenario = scenario;
            Instigator = instigator;
        }

        internal ScenarioRun(Scenario scenario, GameObject instigator, ScenarioWhenNode startNode) : this(scenario, instigator)
        {
            StartNode = startNode;
        }


        public Scenario Scenario { get; }

        /// <summary>Кто вызвал сценарий. Null — никто: старт сцены, кнопка, Timeline.</summary>
        public GameObject Instigator { get; }

        public GameObject Owner => Scenario.gameObject;

        /// <summary>Запуск оборван — остановили или шаг упал: ветки заканчивают, не начиная новых шагов.</summary>
        public bool IsStopped { get; private set; }

        /// <summary>Первая ошибка шага в любой ветке; null — ошибок не было.</summary>
        public Exception Failure { get; private set; }

        /// <summary>Шаг, на котором случилась ошибка, — самый вложенный из идущих. Null — ошибка вне шага.</summary>
        public ScenarioAction FailedAction { get; private set; }

        /// <summary>Ошибка случилась в фоновом шаге или внутри него.</summary>
        public bool HasFailedInBackground { get; private set; }

        /// <summary>Идёт хоть одна ветка: нода, основные шаги, «In Parallel» или фоновый шаг.</summary>
        public bool HasRunningBranches
        {
            get
            {
                foreach (var branch in branches)
                {
                    if (!branch.IsFinished)
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Идёт хоть один фоновый шаг. Ветку, которая спрашивает, и тех, из кого она выросла, не считаем:
        /// иначе «дождаться фоновых» внутри фонового шага ждал бы сам себя.
        /// </summary>
        public bool HasBackgroundSteps => branches.Exists(branch => branch.IsBackground && !branch.IsFinished
                                                                    && (ActiveBranch == null || !ActiveBranch.IsInside(branch)));

        /// <summary>Нода When, с которой начался запуск графа. Null — запуск старого списка шагов.</summary>
        internal ScenarioWhenNode StartNode { get; }

        /// <summary>Нода графа, в ветке которой случилась ошибка. Null — ошибок не было или она вне графа.</summary>
        internal ScenarioNode FailedNode { get; private set; }

        /// <summary>Запуск кончился и записан в журнал — так Run Scenario узнаёт, что дождался.</summary>
        internal bool IsCompleted { get; set; }

        /// <summary>Ветка, чей шаг выполняется прямо сейчас. Ставит сама ветка на время своего хода.</summary>
        internal ScenarioBranch ActiveBranch { get; set; }

        /// <summary>Запуск оборван или оборвана ветка, чей шаг идёт: новых шагов, проводов и фоновых веток не начинать.</summary>
        internal bool IsActiveBranchStopped => IsStopped || (ActiveBranch != null && ActiveBranch.IsFinished);


        public T ComponentOrOwners<T>(T fieldValue) where T : Component
        {
            return fieldValue != null ? fieldValue : Owner.GetComponent<T>();
        }


        // ── Шаги ─────────────────────────────────────────────────────────

        /// <summary>
        /// Выполнить шаг: дождаться его или, если он не ждёт конца, пустить фоновой веткой.
        /// Так шаги зовут и сценарий, и вложенные столбики — блоку не нужно самому разбирать «Wait Until Done».
        /// </summary>
        public IEnumerator PerformStep(ScenarioAction action)
        {
            if (IsActiveBranchStopped)
                yield break;

            if (!action.WaitsForEnd)
            {
                StartBranch(PerformMarked(action), isBackground: true);
                yield break;
            }

            yield return PerformMarked(action);
        }

        // Пока шаг идёт, он отмечен в своей ветке: по отметкам инспектор подсвечивает блоки.
        private IEnumerator PerformMarked(ScenarioAction action)
        {
            var branch = ActiveBranch;

            branch?.MarkPerforming(action);
            yield return action.Perform(this);
            branch?.UnmarkPerforming(action);
        }


        /// <summary>Шаг сейчас идёт в какой-нибудь ветке запуска — для подсветки в инспекторе.</summary>
        public bool IsPerforming(ScenarioAction action)
        {
            foreach (var branch in branches)
            {
                if (branch.IsPerforming(action))
                    return true;
            }

            return false;
        }

        /// <summary>Нода графа сейчас идёт в какой-нибудь ветке запуска — для подсветки на холсте.</summary>
        internal bool IsPerforming(int nodeId)
        {
            foreach (var branch in branches)
            {
                if (!branch.IsFinished && branch.Node != null && branch.Node.Id == nodeId)
                    return true;
            }

            return false;
        }


        // ── Ветки ────────────────────────────────────────────────────────

        /// <summary>Пустить шаги отдельной веткой — как «In Parallel». Ветку обрывает её Stop, обрыв запуска или ошибка.</summary>
        public ScenarioBranch StartBranch(IEnumerator steps)
        {
            return StartBranch(steps, isBackground: false);
        }

        private ScenarioBranch StartBranch(IEnumerator steps, bool isBackground)
        {
            var branch = new ScenarioBranch(this, ActiveBranch, isBackground);
            AddBranch(branch);
            branch.Begin(steps);

            return branch;
        }

        // Дошедшие ветки выкидываем: повтор без конца иначе копил бы их весь запуск.
        private void AddBranch(ScenarioBranch branch)
        {
            branches.RemoveAll(existing => existing.IsFinished);
            branches.Add(branch);
        }


        /// <summary>Начать ветки проводов, чья нода уже доделала ход.</summary>
        internal void StartPendingBranches()
        {
            branchStarts.StartSettled();
        }

        /// <summary>Кусок ждут прямо в ходе ноды: начать его ветки сейчас, чтобы мгновенные кончились в этом же кадре.</summary>
        internal void StartPendingBranchesEagerly(ScenarioFiring firing)
        {
            branchStarts.StartEagerly(firing);
        }


        // ── Провода графа ────────────────────────────────────────────────

        /// <summary>
        /// Сработал выход ноды: по ветке на каждый провод. Ветки начнутся после хода ноды —
        /// кусок, который нода хочет ждать или обрывать, она смотрит в том же ходе, где позвала Fire.
        /// </summary>
        internal ScenarioFiring Fire(ScenarioNode node, ScenarioOutput output)
        {
            var firing = new ScenarioFiring(this, node, output, ActiveBranch?.Firing);

            // Оборванная ветка доигрывает свой ход до yield — ничего нового она пустить уже не должна.
            if (IsActiveBranchStopped)
                return firing;

            Scenario.ReportWireFired(node.Id, output.Id);

            foreach (var target in Scenario.Wiring.TargetsOf(node.Id, output.Id))
            {
                CountInstantTransition();

                var branch = new ScenarioBranch(this, null, isBackground: false, firing) { Node = target.Node };
                AddBranch(branch);
                firing.CountRootBranch();
                branchStarts.Add(branch, ActiveBranch, EnterNode(target));
            }

            if (ActiveBranch == null)
                StartPendingBranches();

            return firing;
        }

        private IEnumerator EnterNode(ScenarioWireTarget target)
        {
            return target.Node switch
            {
                ScenarioStepsNode stepsNode => stepsNode.Enter(this),
                ScenarioFlowNode flowNode => flowNode.Enter(this, target.Input),
                _ => throw new InvalidOperationException($"Node “{target.Node.Title}” cannot be entered by a wire")
            };
        }


        /// <summary>Переход без ожидания — провод или виток повтора. Слишком много за кадр на все запуски — вечный цикл.</summary>
        internal void CountInstantTransition()
        {
            if (transitionsFrame != Time.frameCount)
            {
                transitionsFrame = Time.frameCount;
                transitionsInFrame = 0;
            }

            if (++transitionsInFrame <= MaxInstantTransitionsPerFrame)
                return;

            // Петлю обрывает эта ошибка; запуски после неё в том же кадре начинают счёт заново.
            transitionsInFrame = 0;

            throw new InvalidOperationException(
                    $"Loop without waiting: more than {MaxInstantTransitionsPerFrame} node transitions in one frame. "
                    + "Put a Wait or Wait For into the loop");
        }


        // ── Долгие эффекты ───────────────────────────────────────────────

        /// <summary>Взять долгий эффект под присмотр графа: обрыв куска и остановка отпустят его плавно.</summary>
        public void Hold(IScenarioHeldEffect effect)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));

            if (IsActiveBranchStopped)
            {
                effect.Release();
                return;
            }

            Scenario.HeldEffects.Hold(effect, this, ActiveBranch);
        }


        // ── Обрыв ────────────────────────────────────────────────────────

        /// <summary>Оборвать все ветки. Звук, Timeline и другой сценарий, начатые шагами, доигрывают сами.</summary>
        internal void Stop()
        {
            IsStopped = true;

            foreach (var branch in branches.ToArray())
                branch.Stop();

            branchStarts.Clear();
        }

        /// <summary>Шаг упал: запомнить первую ошибку. Ветки заканчивают сами, сценарий оборвёт остальное.</summary>
        internal void Fail(Exception exception, ScenarioAction failedAction, ScenarioBranch failedBranch)
        {
            if (Failure != null)
                return;

            Failure = exception;
            FailedAction = failedAction;
            FailedNode = failedBranch.Node;
            HasFailedInBackground = failedBranch.IsInBackground;
            IsStopped = true;
        }

        /// <summary>Ошибка вне ветки — в условиях ноды When, до того как запуск пустил хоть одну ветку.</summary>
        internal void FailAtNode(Exception exception, ScenarioNode failedNode)
        {
            if (Failure != null)
                return;

            Failure = exception;
            FailedNode = failedNode;
            IsStopped = true;
        }


        /// <summary>Идёт хоть одна ветка этого куска, фоновые тоже; ещё не начатые — тоже идут.</summary>
        internal bool HasRunningBranchesInside(ScenarioFiring firing)
        {
            foreach (var branch in branches)
            {
                if (!branch.IsFinished && branch.IsInside(firing))
                    return true;
            }

            return false;
        }

        /// <summary>Оборвать всё, что выросло из куска, включая фоновые и не начатые, и отпустить взятые там долгие эффекты.</summary>
        internal void CancelInside(ScenarioFiring firing)
        {
            foreach (var branch in branches.ToArray())
            {
                if (!branch.IsFinished && branch.IsInside(firing))
                    branch.Stop();
            }

            Scenario.HeldEffects.ReleaseInside(firing);
        }


        // Счётчик общий на все запуски и статический: без перезагрузки домена дожил бы до следующей игры.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            transitionsFrame = -1;
            transitionsInFrame = 0;
        }
    }
}
