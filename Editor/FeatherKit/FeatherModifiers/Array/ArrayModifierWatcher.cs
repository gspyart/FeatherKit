using System.Collections.Generic;
using FeatherKit.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Держит скрытые копии модификаторов Array (<see cref="ArrayModifier"/>) вслед за их объектом:
    /// - объект сдвинули, повернули, переложили к другому родителю — копии переставляются
    /// - объект поправили (компонент, материал, ребёнка) — копии собираются заново и повторяют правку
    /// - Ctrl+Z, открытие сцены, выход из игры — отставшие массивы пересобираются
    /// - перезагрузились скрипты — массивы забирают свои копии обратно; ничьи копии (объект удалили, модификатор сняли) — удаляются
    /// - выделил копию или что-то внутри неё — выделяется сам объект: копии правят через него. Нужно править копии по одной — «Apply»
    /// Пересборка — после кадра, где пришла правка: правку в это время ещё записывает Unity.
    /// </summary>
    [InitializeOnLoad]
    public static class ArrayModifierWatcher
    {
        private static readonly HashSet<ArrayModifier> ArraysToMove = new();
        private static readonly HashSet<ArrayModifier> ArraysToRecreate = new();

        private static bool isUpdateQueued;
        private static bool isStaleCheckQueued;


        static ArrayModifierWatcher()
        {
            Undo.undoRedoPerformed += QueueStaleCheck;
            EditorSceneManager.sceneOpened += (_, _) => QueueStaleCheck();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            Selection.selectionChanged += SelectOriginalsInsteadOfCopies;

            // После перезагрузки скриптов массивы не помнят свои копии: забрать их по меткам.
            ArrayModifierBuilder.AdoptOrRemoveStrayCopies();
            QueueStaleCheck();
        }


        // ── Сборка ───────────────────────────────────────────────────────

        // Из игры вернулись — сцена перезагружена без скрытых копий: собрать.
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                QueueStaleCheck();
        }

        private static void QueueStaleCheck()
        {
            if (isStaleCheckQueued)
                return;

            isStaleCheckQueued = true;
            EditorApplication.delayCall += RebuildStaleArrays;
        }

        // Ничьи копии — прочь, отставшие массивы — пересобрать. В игре копии ставит сам компонент — редактор не вмешивается.
        private static void RebuildStaleArrays()
        {
            isStaleCheckQueued = false;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ArrayModifierBuilder.AdoptOrRemoveStrayCopies();

            foreach (var array in StageUtility.GetCurrentStageHandle().FindComponentsOfType<ArrayModifier>())
                if (!ArrayModifierBuilder.IsUpToDate(array))
                    ArrayModifierBuilder.Rebuild(array);
        }


        // Чья правка: сам модификатор — его настройки, их пересоберёт окно; transform объекта — переставить; остальное в объекте — собрать заново.
        // Что-то удалили — может, объект массива: его копии станут ничьими.
        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var arrays = StageUtility.GetCurrentStageHandle().FindComponentsOfType<ArrayModifier>();

            for (var index = 0; index < stream.length; index++)
            {
                if (stream.GetEventType(index) == ObjectChangeKind.DestroyGameObjectHierarchy)
                {
                    QueueStaleCheck();
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
            if (ArraysToMove.Count + ArraysToRecreate.Count == 0 || isUpdateQueued)
                return;

            isUpdateQueued = true;
            EditorApplication.delayCall += UpdateChangedArrays;
        }

        private static void UpdateChangedArrays()
        {
            isUpdateQueued = false;

            foreach (var array in ArraysToRecreate)
                if (array != null && !ArrayModifierBuilder.IsUpToDate(array, true))
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
            var result = new List<Object>();
            var isRedirected = false;

            foreach (var gameObject in selected)
            {
                var marker = gameObject.GetComponentInParent<ArrayPreviewCopy>(true);
                var owner = marker != null ? marker.Owner : null;

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
    }
}
