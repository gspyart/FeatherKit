namespace FeatherKit.LiveBuilder
{
    /// <summary>
    /// Постройка с «копиями вверх» — то же самое на этажах выше одним объектом: стена по линии (<see cref="SplineWall"/>), вырез (<see cref="GeometryCutter"/>).
    /// Сколько копий и шаг — свои, а в здании их задаёт привязка к его этажам (<see cref="SplineBuildingStoreyLink"/>): копий — сколько этажей в повторе, шаг — этаж.
    /// Объект из коробок, комната и лестница копируются по-другому — их повторяет сам расчёт коробок по той же привязке.
    /// </summary>
    public interface IStoreyCopies
    {
        // Сколько всего друг над другом, вместе с самим объектом.
        int CopiesUp { get; set; }

        // Шаг копий вверх, м.
        float CopyStep { get; set; }
    }
}
