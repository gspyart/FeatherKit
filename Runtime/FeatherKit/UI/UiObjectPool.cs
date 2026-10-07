using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.UI
{
    /// <summary>
    /// Стопка одинаковых объектов интерфейса из одного префаба: взял — показал, вернул — выключил.
    /// - выдаёт свободный или заводит новый, сразу под нужным родителем; заводит и заранее;
    /// - возвращённый только выключает: из холста он не уходит — менять иерархию гаснущего холста нельзя.
    /// Часть: держит её полем тот, кто показывает.
    /// </summary>
    public class UiObjectPool<T> where T : Component
    {
        private readonly T prefab;
        private readonly Stack<T> free = new Stack<T>();


        public UiObjectPool(T prefab)
        {
            this.prefab = prefab;
        }


        // ── Выдача и возврат ──────────────────────────────────────────────

        /// <summary>Свободный или новый объект под этим родителем. Нет префаба — null.</summary>
        public T Take(Transform parent)
        {
            while (free.Count > 0)
            {
                var reused = free.Pop();

                // Уничтоженный снаружи Unity выдаёт за null: такой пропускаем.
                if (reused == null)
                    continue;

                reused.transform.SetParent(parent, false);
                reused.gameObject.SetActive(true);

                return reused;
            }

            if (prefab == null)
                return null;

            var created = Object.Instantiate(prefab, parent);

            created.name = prefab.name;

            return created;
        }

        /// <summary>Выключить и отложить до следующей выдачи.</summary>
        public void Release(T item)
        {
            if (item == null)
                return;

            item.gameObject.SetActive(false);

            free.Push(item);
        }


        /// <summary>Завести столько свободных заранее: первые показы не должны стоить Instantiate.</summary>
        public void Prewarm(int count, Transform parent)
        {
            if (prefab == null)
                return;

            for (var i = free.Count; i < count; i++)
            {
                var created = Object.Instantiate(prefab, parent);

                created.name = prefab.name;

                Release(created);
            }
        }
    }
}
