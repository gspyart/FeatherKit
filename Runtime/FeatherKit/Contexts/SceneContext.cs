using FeatherKit.Pooling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.Contexts
{
    /// <summary>
    /// Контекст одной сцены. Отличается от базового удобством для сценовых компонентов:
    /// их не создать через new, они уже лежат на сцене или приходят префабом.
    ///
    /// Всё, что переживает сцену, сюда класть не надо — ему место в AppContext, откуда
    /// это и так видно через родителя.
    /// </summary>
    public abstract class SceneContext : Context
    {
        /// <summary>Чья это сцена. По ней объект находит свой контекст, а не соседний.</summary>
        public Scene Scene { get; private set; }
        public Transform RootTransform { get; private set; }

        
        internal void Bind(Scene scene, Transform rootObject)
        {
            Scene = scene;
            RootTransform = rootObject;
        }


        // Пул свой на каждую сцену, а не общий: его инстансы — объекты этой сцены, и вместе
        // с ней их уничтожает Unity. Общий пул остался бы со списком мёртвых ссылок.
        private protected override void RegisterDefaults()
        {
            Register(new PoolManager());
        }

        
        /// <summary>
        /// Достать сценовый компонент и сразу положить в контекст: сначала ищем на сцене,
        /// иначе спавним префаб. Нет ни того, ни другого — вернётся null, и решать,
        /// критично ли это, будет вызывающий.
        /// </summary>
        protected T RegisterSceneComponent<T>(T prefab = null, Transform parent = null) where T : Component
        {
            var service = SceneServiceResolver.Resolve(prefab, out var created);

            if (service == null)
                return null;

            // Уничтожаем при выгрузке только то, что сами и завели: объект, найденный
            // на сцене, нам не принадлежит.
            if (created)
            {
                MarkAsInstance(service.gameObject);
                service.transform.SetParent(parent);
            }

            return Register(service);
        }
    }
}
