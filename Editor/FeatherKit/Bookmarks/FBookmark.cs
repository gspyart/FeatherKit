using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Одна закладка: ссылка на ассет проекта или объект сцены по GlobalObjectId.
    /// - находит объект заново, даже после переименования и переноса
    /// - помнит имя и сцену, чтобы показать закладку, пока сцена закрыта
    /// </summary>
    [Serializable]
    public class FBookmark
    {
        // Виды адреса у GlobalObjectId: 2 — объект сцены, 0 — адреса нет.
        private const int SceneObjectIdentifierType = 2;
        private const int NullIdentifierType = 0;

        [SerializeField] private string globalId;
        [SerializeField] private string displayName;
        [SerializeField] private bool isSceneObject;

        [NonSerialized] private Object resolved;
        [NonSerialized] private bool isResolveAttempted;


        private FBookmark(GlobalObjectId id, Object target)
        {
            globalId = id.ToString();
            displayName = target.name;
            isSceneObject = id.identifierType == SceneObjectIdentifierType;
            resolved = target;
            isResolveAttempted = true;
        }


        public string GlobalId => globalId;

        public bool IsSceneObject => isSceneObject;

        /// <summary>Живое имя объекта, а если он не найден — последнее известное.</summary>
        public string DisplayName
        {
            get
            {
                var target = Resolve();

                if (target != null)
                    displayName = target.name;

                return displayName;
            }
        }

        /// <summary>Путь сцены, где лежит объект. У ассета — пусто.</summary>
        public string ScenePath => isSceneObject && GlobalObjectId.TryParse(globalId, out var id)
            ? AssetDatabase.GUIDToAssetPath(id.assetGUID)
            : string.Empty;

        /// <summary>Объект сцены, чья сцена сейчас закрыта: его можно найти, открыв её.</summary>
        public bool IsInClosedScene => isSceneObject && Resolve() == null && IsSceneFileAlive && !IsSceneLoaded;

        private bool IsSceneFileAlive => !string.IsNullOrEmpty(ScenePath);

        private bool IsSceneLoaded => UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath).isLoaded;


        // ── Создание ─────────────────────────────────────────────────────

        // Нет адреса у того, чего нет в файлах: объекта, созданного в игре, или сцены, которую не сохраняли.
        public static bool TryCreate(Object target, out FBookmark bookmark)
        {
            bookmark = null;

            if (target == null)
                return false;

            var id = GlobalObjectId.GetGlobalObjectIdSlow(target);

            if (id.identifierType == NullIdentifierType || id.targetObjectId == 0)
                return false;

            bookmark = new FBookmark(id, target);

            return true;
        }


        /// <summary>Независимая копия — для другой вкладки.</summary>
        public FBookmark Copy()
        {
            return (FBookmark)MemberwiseClone();
        }


        // ── Поиск объекта ────────────────────────────────────────────────

        // Поиск медленный, а окно перерисовывается часто — ищем один раз до сброса.
        // Уничтоженный объект Unity равен null, но ссылка жива: такой ищем заново — его могли вернуть отменой.
        public Object Resolve()
        {
            if (resolved != null)
                return resolved;

            if (isResolveAttempted && ReferenceEquals(resolved, null))
                return null;

            isResolveAttempted = true;

            var found = GlobalObjectId.TryParse(globalId, out var id)
                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id)
                : null;

            // «Не нашли» храним настоящим null — иначе его не отличить от «нашли и потом уничтожили».
            resolved = found != null ? found : null;

            return resolved;
        }


        /// <summary>Не нашли раньше — поискать снова: сцена открылась, удалённое вернули.</summary>
        public void RetryIfNotFound()
        {
            if (resolved == null)
                isResolveAttempted = false;
        }
    }
}
