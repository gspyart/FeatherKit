using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Collections
{
    /// <summary>
    /// Взвешенный случайный выбор из набора вариантов — для лут-роллов, выбора карточек
    /// модификаторов и т.п. Ограничен ссылочными типами, чтобы можно было проверять на null.
    /// </summary>
    public class WeightedRandom<T> where T : class
    {
        private readonly List<T> items = new List<T>();
        private readonly List<float> weights = new List<float>();

        private float totalWeight;


        public int Count => items.Count;


        // Null-объект или вес <= 0 игнорируются — такой вариант никогда не выпадет.
        public void Add(T item, float weight)
        {
            if (item == null || weight <= 0f)
                return;

            items.Add(item);
            weights.Add(weight);
            totalWeight += weight;
        }

        public void Clear()
        {
            items.Clear();
            weights.Clear();
            totalWeight = 0f;
        }


        // False и result == null, если нечего выбирать.
        public bool TryGet(out T result)
        {
            if (items.Count == 0 || totalWeight <= 0f)
            {
                result = null;
                return false;
            }

            var roll = Random.value * totalWeight;
            var cumulative = 0f;

            for (var i = 0; i < items.Count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                {
                    result = items[i];
                    return true;
                }
            }

            result = items[items.Count - 1];
            return true;
        }

        // Выбрать до count уникальных вариантов без повторов (например, 3 карточки модификаторов).
        public List<T> GetUniqueMultiple(int count)
        {
            var result = new List<T>();
            if (count <= 0 || items.Count == 0)
                return result;

            var pool = new WeightedRandom<T>();
            for (var i = 0; i < items.Count; i++)
                pool.Add(items[i], weights[i]);

            var take = Mathf.Min(count, items.Count);
            for (var i = 0; i < take; i++)
            {
                if (!pool.TryGet(out var picked))
                    break;

                result.Add(picked);
                pool.Remove(picked);
            }

            return result;
        }

        private void Remove(T item)
        {
            if (item == null)
                return;

            var index = items.IndexOf(item);
            if (index < 0)
                return;

            totalWeight -= weights[index];
            items.RemoveAt(index);
            weights.RemoveAt(index);
        }
    }
}
