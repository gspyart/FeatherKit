namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Значки настроек окна Live Builder — переключателей, строк и заголовков зон. Рисунки — в <see cref="LiveBuilderOptionIcons"/>.
    /// </summary>
    public enum LiveBuilderOptionIcon
    {
        // Окно
        Power,
        Info,
        Keyboard,
        Global,
        Level,

        // Общее
        Storey,
        Grid,
        Eye,
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
        SeparateSteps,
        SolidSteps,

        // Этажи здания
        ThisStorey,
        AllStoreysAbove,
        Floor,
        Roof
    }
}
