using System.Collections.Generic;
using FeatherKit.Helpers;
using FeatherKit.Lifecycle;
using UnityEngine;

namespace FeatherKit.Pooling
{
    /// <summary>
    /// ЗАВОДИТСЯ КОНТЕКСТОМ САМ — у каждой сцены свой. Бери готовый из своего
    /// SceneContext, а не создавай второй: инстансы разных пулов не знают друг о друге,
    /// и один и тот же префаб начнёт плодиться в двух стопках.
    ///
    /// Единый реестр пулов на сцену: один ObjectPool на префаб.
    ///
    /// Просить можно и объект, и компонент на нём — пул под префабом один и тот же:
    /// <code>
    /// pool.Get(effectPrefab, point, Quaternion.identity);          // GameObject
    /// pool.Get(projectilePrefab, point, rotation).Launch(...);     // сразу нужный компонент
    /// </code>
    /// </summary>
    public class PoolManager : IReleasable
    {
        private const int DefaultMaxSize = 256;

        // Ключ — номер объекта префаба в памяти; и компонент, и GameObject ведут к одному пулу.
        private readonly Dictionary<ulong, ObjectPool> poolsByPrefab = new Dictionary<ulong, ObjectPool>();
        private readonly Dictionary<ulong, ObjectPool> poolByInstance = new Dictionary<ulong, ObjectPool>();

        // Поколение на инстанс: растёт на Get и на Release, для проверки актуальности handle.
        private readonly Dictionary<ulong, int> generationByInstance = new Dictionary<ulong, int>();


        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
                return null;

            var pool = GetOrCreatePool(prefab, null, DefaultMaxSize);
            var instance = pool.Get(position, rotation);

            RegisterTransition(instance, pool);

            return instance;
        }

        /// <summary>То же, но сразу нужным компонентом. Нет его на объекте — вернётся null.</summary>
        public T Get<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            if (prefab == null)
                return null;

            var instance = Get(prefab.gameObject, position, rotation);

            return instance != null ? instance.GetComponent<T>() : null;
        }

        /// <summary>Безопасная ссылка вместо голой: сама знает, что объект уже вернулся в пул.</summary>
        public PoolHandle<T> GetHandle<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            var instance = Get(prefab, position, rotation);
            if (instance == null)
                return new PoolHandle<T>();

            var id = instance.gameObject.GetStableId();

            return new PoolHandle<T>(instance, id, generationByInstance[id], this);
        }

        /// <summary>Заранее создать N инстансов, чтобы первый спавн в бою не подвесил кадр.</summary>
        public void Prewarm(GameObject prefab, int count, Transform parent = null, int maxSize = DefaultMaxSize)
        {
            if (prefab != null)
                GetOrCreatePool(prefab, parent, maxSize).Prewarm(count);
        }

        public void Prewarm<T>(T prefab, int count, Transform parent = null, int maxSize = DefaultMaxSize)
            where T : Component
        {
            if (prefab != null)
                Prewarm(prefab.gameObject, count, parent, maxSize);
        }

        /// <summary>Вернуть в пул. Объект не из пула — просто уничтожается.</summary>
        public void Release(GameObject instance)
        {
            // ReferenceEquals, а не ==: уничтоженный объект Unity выдаёт за null, но записи
            // о нём надо вычистить — этим займётся сам пул.
            if (ReferenceEquals(instance, null))
                return;

            var id = instance.GetStableId();

            if (poolByInstance.TryGetValue(id, out var pool))
            {
                pool.Release(instance);

                // Сверх лимита пул добивает инстанс и вычёркивает его через Forget.
                // Записывать его обратно нельзя: объекта уже нет, а записи о нём остались
                // бы висеть в словарях до конца запуска.
                if (poolByInstance.ContainsKey(id))
                    RegisterTransition(instance, pool);
            }
            else if (instance != null)
            {
                Object.Destroy(instance);
            }
        }

        public void Release<T>(T instance) where T : Component
        {
            if (instance != null)
                Release(instance.gameObject);
        }

        /// <summary>
        /// Контекст гасит свои сервисы — пул разбирает себя вместе с инстансами. Обычно
        /// сцена уносит их и так, но при перезагрузке сцены контекст гасится, пока старая
        /// ещё жива: без этого её объекты остались бы ничьими.
        /// </summary>
        public void Release() => Clear();

        /// <summary>Разобрать все пулы вместе с инстансами.</summary>
        public void Clear()
        {
            foreach (var pool in poolsByPrefab.Values)
                pool.Dispose();

            poolsByPrefab.Clear();
            poolByInstance.Clear();
            generationByInstance.Clear();
        }


        /// <summary>
        /// Забыть уничтоженный инстанс. Зовёт сам пул, когда добивает лишние объекты:
        /// без этого записи о них копились бы весь запуск.
        /// </summary>
        internal void Forget(GameObject instance)
        {
            // Тот же ReferenceEquals: забыть надо именно уничтоженный инстанс, а обычная
            // проверка на null отсеяла бы как раз его.
            if (ReferenceEquals(instance, null))
                return;

            var id = instance.GetStableId();

            poolByInstance.Remove(id);
            generationByInstance.Remove(id);
        }

        // Используется PoolHandle для самопроверки, напрямую не вызывать.
        internal bool IsGenerationActual(ulong instanceId, int generation)
        {
            return generationByInstance.TryGetValue(instanceId, out var current) && current == generation;
        }

        private void RegisterTransition(GameObject instance, ObjectPool pool)
        {
            var id = instance.GetStableId();

            poolByInstance[id] = pool;
            generationByInstance[id] = generationByInstance.TryGetValue(id, out var g) ? g + 1 : 1;
        }

        private ObjectPool GetOrCreatePool(GameObject prefab, Transform parent, int maxSize)
        {
            var key = prefab.GetStableId();

            if (poolsByPrefab.TryGetValue(key, out var existing))
                return existing;

            var pool = new ObjectPool(prefab, this, parent, maxSize);
            poolsByPrefab[key] = pool;

            return pool;
        }
    }
}
