namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инспектор постройки, который умеет нарисовать только главные поля — секцию Construction карточки панели (<see cref="LiveBuilderConstructionCard"/>).
    /// Материалы, тонкая подстройка и предупреждения остаются в полном инспекторе: у панели для них свои секции и заголовок.
    /// </summary>
    public interface ILiveBuilderCompactInspector
    {
        // Сам обновляет и сохраняет serializedObject.
        void DrawMainFields();
    }
}
