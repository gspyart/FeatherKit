namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода, которая держит живое состояние сверх веток запуска: подписки, списки ожиданий и повторов.
    /// Сценарий сбрасывает его на Stop и уничтожении, после того как оборвал запуски. Своя нода с таким состоянием реализует его.
    /// </summary>
    public interface IScenarioLiveNode
    {
        void ResetLiveState();
    }
}
