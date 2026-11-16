namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Блок со старыми полями прежних версий: умеет перенести их в новые. Только в главном потоке.
    /// Редактор зовёт перенос кнопкой «Upgrade Fields»; в игре блок зовёт его сам перед работой.
    /// </summary>
    public interface IScenarioUpgradableBlock
    {
        /// <summary>Перенести старые поля в новые, если новые не заданы; старые очистить. True — данные изменились.</summary>
        bool TryUpgradeLegacyFields();
    }
}
