using FeatherKit.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Держит статичным всё внутри групп с галкой «Keeps Contents Static» (<see cref="ObjectGroup"/>):
    /// в иерархии что-то добавили или переложили — ставит все флаги Static тем, у кого их нет.
    /// Только в редакторе и не во время игры. Сам переключатель Static — <see cref="ObjectGroupSwitches"/>.
    /// </summary>
    [InitializeOnLoad]
    public static class ObjectGroupStaticUpdater
    {
        public const StaticEditorFlags AllStaticFlags = (StaticEditorFlags)~0;


        static ObjectGroupStaticUpdater()
        {
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }


        // ── Реакция на изменения ─────────────────────────

        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (HasHierarchyChanges(ref stream))
                MakeAllStaticGroupsStatic();
        }

        // Состав иерархии поменялся: новые объекты, другой родитель, компоненты, правка префаба; или галку группы включили в инспекторе.
        // Движение и поворот сюда не входят: их много, а состав они не меняют.
        private static bool HasHierarchyChanges(ref ObjectChangeEventStream stream)
        {
            for (var index = 0; index < stream.length; index++)
            {
                switch (stream.GetEventType(index))
                {
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                    case ObjectChangeKind.ChangeGameObjectParent:
                    case ObjectChangeKind.ChangeGameObjectStructure:
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    case ObjectChangeKind.UpdatePrefabInstances:
                        return true;

                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                        stream.GetChangeGameObjectOrComponentPropertiesEvent(index, out var change);

                        if (EditorUtility.EntityIdToObject(change.entityId) is ObjectGroup)
                            return true;

                        break;
                }
            }

            return false;
        }

        private static void MakeAllStaticGroupsStatic()
        {
            foreach (var group in Object.FindObjectsByType<ObjectGroup>(FindObjectsInactive.Include))
            {
                if (group.KeepsContentsStatic)
                    MakeContentsStatic(group);
            }
        }


        // ── Флаги ────────────────────────────────────────

        // Сцену помечает изменённой, только если что-то поменялось: иначе каждая правка рядом «портила» бы файл.
        public static void MakeContentsStatic(ObjectGroup group)
        {
            var hasChanges = false;

            foreach (var child in group.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetStaticEditorFlags(child.gameObject) == AllStaticFlags)
                    continue;

                GameObjectUtility.SetStaticEditorFlags(child.gameObject, AllStaticFlags);
                hasChanges = true;
            }

            if (hasChanges)
                EditorSceneManager.MarkSceneDirty(group.gameObject.scene);
        }
    }
}
