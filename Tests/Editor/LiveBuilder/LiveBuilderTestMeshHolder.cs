using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Ассет для проверки уборки мешей: ScriptableObject, который только ссылается на меш, — как настройки или таблица уровня в игре.
    /// Файл меша, на который смотрит только он, уборка не трогает.
    /// </summary>
    public sealed class LiveBuilderTestMeshHolder : ScriptableObject
    {
        public Mesh Mesh;
    }
}
