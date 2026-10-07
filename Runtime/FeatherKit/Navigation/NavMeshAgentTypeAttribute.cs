using UnityEngine;

namespace FeatherKit.Navigation
{
    /// <summary>
    /// Показать поле как выбор типа агента навмеша — списком имён, а не числом.
    ///
    /// Номер типа сам по себе не значит ничего: у «Humanoid» он один, у большого
    /// своего — другой, и на глаз они неразличимы. Поставленный не тот номер означает
    /// путь по чужой поверхности, а заметить это можно только в игре.
    /// </summary>
    public class NavMeshAgentTypeAttribute : PropertyAttribute
    {
    }
}
