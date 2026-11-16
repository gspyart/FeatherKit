using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Один ли это объект для блоков списков: сам объект Unity или GameObject и его компоненты.</summary>
    public static class ScenarioObjectMatching
    {
        public static bool IsSameObject(Object left, Object right)
        {
            if (left == right)
                return true;

            var leftGameObject = GameObjectOf(left);

            return leftGameObject != null && leftGameObject == GameObjectOf(right);
        }

        private static GameObject GameObjectOf(Object unityObject)
        {
            return unityObject switch
            {
                GameObject gameObject => gameObject,
                Component component => component.gameObject,
                _ => null
            };
        }


        /// <summary>Убрать все совпадения. Вернёт, сколько убрано.</summary>
        public static int RemoveMatches(List<Object> objects, Object removed)
        {
            var removedCount = 0;

            for (var index = objects.Count - 1; index >= 0; index--)
            {
                if (!IsSameObject(objects[index], removed))
                    continue;

                objects.RemoveAt(index);
                removedCount++;
            }

            return removedCount;
        }
    }
}
