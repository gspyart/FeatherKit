using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Порядок объектов в иерархии, как его читает глаз сверху вниз: родитель раньше своих детей, дети — по порядку,
    /// всё, что внутри объекта, — раньше его следующего соседа. По нему вырезающая коробка решает, режет ли она стену или здание:
    /// режет только то, что выше неё в списке, — как кисти в Unreal BSP и RealtimeCSG.
    /// Спрашивают о каждой коробке для каждой стены и здания, поэтому на время прохода очереди пересборки (<see cref="LiveBuilderRebuildQueue"/>)
    /// путь объекта от корня считается один раз. Поменял иерархию посреди прохода — сбрось запомненное (<see cref="ForgetPassPaths"/>).
    /// </summary>
    public static class LiveBuilderHierarchyOrder
    {
        // Пути объектов от корня на время прохода; вне прохода — null, считаются каждый раз.
        private static Dictionary<Transform, int[]> passSiblingPaths;


        // ── Проход ───────────────────────────────────────────────────────

        public static void BeginPass()
        {
            passSiblingPaths = new Dictionary<Transform, int[]>();
        }


        public static void EndPass()
        {
            passSiblingPaths = null;
        }


        // Иерархию поменяли посреди прохода (здание добавило или убрало части) — запомненные пути уже не те.
        public static void ForgetPassPaths()
        {
            passSiblingPaths?.Clear();
        }


        // ── Порядок ──────────────────────────────────────────────────────

        // first стоит в иерархии раньше second. Сам с собой — нет. Разные сцены — по порядку открытых сцен.
        public static bool IsBefore(Transform first, Transform second)
        {
            if (first == second)
                return false;

            if (first.gameObject.scene != second.gameObject.scene)
                return SceneIndexOf(first.gameObject.scene) < SceneIndexOf(second.gameObject.scene);

            var firstPath = SiblingPathOf(first);
            var secondPath = SiblingPathOf(second);
            var commonLength = Mathf.Min(firstPath.Length, secondPath.Length);

            for (var level = 0; level < commonLength; level++)
                if (firstPath[level] != secondPath[level])
                    return firstPath[level] < secondPath[level];

            // Один — предок другого: предок раньше.
            return firstPath.Length < secondPath.Length;
        }

        // Номера среди соседей от корня сцены до самого объекта; в проходе — из памяти.
        private static int[] SiblingPathOf(Transform node)
        {
            if (passSiblingPaths != null && passSiblingPaths.TryGetValue(node, out var remembered))
                return remembered;

            var path = new List<int>();

            for (var current = node; current != null; current = current.parent)
                path.Add(current.GetSiblingIndex());

            path.Reverse();
            var pathArray = path.ToArray();

            if (passSiblingPaths != null)
                passSiblingPaths[node] = pathArray;

            return pathArray;
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
