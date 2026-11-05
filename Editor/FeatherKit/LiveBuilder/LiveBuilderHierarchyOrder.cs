using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Порядок объектов в иерархии, как его читает глаз сверху вниз: родитель раньше своих детей, дети — по порядку,
    /// всё, что внутри объекта, — раньше его следующего соседа. По нему вырезающая коробка решает, режет ли она стену или здание:
    /// режет только то, что выше неё в списке, — как кисти в Unreal BSP и RealtimeCSG.
    /// </summary>
    public static class LiveBuilderHierarchyOrder
    {
        // first стоит в иерархии раньше second. Сам с собой — нет. Разные сцены — по порядку открытых сцен.
        public static bool IsBefore(Transform first, Transform second)
        {
            if (first == second)
                return false;

            if (first.gameObject.scene != second.gameObject.scene)
                return SceneIndexOf(first.gameObject.scene) < SceneIndexOf(second.gameObject.scene);

            var firstPath = SiblingPathOf(first);
            var secondPath = SiblingPathOf(second);
            var commonLength = Mathf.Min(firstPath.Count, secondPath.Count);

            for (var level = 0; level < commonLength; level++)
                if (firstPath[level] != secondPath[level])
                    return firstPath[level] < secondPath[level];

            // Один — предок другого: предок раньше.
            return firstPath.Count < secondPath.Count;
        }

        // Номера среди соседей от корня сцены до самого объекта.
        private static List<int> SiblingPathOf(Transform node)
        {
            var path = new List<int>();

            for (var current = node; current != null; current = current.parent)
                path.Add(current.GetSiblingIndex());

            path.Reverse();
            return path;
        }

        private static int SceneIndexOf(Scene scene)
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index) == scene)
                    return index;

            return -1;
        }
    }
}
