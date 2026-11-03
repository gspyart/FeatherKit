using System.Collections.Generic;
using FeatherKit.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Держит копии модификаторов Array (<see cref="ArrayModifier"/>) вслед за их объектом:
    /// - объект сдвинули, повернули, переложили к другому родителю — копии переставляются
    /// - объект поправили (компонент, материал, ребёнка) — копии собираются заново и повторяют правку
    /// - Ctrl+Z, открытие сцены — отставшие массивы пересобираются
    /// - объект удалили — его копии удаляются следом
    /// - выделил копию или что-то внутри неё — выделяется сам объект: копии правят через него, ручные правки затёрлись бы.
    ///   Нужно править копии по одной — «Apply»
    /// Пересборка — после кадра, где пришла правка: правку в это время ещё записывает Unity, а посреди Ctrl+Z нельзя записывать новые шаги отмены.
    /// Пересборка вливается в шаг отмены самой правки: одно нажатие Ctrl+Z отменяет и правку, и то, что она сделала с копиями.
    /// </summary>
    [InitializeOnLoad]
    public static class ArrayModifierWatcher
    {
        private static readonly HashSet<ArrayModifier> ArraysToMove = new();
        private static readonly HashSet<ArrayModifier> ArraysToRecreate = new();

        // Копии каждого массива по его объекту: объект удалили — самого массива уже нет, а знать, чьи копии убрать, надо.
        private static readonly Dictionary<EntityId, List<GameObject>> CopiesByArrayObject = new();
        private static readonly List<GameObject> CopiesToRemove = new();

        private static bool isUpdateQueued;
        private static bool isStaleCheckQueued;

        // Шаг отмены, в котором пришла правка, и его имя; -1 — правок в очереди нет.
        private static int changeUndoGroup = -1;
        private static string changeUndoName;


        static ArrayModifierWatcher()
        {
            Undo.undoRedoPerformed += QueueStaleCheck;
            EditorSceneManager.sceneOpened += (_, _) => QueueStaleCheck();
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            Selection.selectionChanged += SelectOriginalsInsteadOfCopies;

            // После перезагрузки скриптов память пуста: сверить массивы и запомнить их копии.
            QueueStaleCheck();
        }


        // ── Сборка ───────────────────────────────────────────────────────

        // После Ctrl+Z и открытия сцены — сверить массивы кадром позже: посреди отмены Unity не даёт записывать новые шаги отмены.
        private static void QueueStaleCheck()
        {
            if (isStaleCheckQueued)
                return;

            isStaleCheckQueued = true;
            EditorApplication.delayCall += RebuildStaleArrays;
        }

        private static void RebuildStaleArrays()
        {
            isStaleCheckQueued = false;

            foreach (var array in StageUtility.GetCurrentStageHandle().FindComponentsOfType<ArrayModifier>())
                if (!ArrayModifierBuilder.IsUpToDate(array))
                    ArrayModifierBuilder.Rebuild(array);

            RememberCopies();
        }


        // Чья правка: сам модификатор — его настройки, их пересоберёт окно; transform объекта — переставить; остальное в объекте — собрать заново.
        // Удалили объект массива — убрать и его копии.
        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            var arrays = StageUtility.GetCurrentStageHandle().FindComponentsOfType<ArrayModifier>();

            if (arrays.Length == 0 && CopiesByArrayObject.Count == 0)
                return;

            for (var index = 0; index < stream.length; index++)
            {
                if (stream.GetEventType(index) == ObjectChangeKind.DestroyGameObjectHierarchy)
                {
                    QueueCopiesOfDestroyed(ref stream, index);
                    continue;
                }

                var changed = ChangedObjectOf(ref stream, index);

                if (changed == null || changed is ArrayModifier)
                    continue;

                var changedTransform = changed is Component component ? component.transform : changed is GameObject gameObject ? gameObject.transform : null;

                if (changedTransform == null)
                    continue;

                foreach (var array in arrays)
                {
                    if (!changedTransform.IsChildOf(array.transform))
                        continue;

                    var isOwnPlace = changed == array.transform || changed == array.gameObject && stream.GetEventType(index) == ObjectChangeKind.ChangeGameObjectParent;
                    (isOwnPlace ? ArraysToMove : ArraysToRecreate).Add(array);
                }
            }

            RememberCopies();
            QueueUpdate();
        }

        // Удалили объект массива — его копии в очередь на удаление. Копии удалили вместе с ним — в очереди их просто не окажется живыми.
        private static void QueueCopiesOfDestroyed(ref ObjectChangeEventStream stream, int index)
        {
            stream.GetDestroyGameObjectHierarchyEvent(index, out var destroyed);

            if (!CopiesByArrayObject.Remove(destroyed.entityId, out var copies))
                return;

            CopiesToRemove.AddRange(copies);
        }

        private static Object ChangedObjectOf(ref ObjectChangeEventStream stream, int index)
        {
            switch (stream.GetEventType(index))
            {
                case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                    stream.GetChangeGameObjectOrComponentPropertiesEvent(index, out var properties);
                    return EditorUtility.EntityIdToObject(properties.entityId);

                case ObjectChangeKind.ChangeGameObjectStructure:
                    stream.GetChangeGameObjectStructureEvent(index, out var structure);
                    return EditorUtility.EntityIdToObject(structure.entityId);

                case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    stream.GetChangeGameObjectStructureHierarchyEvent(index, out var hierarchy);
                    return EditorUtility.EntityIdToObject(hierarchy.entityId);

                case ObjectChangeKind.ChangeGameObjectParent:
                    stream.GetChangeGameObjectParentEvent(index, out var parent);
                    return EditorUtility.EntityIdToObject(parent.entityId);

                case ObjectChangeKind.CreateGameObjectHierarchy:
                    stream.GetCreateGameObjectHierarchyEvent(index, out var created);
                    return EditorUtility.EntityIdToObject(created.entityId);

                default:
                    return null;
            }
        }

        // Запомнить шаг отмены правки и пересобрать кадром позже.
        private static void QueueUpdate()
        {
            if (ArraysToMove.Count + ArraysToRecreate.Count + CopiesToRemove.Count == 0)
                return;

            if (changeUndoGroup < 0)
            {
                changeUndoGroup = Undo.GetCurrentGroup();
                changeUndoName = Undo.GetCurrentGroupName();
            }

            if (isUpdateQueued)
                return;

            isUpdateQueued = true;
            EditorApplication.delayCall += UpdateChangedArrays;
        }

        private static void UpdateChangedArrays()
        {
            isUpdateQueued = false;

            foreach (var copy in CopiesToRemove)
                if (copy != null)
                    Undo.DestroyObjectImmediate(copy);

            foreach (var array in ArraysToRecreate)
                if (array != null && !ArrayModifierBuilder.IsUpToDate(array, true))
                    ArrayModifierBuilder.Rebuild(array, true);

            foreach (var array in ArraysToMove)
                if (array != null && !ArraysToRecreate.Contains(array) && !ArrayModifierBuilder.IsUpToDate(array))
                    ArrayModifierBuilder.Rebuild(array);

            // Пересборка — в том же шаге отмены, что и правка, и под её именем: Ctrl+Z одним нажатием возвращает и то и другое.
            // Шага нет (-1) — не сворачивать: иначе слилась бы вся история отмены.
            if (changeUndoGroup >= 0)
            {
                Undo.CollapseUndoOperations(changeUndoGroup);

                if (!string.IsNullOrEmpty(changeUndoName))
                    Undo.SetCurrentGroupName(changeUndoName);
            }

            changeUndoGroup = -1;
            changeUndoName = null;

            CopiesToRemove.Clear();
            ArraysToMove.Clear();
            ArraysToRecreate.Clear();
            RememberCopies();
        }


        // ── Чьи копии ────────────────────────────────────────────────────

        // Копии всех массивов сцены по их объектам — на случай, если объект удалят.
        private static void RememberCopies()
        {
            CopiesByArrayObject.Clear();

            foreach (var array in StageUtility.GetCurrentStageHandle().FindComponentsOfType<ArrayModifier>())
                CopiesByArrayObject[array.gameObject.GetEntityId()] = new List<GameObject>(array.Copies);
        }


        // ── Выделение ────────────────────────────────────────────────────

        // Копии и всё внутри них — заменить на их объект; остальное выделение — как есть.
        private static void SelectOriginalsInsteadOfCopies()
        {
            var selected = Selection.gameObjects;

            if (selected.Length == 0)
                return;

            var arrays = StageUtility.GetCurrentStageHandle().FindComponentsOfType<ArrayModifier>();

            if (arrays.Length == 0)
                return;

            var result = new List<Object>();
            var isRedirected = false;

            foreach (var gameObject in selected)
            {
                var owner = ArrayOwningCopy(gameObject.transform, arrays);

                if (owner == null)
                {
                    result.Add(gameObject);
                    continue;
                }

                isRedirected = true;

                if (!result.Contains(owner.gameObject))
                    result.Add(owner.gameObject);
            }

            if (!isRedirected)
                return;

            Selection.objects = result.ToArray();

            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.ShowNotification(new GUIContent("A copy of an Array — edit the original, or Apply"), 1.5f);
        }

        // Массив, чья копия — этот объект или его родитель.
        private static ArrayModifier ArrayOwningCopy(Transform node, ArrayModifier[] arrays)
        {
            foreach (var array in arrays)
                foreach (var copy in array.Copies)
                    if (copy != null && node.IsChildOf(copy.transform))
                        return array;

            return null;
        }
    }
}
