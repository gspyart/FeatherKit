namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Значки настроек окна Live Builder — переключателей, строк и корешков вкладок. Рисунки — в <see cref="LiveBuilderOptionIcons"/>.
    /// </summary>
    public enum LiveBuilderOptionIcon
    {
        // Окно
        Power,
        Keyboard,
        Level,

        // Общее
        Storey,
        Grid,
        SnapPoints,
        Wireframe,
        ViewAll,
        ViewRooms,
        ViewWallsDown,

        // Коробка и выделение
        BoxAuto,
        BoxAdd,
        BoxCut,
        Boxes,
        Faces,
        NewObject,
        Room,
        Ramp,
        Column,

        // Точки линии и контура
        Corner,
        Smooth,
        Bezier,

        // Форма
        Rectangle,
        Contour,
        OpeningRectangle,
        OpeningArch,
        OpeningCircle,
        AtCursor,

        // Лестница
        OpenSteps,
        SolidSteps,

        // Этажи здания
        ThisStorey,
        AllStoreysAbove,
        Floor,
        Roof
    }
}
