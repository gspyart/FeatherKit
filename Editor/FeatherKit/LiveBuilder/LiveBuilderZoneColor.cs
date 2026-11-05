namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Цвет смысловой зоны окна Live Builder (<see cref="LiveBuilderZoneFrame"/>):
    /// общее — серая, выбранный инструмент — синяя, выделенное — оранжевая, уровень — фиолетовая.
    /// </summary>
    public enum LiveBuilderZoneColor
    {
        Global,
        Tool,
        Selection,
        Level
    }
}
