namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Что делают ручки инструмента «Своя точка»: двигают саму точку
    /// или двигают, вращают, растягивают выделенное вокруг неё.
    /// </summary>
    public enum WorkingPointMode
    {
        MovePoint,
        MoveSelection,
        RotateSelection,
        ScaleSelection
    }
}
