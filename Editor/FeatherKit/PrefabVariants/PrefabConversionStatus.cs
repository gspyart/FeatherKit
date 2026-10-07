namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Чем кончилась переделка одного префаба — или её предпросмотр.
    /// </summary>
    public enum PrefabConversionStatus
    {
        /// <summary>Предпросмотр прошёл: переделывать можно.</summary>
        Ready,

        /// <summary>Переделывать нельзя или незачем — причина в итоге.</summary>
        Skipped,

        /// <summary>Пробовали, не вышло. Файл префаба не тронут.</summary>
        Failed,

        /// <summary>Сохранён вариантом шаблона.</summary>
        Converted
    }
}
