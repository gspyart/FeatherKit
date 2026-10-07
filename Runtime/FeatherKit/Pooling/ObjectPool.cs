using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Pooling
{
    /// <summary>
    /// Пул инстансов одного префаба. Переиспользует объекты вместо Instantiate/Destroy.
    /// Не использовать напрямую в игровом коде — доступ через PoolManager.
    ///
    /// Работает с GameObject, а не с компонентом: создаёт, включает и уничтожает Unity именно
    /// объект целиком. Нужный компонент выдаёт PoolManager сверху — поэтому один и тот же пул
    /// обслуживает и запрос «дай мне снаряд», и «дай мне объект».
    /// </summary>
    public class ObjectPool
    {
        private readonly GameObject prefab;
        private readonly Transform parent;
        private readonly int maxSize;
        private readonly PoolManager owner;

        private readonly Stack<GameObject> inactive = new Stack<GameObject>();
        private readonly HashSet<GameObject> active = new HashSet<GameObject>();

        // Хуки ищем один раз на инстанс: GetComponents создаёт массив, а спавн идёт каждый кадр.
        private readonly Dictionary<GameObject, IPoolable[]> hooks = new Dictionary<GameObject, IPoolable[]>();


        public int CountActive => active.Count;

        public int CountInactive => inactive.Count;

        public int CountTotal => CountActive + CountInactive;


        // maxSize — лимит неактивных инстансов про запас, не лимит на Get.
        public ObjectPool(GameObject prefab, PoolManager owner, Transform parent = null, int maxSize = 256)
        {
            this.prefab = prefab;
            this.owner = owner;
            this.parent = parent;
            this.maxSize = Mathf.Max(1, maxSize);
        }


        /// <summary>Заранее создать N неактивных инстансов про запас.</summary>
        public void Prewarm(int count)
        {
            for (var i = 0; i < count && CountInactive < maxSize; i++)
                inactive.Push(CreateInstance());
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            var instance = TakeInactive() ?? CreateInstance();

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            active.Add(instance);

            var poolables = HooksFor(instance);

            for (var i = 0; i < poolables.Length; i++)
                poolables[i].OnSpawned(owner);

            return instance;
        }

        public void Release(GameObject instance)
        {
            if (!active.Remove(instance))
                return;

            // Инстанс уничтожили мимо пула — возвращать нечего, просто вычёркиваем.
            if (instance == null)
            {
                Forget(instance);
                return;
            }

            var poolables = HooksFor(instance);

            for (var i = 0; i < poolables.Length; i++)
                poolables[i].OnDespawned();

            instance.SetActive(false);
            instance.transform.SetParent(parent);

            if (CountInactive >= maxSize)
            {
                Destroy(instance);
                return;
            }

            inactive.Push(instance);
        }

        /// <summary>Уничтожить всё, что пул создал. Зовётся при разборе PoolManager.</summary>
        public void Dispose()
        {
            foreach (var instance in inactive)
                Destroy(instance);

            foreach (var instance in active)
                Destroy(instance);

            inactive.Clear();
            active.Clear();
            hooks.Clear();
        }


        // Достать инстанс из стопки, пропуская уничтоженные снаружи. Unity оставляет от
        // такого объекта "поддельно-null" ссылку: она лежит в стопке как живая, а обращение
        // к её transform падает MissingReferenceException прямо в момент спавна.
        private GameObject TakeInactive()
        {
            while (inactive.Count > 0)
            {
                var candidate = inactive.Pop();

                if (candidate != null)
                    return candidate;

                Forget(candidate);
            }

            return null;
        }

        // Хуков может не оказаться, если инстанс пришёл не от нас или запись уже вычистили.
        // Пустой массив вместо исключения: пул обязан выдать объект, а не упасть.
        private IPoolable[] HooksFor(GameObject instance)
        {
            return hooks.TryGetValue(instance, out var poolables) ? poolables : System.Array.Empty<IPoolable>();
        }

        // Менеджер должен забыть инстанс вместе с его поколением — иначе записи о давно
        // уничтоженных объектах живут до конца запуска.
        private void Destroy(GameObject instance)
        {
            if (instance == null)
                return;

            Forget(instance);

            Object.Destroy(instance);
        }

        // Вычеркнуть инстанс отовсюду, не уничтожая: он либо уже уничтожен, либо мы
        // уничтожим его следующей строкой.
        private void Forget(GameObject instance)
        {
            owner?.Forget(instance);
            hooks.Remove(instance);
        }

        private GameObject CreateInstance()
        {
            var instance = Object.Instantiate(prefab, parent);
            instance.SetActive(false);

            // Только с корня: вложенные объекты со своими хуками — это отдельные сущности,
            // которые обычно и в пул уходят отдельно.
            hooks[instance] = instance.GetComponents<IPoolable>();

            return instance;
        }
    }
}
