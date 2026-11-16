using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Помощь блокам, переносящим старые поля в новые: перенос — в главном потоке, не при чтении данных.
    /// В редакторе незаданная ссылка приходит «пустышкой», а не null — проверять надо так, как здесь.
    /// </summary>
    internal static class ScenarioFieldMigration
    {
        /// <summary>Ссылка задана. Сначала дешёвая проверка: в сборке пустые ссылки — настоящий null.</summary>
        public static bool IsAssigned(Object reference)
        {
            return !ReferenceEquals(reference, null) && reference != null;
        }
    }
}
