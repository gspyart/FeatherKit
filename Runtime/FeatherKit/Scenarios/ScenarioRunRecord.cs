namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Чем кончилась одна попытка запуска сценария — строка журнала в окне сценариев.
    /// Почему не сработал, кто вызвал, на каком шаге или ноде графа оборвался.
    /// </summary>
    public readonly struct ScenarioRunRecord
    {
        public ScenarioRunRecord(Scenario scenario, string instigatorName, float startTime, float endTime,
            ScenarioRunOutcome outcome, int failedActionIndex = -1, string failureMessage = null,
            int startNodeId = -1, int failedNodeId = -1)
        {
            Scenario = scenario;
            InstigatorName = instigatorName;
            StartTime = startTime;
            EndTime = endTime;
            Outcome = outcome;
            FailedActionIndex = failedActionIndex;
            FailureMessage = failureMessage;
            StartNodeId = startNodeId;
            FailedNodeId = failedNodeId;
        }


        public Scenario Scenario { get; }

        /// <summary>Имя того, кто вызвал; null — никто.</summary>
        public string InstigatorName { get; }

        public float StartTime { get; }

        public float EndTime { get; }

        public ScenarioRunOutcome Outcome { get; }

        /// <summary>Номер шага, на котором сценарий оборвался с ошибкой; -1 — не обрывался или упал вложенный фоновый шаг.</summary>
        public int FailedActionIndex { get; }

        public string FailureMessage { get; }

        /// <summary>Id ноды When, с которой начался запуск графа; -1 — запуск старого списка шагов.</summary>
        public int StartNodeId { get; }

        /// <summary>Id ноды графа, на которой запуск упал; -1 — не падал или упал вне ноды.</summary>
        public int FailedNodeId { get; }
    }


    public enum ScenarioRunOutcome
    {
        Finished,
        Stopped,
        Failed,
        ConditionsNotMet
    }
}
