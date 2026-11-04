namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Смысловая зона окна Live Builder — у каждой свой цвет (<see cref="LiveBuilderWindowZones"/>):
    /// общее — серая, выбранный инструмент — синяя, выделенное — оранжевая, уровень — фиолетовая.
    /// </summary>
    public enum LiveBuilderWindowZone
    {
        Global,
        Tool,
        Selection,
        Level
    }
}
