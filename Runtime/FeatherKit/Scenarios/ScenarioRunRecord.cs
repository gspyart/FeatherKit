namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Чем кончилась одна попытка запуска сценария — строка журнала в окне сценариев.
    /// Почему не сработал, кто вызвал, на каком шаге оборвался.
    /// </summary>
    public readonly struct ScenarioRunRecord
    {
        public ScenarioRunRecord(Scenario scenario, string instigatorName, float startTime, float endTime,
            ScenarioRunOutcome outcome, int failedActionIndex = -1, string failureMessage = null)
        {
            Scenario = scenario;
            InstigatorName = instigatorName;
            StartTime = startTime;
            EndTime = endTime;
            Outcome = outcome;
            FailedActionIndex = failedActionIndex;
            FailureMessage = failureMessage;
        }


        public Scenario Scenario { get; }

        /// <summary>Имя того, кто вызвал; null — никто.</summary>
        public string InstigatorName { get; }

        public float StartTime { get; }

        public float EndTime { get; }

        public ScenarioRunOutcome Outcome { get; }

        /// <summary>Номер шага, на котором сценарий оборвался с ошибкой; -1 — не обрывался.</summary>
        public int FailedActionIndex { get; }

        public string FailureMessage { get; }
    }


    public enum ScenarioRunOutcome
    {
        Finished,
        Stopped,
        Failed,
        ConditionsNotMet
    }
}
