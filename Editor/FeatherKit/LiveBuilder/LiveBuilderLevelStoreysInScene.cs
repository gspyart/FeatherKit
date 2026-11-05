using System;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Где в открытой сцене лежат этажи уровня (<see cref="LiveBuilderLevelStoreys"/>): найти их в активной сцене и положить новые, если их там нет.
    /// Найденное помнится до смены активной сцены, Ctrl+Z или до того, как в сцене добавили или убрали объекты:
    /// этажи спрашивают на каждый кадр камеры и каждую коробку при отрисовке, обходить сцену каждый раз нельзя.
    /// </summary>
    [InitializeOnLoad]
    public static class LiveBuilderLevelStoreysInScene
    {
        // Имя объекта, который кладётся в сцену.
        private const string HolderName = "Live Builder Level";

        // Найденные этажи и сцена, в которой искали; null — в этой сцене их нет.
        private static LiveBuilderLevelStoreys foundStoreys;
        private static Scene searchedScene;
        private static bool isSearched;


        static LiveBuilderLevelStoreysInScene()
        {
            ObjectChangeEvents.changesPublished += ForgetOnStructureChange;
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) => ForgetAndNotify();
            Undo.undoRedoPerformed += ForgetAndNotify;
        }


        // Этажи уровня могли стать другими: сменилась активная сцена или Ctrl+Z.
        public static event Action Changed;


        // ── Поиск ────────────────────────────────────────────────────────

        // Этажи уровня активной сцены; нет их там — null.
        public static LiveBuilderLevelStoreys Find()
        {
            var activeScene = SceneManager.GetActiveScene();

            // Найденный объект удалили — искать заново.
            if (isSearched && activeScene == searchedScene && (foundStoreys is null || foundStoreys != null))
                return foundStoreys;

            searchedScene = activeScene;
            isSearched = true;
            foundStoreys = FindIn(activeScene);
            return foundStoreys;
        }

        private static LiveBuilderLevelStoreys FindIn(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            foreach (var root in scene.GetRootGameObjects())
            {
                var storeys = root.GetComponentInChildren<LiveBuilderLevelStoreys>(true);

                if (storeys != null)
                    return storeys;
            }

            return null;
        }


        // Этажи уровня активной сцены для правки, записанные в Undo. Нет их там — кладёт объект с этими числами: с ними уровень и выглядел до сих пор.
        public static LiveBuilderLevelStoreys FindOrCreate(float storeyHeight, float groundLevel, string undoName)
        {
            var storeys = Find();

            if (storeys == null)
                storeys = Create(storeyHeight, groundLevel);

            Undo.RecordObject(storeys, undoName);
            return storeys;
        }

        // Объект в корне активной сцены, только для редактора: в сборку игры не попадает.
        private static LiveBuilderLevelStoreys Create(float storeyHeight, float groundLevel)
        {
            var holder = new GameObject(HolderName) { tag = "EditorOnly" };
            var storeys = holder.AddComponent<LiveBuilderLevelStoreys>();
            storeys.StoreyHeight = storeyHeight;
            storeys.GroundLevel = groundLevel;
            Undo.RegisterCreatedObjectUndo(holder, "Store Level Storeys In Scene");

            foundStoreys = storeys;
            searchedScene = holder.scene;
            isSearched = true;
            return storeys;
        }


        // ── Забыть найденное ─────────────────────────────────────────────

        // Объекты в сцене добавили, удалили или поменяли им компоненты — этажи могли появиться или пропасть.
        private static void ForgetOnStructureChange(ref ObjectChangeEventStream stream)
        {
            for (var index = 0; index < stream.length; index++)
                if (stream.GetEventType(index) is ObjectChangeKind.CreateGameObjectHierarchy or ObjectChangeKind.DestroyGameObjectHierarchy
                    or ObjectChangeKind.ChangeGameObjectStructure or ObjectChangeKind.ChangeGameObjectStructureHierarchy or ObjectChangeKind.ChangeScene)
                {
                    isSearched = false;
                    return;
                }
        }

        private static void ForgetAndNotify()
        {
            isSearched = false;
            Changed?.Invoke();
        }
    }
}
