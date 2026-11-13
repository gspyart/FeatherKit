using System.Collections;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Одно срабатывание выхода ноды со всем, что из него выросло по проводам, — обрываемый кусок запуска.
    /// - идёт, пока идёт хоть одна его ветка, фоновые тоже
    /// - его можно дождаться или оборвать вместе с потомками и долгими эффектами, взятыми внутри
    /// </summary>
    public class ScenarioFiring
    {
        internal ScenarioFiring(ScenarioRun run, ScenarioNode node, ScenarioOutput output)
        {
            Run = run;
            Node = node;
            Output = output;
        }


        public ScenarioRun Run { get; }

        /// <summary>Нода, чей выход сработал.</summary>
        public ScenarioNode Node { get; }

        public ScenarioOutput Output { get; }

        /// <summary>Идёт хоть одна ветка этого происхождения.</summary>
        public bool IsRunning => false; // TODO этап 1, рантайм


        public IEnumerator WaitUntilDone()
        {
            // TODO этап 1, рантайм: ждать, пока IsRunning не станет false.
            yield break;
        }


        /// <summary>Оборвать всё этого происхождения, включая фоновые, и отпустить долгие эффекты, взятые внутри.</summary>
        public void Cancel()
        {
            // TODO этап 1, рантайм
        }
    }
}
