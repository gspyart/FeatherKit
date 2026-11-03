using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Одна очередь пересборки на весь Live Builder. Любая правка, Ctrl+Z, точка сплайна, открытие сцены — один проход в строгом порядке:
    /// привязки к зданиям → точки перегородок на стенах здания → проёмы и вырезы на своих местах линии → объекты из коробок → здания → стены по линии. Каждый следующий шаг берёт уже свежее от предыдущего
    /// (вырезы объекта режут здания и стены, привязки задают копии по этажам), поэтому одна правка — один проход, без отставания.
    /// Заодно говорит <see cref="BoxModelPrefabInstances"/>, какие экземпляры префабов трогали: дорогая сверка их правок — только у них.
    /// </summary>
    [InitializeOnLoad]
    public static class LiveBuilderRebuildQueue
    {
        // Пауза после правки, чтобы пачка изменений дала один проход, с.
        private const double CheckDelaySeconds = 0.05;

        private static bool isCheckPending;
        private static double checkTime;

        // Идёт проход — новые просьбы проверить не нужны: следующие шаги прохода и так возьмут свежее.
        private static bool isPassRunning;


        static LiveBuilderRebuildQueue()
        {
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            Undo.undoRedoPerformed += OnUndoRedo;
            Spline.Changed += OnSplineChanged;
            EditorApplication.update += OnEditorUpdate;
            EditorSceneManager.sceneOpened += OnSceneOpened;

            // Скрытые копии массива правок редактора не дают, а их вырезы режут стены.
            ArrayModifierBuilder.PreviewCopiesChanged += RequestCheck;
            RequestCheck();
        }


        // ── Очередь ──────────────────────────────────────────────────────

        public static void RequestCheck()
        {
            if (isPassRunning)
                return;

            isCheckPending = true;
            checkTime = EditorApplication.timeSinceStartup + CheckDelaySeconds;
        }


        // Один проход: каждый шаг сам решает, что у него устарело. Сверка привязок и проёмов правит числа без событий редактора —
        // поэтому после неё расчёт коробок забывается.
        private static void RebuildChanged()
        {
            isPassRunning = true;
            GeometryCutterLookup.BeginPass();

            try
            {
                SplineBuildingLinkSync.SyncAll();
                SplineWallBuildingAnchors.SyncAll();
                HoleCutterLineSync.SyncAll();
                BoxModelRebuilder.ForgetCachedResults();
                BoxModelRebuilder.RebuildChangedModels();
                SplineBuildingRebuilder.RebuildChangedBuildings();
                SplineWallRebuilder.RebuildChangedWalls();
            }
            finally
            {
                GeometryCutterLookup.EndPass();
                isPassRunning = false;
            }
        }


        // ── События редактора ────────────────────────────────────────────

        // Поменяли числа объекта — забыть сверку только его экземпляра префаба; что-то другое (создали, удалили, переставили) — все сверки.
        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            BoxModelRebuilder.ForgetCachedResults();

            for (var index = 0; index < stream.length; index++)
            {
                if (stream.GetEventType(index) != ObjectChangeKind.ChangeGameObjectOrComponentProperties)
                {
                    BoxModelPrefabInstances.ForgetAllChecks();
                    continue;
                }

                stream.GetChangeGameObjectOrComponentPropertiesEvent(index, out var change);
                var changed = EditorUtility.EntityIdToObject(change.entityId);
                var changedObject = changed as GameObject ?? (changed as Component)?.gameObject;

                if (changedObject != null)
                    BoxModelPrefabInstances.ForgetCheck(changedObject);
                else
                    BoxModelPrefabInstances.ForgetAllChecks();
            }

            RequestCheck();
        }

        private static void OnUndoRedo()
        {
            BoxModelRebuilder.ForgetCachedResults();
            BoxModelPrefabInstances.ForgetAllChecks();
            RequestCheck();
        }

        private static void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            BoxModelRebuilder.ForgetCachedResults();
            RequestCheck();
        }

        // Открытие сцены — не правка, само проверку не зовёт; а меши со сценой могли разойтись, пока она была закрыта.
        private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            BoxModelPrefabInstances.ForgetAllChecks();
            BoxModelRebuilder.ForgetCachedResults();
            RequestCheck();
        }

        private static void OnEditorUpdate()
        {
            if (!isCheckPending || EditorApplication.timeSinceStartup < checkTime || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            isCheckPending = false;
            RebuildChanged();
        }
    }
}
