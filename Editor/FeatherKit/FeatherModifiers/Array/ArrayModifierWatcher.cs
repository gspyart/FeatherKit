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
    /// - выделил копию или что-то внутри неё — выделяется сам объект: копии правят через него, ручные правки затёрлись бы.
    ///   Нужно править копии по одной — «Apply»
    /// Пересборка — после кадра, где пришла правка: правку в это время ещё записывает Unity.
    /// </summary>
    [InitializeOnLoad]
    public static class ArrayModifierWatcher
    {
        private static readonly HashSet<ArrayModifier> ArraysToMove = new();
        private static readonly HashSet<ArrayModifier> ArraysToRecreate = new();
        private static bool isUpdateQueued;


        static ArrayModifierWatcher()
        {
            Undo.undoRedoPerformed += RebuildStaleArrays;
            EditorSceneManager.sceneOpened += (_, _) => RebuildStaleArrays();
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            Selection.selectionChanged += SelectOriginalsInsteadOfCopies;
        }


        // ── Сборка ───────────────────────────────────────────────────────

        private static void RebuildStaleArrays()
        {
            foreach (var array in StageUtility.GetCurrentStageHandle().FindComponentsOfType<ArrayModifier>())
                if (!ArrayModifierBuilder.IsUpToDate(array))
                    ArrayModifierBuilder.Rebuild(array);
        }


        // Чья правка: сам модификатор — его настройки, их пересоберёт окно; transform объекта — переставить; остальное в объекте — собрать заново.
        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            var arrays = StageUtility.GetCurrentStageHandle().FindComponentsOfType<ArrayModifier>();

            if (arrays.Length == 0)
                return;

            for (var index = 0; index < stream.length; index++)
            {
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

            QueueUpdate();
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

        private static void QueueUpdate()
        {
            if (isUpdateQueued || ArraysToMove.Count + ArraysToRecreate.Count == 0)
                return;

            isUpdateQueued = true;
            EditorApplication.delayCall += UpdateChangedArrays;
        }

        private static void UpdateChangedArrays()
        {
            isUpdateQueued = false;

            foreach (var array in ArraysToRecreate)
                if (array != null)
                    ArrayModifierBuilder.Rebuild(array, true);

            foreach (var array in ArraysToMove)
                if (array != null && !ArraysToRecreate.Contains(array) && !ArrayModifierBuilder.IsUpToDate(array))
                    ArrayModifierBuilder.Rebuild(array);

            ArraysToMove.Clear();
            ArraysToRecreate.Clear();
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
