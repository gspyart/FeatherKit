using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Настоящие копии повтора (<see cref="LiveBuilderArray"/>): экземпляры префабов и прочие объекты не Live Builder (<see cref="LiveBuilderArrayRealParts"/>).
    /// - копии лежат в спрятанном контейнере рядом с оригиналом (<see cref="LiveBuilderGeneratedCopies"/>), сохраняются в сцену, Unity их видит: их вырезы режут стены
    /// - создать, переставить, убрать, пересобрать после правки оригинала; оригинал, не показанный на своём месте (пропуск, другая буква узора), выключить
    /// Без Undo: копии — след чисел повтора, после Ctrl+Z чисел проход пересоберёт их сам.
    /// </summary>
    public static class LiveBuilderArraySync
    {
        // Копии не правятся руками и не видны в иерархии; в сцену сохраняются.
        private const HideFlags ContainerFlags = HideFlags.HideInHierarchy;
        private const HideFlags CopyFlags = HideFlags.NotEditable;

        private const string ContainerSuffix = " Copies";


        // ── Проход ───────────────────────────────────────────────────────

        // Все повторы сцены (или открытого префаба) с настоящими частями — копии на места; лишние контейнеры убраны. true — что-то поменялось.
        public static bool SyncAll()
        {
            var isChanged = false;
            var keptContainers = new HashSet<LiveBuilderGeneratedCopies>();

            foreach (var array in StageUtility.GetCurrentStageHandle().FindComponentsOfType<LiveBuilderArray>())
                if (CanMakeCopiesOf(array))
                    isChanged |= SyncArray(array, keptContainers);

            foreach (var container in StageUtility.GetCurrentStageHandle().FindComponentsOfType<LiveBuilderGeneratedCopies>())
                if (!keptContainers.Contains(container))
                {
                    Remove(container);
                    isChanged = true;
                }

            return isChanged;
        }

        // Повтор внутри настоящей части или чужих копий сам копий не делает: их копии — уже его копии.
        private static bool CanMakeCopiesOf(LiveBuilderArray array)
        {
            if (!array.isActiveAndEnabled)
                return false;

            for (var node = array.transform.parent; node != null; node = node.parent)
                if (LiveBuilderArrayRealParts.IsBoundary(node))
                    return false;

            return true;
        }


        // Копии каждой настоящей части повтора: копии повторов над ним × свои копии с буквой её варианта.
        private static bool SyncArray(LiveBuilderArray array, HashSet<LiveBuilderGeneratedCopies> keptContainers)
        {
            var realParts = LiveBuilderArrayRealParts.CopiedBy(array);

            if (realParts.Count == 0)
                return false;

            var copiesAbove = LiveBuilderArrayCopyPoses.CopiesOf(array.transform.parent);
            var ownCopies = LiveBuilderArrayCopyPoses.PlacedCopiesOf(array, copiesAbove.Count);
            var isChanged = false;

            foreach (var realPart in realParts)
            {
                if (!CanHoldCopies(realPart))
                    continue;

                var variant = VariantOf(array, realPart);
                var moves = new List<Matrix4x4>();
                var showsOriginal = false;

                foreach (var above in copiesAbove)
                    foreach (var copy in ownCopies)
                    {
                        if (!copy.Shows(variant))
                            continue;

                        if (above == Vector3.zero && copy.IsAtOriginalPlace)
                            showsOriginal = true;
                        else
                            moves.Add(Matrix4x4.Translate(above) * copy.Move);
                    }

                var container = SyncPart(realPart, moves, showsOriginal, ref isChanged);

                if (container != null)
                    keptContainers.Add(container);
            }

            return isChanged;
        }

        // Контейнер — сосед оригинала: в экземпляр чужого префаба его не положить.
        private static bool CanHoldCopies(Transform realPart)
        {
            var parent = realPart.parent;
            return parent == null || !PrefabUtility.IsPartOfPrefabInstance(parent.gameObject);
        }

        // Вариант узора, под которым лежит часть: ребёнок повтора на пути к ней.
        private static int VariantOf(LiveBuilderArray array, Transform realPart)
        {
            var node = realPart;

            while (node != null && node.parent != array.transform)
                node = node.parent;

            return node != null ? LiveBuilderArrayPattern.VariantIndexOf(array, node) : LiveBuilderArrayPattern.AllVariants;
        }


        // ── Одна часть ───────────────────────────────────────────────────

        // Копии одной части по переносам; оригинал на месте показан или выключен. Копий нет и оригинал показан — контейнер не нужен.
        private static LiveBuilderGeneratedCopies SyncPart(Transform realPart, List<Matrix4x4> moves, bool showsOriginal, ref bool isChanged)
        {
            var container = FindContainer(realPart);

            if (moves.Count == 0 && showsOriginal)
            {
                if (container != null)
                {
                    Remove(container);
                    isChanged = true;
                }

                return null;
            }

            if (container == null)
            {
                container = CreateContainer(realPart);
                isChanged = true;
            }

            var content = ContentOf(realPart);

            if (container.BuiltContent != content)
            {
                DestroyCopies(container, 0);
                container.BuiltContent = content;
                isChanged = true;
            }

            isChanged |= PlaceCopies(container, realPart, moves);
            isChanged |= ShowOriginal(container, realPart, showsOriginal);
            return container;
        }

        private static LiveBuilderGeneratedCopies FindContainer(Transform realPart)
        {
            var parent = realPart.parent;

            if (parent == null)
            {
                foreach (var root in realPart.gameObject.scene.GetRootGameObjects())
                    if (root.TryGetComponent<LiveBuilderGeneratedCopies>(out var rootContainer) && rootContainer.Original == realPart.gameObject)
                        return rootContainer;

                return null;
            }

            foreach (Transform sibling in parent)
                if (sibling.TryGetComponent<LiveBuilderGeneratedCopies>(out var container) && container.Original == realPart.gameObject)
                    return container;

            return null;
        }

        private static LiveBuilderGeneratedCopies CreateContainer(Transform realPart)
        {
            var containerObject = new GameObject(realPart.name + ContainerSuffix) { hideFlags = ContainerFlags };

            if (realPart.parent != null)
                containerObject.transform.SetParent(realPart.parent, false);
            else
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(containerObject, realPart.gameObject.scene);

            var container = containerObject.AddComponent<LiveBuilderGeneratedCopies>();
            container.Original = realPart.gameObject;
            return container;
        }


        // Копии по местам: недостающие — создать, лишние — убрать. true — что-то сдвинулось или появилось.
        private static bool PlaceCopies(LiveBuilderGeneratedCopies container, Transform realPart, List<Matrix4x4> moves)
        {
            var copies = container.Copies;
            var isChanged = copies.RemoveAll(copy => copy == null) > 0;

            for (var index = 0; index < moves.Count; index++)
            {
                if (index >= copies.Count)
                {
                    copies.Add(CreateCopy(container, realPart));
                    isChanged = true;
                }

                isChanged |= Place(copies[index].transform, realPart, moves[index], index);
            }

            if (copies.Count > moves.Count)
            {
                DestroyCopies(container, moves.Count);
                isChanged = true;
            }

            return isChanged;
        }

        // Экземпляр префаба с правками оригинала или клон; включён, без своего повтора, со своими мешами.
        private static GameObject CreateCopy(LiveBuilderGeneratedCopies container, Transform realPart)
        {
            var copy = ObjectCopyFactory.Instantiate(realPart.gameObject, container.transform);

            foreach (var ownArray in copy.GetComponents<LiveBuilderArray>())
                Object.DestroyImmediate(ownArray);

            copy.SetActive(true);
            ObjectCopyFactory.GiveOwnMeshes(realPart.gameObject, copy, HideFlags.None);

            foreach (var node in copy.GetComponentsInChildren<Transform>(true))
                node.gameObject.hideFlags = CopyFlags;

            return copy;
        }

        // Копия = перенос × оригинал; масштаб — оригинала (контейнер — в тех же осях, что оригинал). Меняется только то, что отличается.
        private static bool Place(Transform copy, Transform realPart, Matrix4x4 move, int index)
        {
            var position = move.MultiplyPoint3x4(realPart.position);
            var rotation = move.rotation * realPart.rotation;
            var name = realPart.name + " " + (index + 2);
            var isChanged = false;

            if (copy.position != position || copy.rotation != rotation)
            {
                copy.SetPositionAndRotation(position, rotation);
                isChanged = true;
            }

            if (copy.localScale != realPart.localScale)
            {
                copy.localScale = realPart.localScale;
                isChanged = true;
            }

            if (copy.name != name)
                copy.name = name;

            return isChanged;
        }

        // Выключенный руками оригинал повтор не включает: включает только тот, что выключил сам.
        private static bool ShowOriginal(LiveBuilderGeneratedCopies container, Transform realPart, bool showsOriginal)
        {
            if (!showsOriginal && realPart.gameObject.activeSelf)
            {
                realPart.gameObject.SetActive(false);
                container.HidesOriginal = true;
                return true;
            }

            if (showsOriginal && container.HidesOriginal)
            {
                realPart.gameObject.SetActive(true);
                container.HidesOriginal = false;
                return true;
            }

            return false;
        }


        // ── Убрать ───────────────────────────────────────────────────────

        // Контейнер больше не нужен: копии — вон, выключенный повтором оригинал — обратно.
        private static void Remove(LiveBuilderGeneratedCopies container)
        {
            if (container.HidesOriginal && container.Original != null)
                container.Original.SetActive(true);

            Object.DestroyImmediate(container.gameObject);
        }


        private static void DestroyCopies(LiveBuilderGeneratedCopies container, int keepCount)
        {
            var copies = container.Copies;

            for (var index = copies.Count - 1; index >= keepCount; index--)
            {
                if (copies[index] != null)
                    Object.DestroyImmediate(copies[index]);

                copies.RemoveAt(index);
            }
        }


        // ── Подпись содержимого ──────────────────────────────────────────

        // Что внутри оригинала: имена, слои, компоненты. Место, включённость и повтор самого оригинала — не его содержимое.
        private static string ContentOf(Transform realPart)
        {
            var content = new Hash128();

            foreach (var node in realPart.GetComponentsInChildren<Transform>(true))
            {
                content.Append(node.name);
                content.Append(node.gameObject.layer);

                if (node != realPart)
                    content.Append(node.gameObject.activeSelf ? 1 : 0);

                foreach (var component in node.GetComponents<Component>())
                    if (component != null && !(node == realPart && (component is Transform || component is LiveBuilderArray)))
                        content.Append(EditorJsonUtility.ToJson(component));
            }

            return content.ToString();
        }
    }
}
