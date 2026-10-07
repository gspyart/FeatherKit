using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Helpers
{
    public static class FCollectionUtils
    {
        // Null или пустой список — возвращает default(T), а не кидает исключение.
        public static T RandomElement<T>(this IReadOnlyList<T> list)
        {
            if (list == null || list.Count == 0)
                return default;

            return list[Random.Range(0, list.Count)];
        }

        /// <summary>
        /// N случайных без повторов. Если просят больше, чем есть — вернёт всё, что есть.
        /// Порядок результата тоже случайный.
        /// </summary>
        public static List<T> RandomElements<T>(this IReadOnlyList<T> list, int count)
        {
            var result = new List<T>();

            list.RandomElements(count, result);

            return result;
        }

        /// <summary>
        /// То же, но в готовый список — когда выбор идёт каждый кадр и мусорить нельзя.
        /// Список очищается перед заполнением.
        /// </summary>
        public static void RandomElements<T>(this IReadOnlyList<T> list, int count, List<T> result)
        {
            if (result == null)
                return;

            if (list == null || count <= 0)
            {
                result.Clear();
                return;
            }

            // Приёмник может оказаться тем же списком, из которого выбираем. Чистить его
            // тогда нельзя — сотрём то, что просили выбрать, ещё до того как прочитаем.
            if (!ReferenceEquals(list, result))
            {
                result.Clear();

                for (var i = 0; i < list.Count; i++)
                    result.Add(list[i]);
            }

            var take = Mathf.Min(count, result.Count);

            // Частичная перетасовка: доводим до нужного числа первые элементы и обрезаем хвост.
            // Полная перетасовка всего списка ради трёх элементов — лишняя работа.
            for (var i = 0; i < take; i++)
            {
                var j = Random.Range(i, result.Count);

                (result[i], result[j]) = (result[j], result[i]);
            }

            result.RemoveRange(take, result.Count - take);
        }

        /// <summary>Перемешать на месте.</summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            if (list == null)
                return;

            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);

                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public static bool IsNullOrEmpty<T>(this ICollection<T> collection) => collection == null || collection.Count == 0;
    }
}
