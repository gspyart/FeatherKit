using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Что под повтором (<see cref="LiveBuilderArray"/>) копируется настоящими объектами, а что — расчётом.
    /// - настоящая часть: внешний корень экземпляра префаба или объект с компонентами не Live Builder (рендерер, свет, свой скрипт); постройка Live Builder — нет
    /// - граница расчёта: настоящая часть и контейнер её копий (<see cref="LiveBuilderGeneratedCopies"/>) — повторы над ними расчётом не идут
    /// </summary>
    public static class LiveBuilderArrayRealParts
    {
        private const string LiveBuilderNamespace = "FeatherKit.LiveBuilder";

        // Общий список под компоненты одного узла: проверка идёт на каждый узел цепочки повторов.
        private static readonly List<Component> ComponentsBuffer = new();


        // ── Граница ──────────────────────────────────────────────────────

        // Повторы над узлом считаются не расчётом: он сам настоящий, или это контейнер копий.
        public static bool IsBoundary(Transform node)
        {
            return IsGeneratedContainer(node) || IsRealPart(node);
        }


        public static bool IsGeneratedContainer(Transform node)
        {
            return node.TryGetComponent<LiveBuilderGeneratedCopies>(out _);
        }


        // Настоящая часть: копия ей нужна объектом в сцене, а не строчками в меше постройки. Постройка Live Builder — и в префабе — считается расчётом, как раньше.
        // Повтор и модуль — пометки, а не постройка: по ним не судим.
        public static bool IsRealPart(Transform node)
        {
            var gameObject = node.gameObject;

            if (IsServiceObject(gameObject))
                return false;

            gameObject.GetComponents(ComponentsBuffer);
            var hasOwnComponents = false;

            foreach (var component in ComponentsBuffer)
            {
                if (component == null || component is Transform || component is LiveBuilderArray || component is LiveBuilderModule)
                    continue;

                if (IsLiveBuilderComponent(component))
                    return false;

                hasOwnComponents = true;
            }

            return hasOwnComponents || PrefabUtility.IsOutermostPrefabInstanceRoot(gameObject);
        }

        // Меш, рендерер и коллайдер у постройки Live Builder — её же результат, а не своё.
        private static bool IsLiveBuilderComponent(Component component)
        {
            var space = component.GetType().Namespace;
            return space != null && space.StartsWith(LiveBuilderNamespace, System.StringComparison.Ordinal);
        }


        // Скрытая несохраняемая копия массива FeatherKit — чужой след, её не копируем и не обходим.
        public static bool IsServiceObject(GameObject gameObject)
        {
            return (gameObject.hideFlags & HideFlags.DontSave) != 0;
        }


        // ── Под повтором ─────────────────────────────────────────────────

        // Настоящие части, которые копирует этот повтор: под ним и он сам. Внутрь настоящей части, контейнеров копий
        // и узлов со своим повтором не заходим — те копирует их повтор.
        public static List<Transform> CopiedBy(LiveBuilderArray array)
        {
            var realParts = new List<Transform>();
            CollectUnder(array.transform, array.transform, realParts);
            return realParts;
        }

        private static void CollectUnder(Transform node, Transform arrayNode, List<Transform> realParts)
        {
            if (IsServiceObject(node.gameObject) || IsGeneratedContainer(node) || node != arrayNode && node.TryGetComponent<LiveBuilderArray>(out _))
                return;

            if (IsRealPart(node))
            {
                realParts.Add(node);
                return;
            }

            foreach (Transform child in node)
                CollectUnder(child, arrayNode, realParts);
        }


        // Узел внутри контейнера копий: его повторы — чужой след, они уже стали копиями.
        public static bool IsInsideGeneratedCopies(Transform node)
        {
            return node.GetComponentInParent<LiveBuilderGeneratedCopies>(true) != null;
        }
    }
}
