using System.Collections;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Одно срабатывание выхода ноды со всем, что из него выросло по проводам, — обрываемый кусок запуска.
    /// - идёт, пока идёт хоть одна его ветка, фоновые тоже
    /// - его можно дождаться или оборвать вместе с потомками и долгими эффектами, взятыми внутри
    /// - смотреть кусок нода обязана в том же ходе, где позвала Fire: потом его ветки уже не узнать
    /// </summary>
    public class ScenarioFiring
    {
        private int rootBranchCount;


        internal ScenarioFiring(ScenarioRun run, ScenarioNode node, ScenarioOutput output, ScenarioFiring outerFiring)
        {
            Run = run;
            Node = node;
            Output = output;
            OuterFiring = outerFiring;
        }


        public ScenarioRun Run { get; }

        /// <summary>Нода, чей выход сработал.</summary>
        public ScenarioNode Node { get; }

        public ScenarioOutput Output { get; }

        /// <summary>Идёт хоть одна ветка этого происхождения; ещё не начатые — тоже.</summary>
        public bool IsRunning
        {
            get
            {
                IsWatched = true;

                return rootBranchCount > 0 && Run.HasRunningBranchesInside(this);
            }
        }

        /// <summary>У выхода были провода — было что пускать.</summary>
        internal bool HasBranches => rootBranchCount > 0;

        /// <summary>Кусок ждут или могут оборвать: только такие куски ветки помнят как своё происхождение.</summary>
        internal bool IsWatched { get; private set; }

        /// <summary>Ближайший наблюдаемый кусок, в котором шла сработавшая нода. Null — снаружи всех.</summary>
        internal ScenarioFiring OuterFiring { get; }


        /// <summary>Дождаться всего выросшего. Мгновенные ветки начинаются сразу и кончаются в этом же кадре.</summary>
        public IEnumerator WaitUntilDone()
        {
            IsWatched = true;
            Run.StartPendingBranchesEagerly(this);

            while (IsRunning)
                yield return null;
        }


        /// <summary>Оборвать всё этого происхождения, включая фоновые, и отпустить долгие эффекты, взятые внутри.</summary>
        public void Cancel()
        {
            IsWatched = true;

            if (rootBranchCount > 0)
                Run.CancelInside(this);
        }


        internal void CountRootBranch()
        {
            rootBranchCount++;
        }
    }
}
