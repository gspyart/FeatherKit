using UnityEngine;

namespace FeatherKit.Contexts
{
    /// <summary>
    /// Достаёт сценовый компонент-сервис по одной и той же схеме, чтобы её не переписывать
    /// под каждый контроллер:
    ///
    /// 1. Уже есть на сцене — берём его (удобно настраивать глазами в редакторе).
    /// 2. Нет, но задан префаб — инстанциируем префаб (сцена может быть пустой).
    /// 3. Нет ни того, ни другого — создаём голый GameObject с этим компонентом
    ///    (годится только тем, кому нечего настраивать в инспекторе).
    ///
    /// Пункт 3 логирует предупреждение: для камеры или UI это почти всегда значит, что
    /// забыли положить префаб в конфиг, а не осознанный выбор.
    /// </summary>
    public static class SceneServiceResolver
    {
        /// <summary>
        /// <paramref name="created"/> — создали мы объект или взяли готовый со сцены.
        /// По нему контекст решает, уничтожать его при выгрузке: чужой объект не наш,
        /// на нём может висеть ещё что-то нужное.
        /// </summary>
        public static T Resolve<T>(T prefab, out bool created) where T : Component
        {
            created = false;

            // FindObjectsInactive.Exclude — выключенный на сцене объект считаем "нет его",
            // иначе сервисом стал бы намеренно отключённый вариант.
            var existing = Object.FindAnyObjectByType<T>(FindObjectsInactive.Exclude);
            if (existing != null)
                return existing;

            created = true;

            if (prefab != null)
            {
                var instance = Object.Instantiate(prefab);
                instance.name = prefab.name; // без "(Clone)" — иначе в иерархии каша
                return instance;
            }

            Debug.LogWarning($"{typeof(T).Name}: нет ни на сцене, ни в конфиге — создан пустой объект. " +
                             "Если у него есть настройки в инспекторе, они будут дефолтные.");

            return new GameObject(typeof(T).Name).AddComponent<T>();
        }
    }
}
