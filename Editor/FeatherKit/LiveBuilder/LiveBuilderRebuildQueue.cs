using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Одна очередь пересборки на весь Live Builder. Любая правка, Ctrl+Z, точка сплайна, открытие сцены — один проход в строгом порядке:
    /// здания на этажах уровня → этажи зданий → точки перегородок на стенах здания → проёмы на своих местах линии
    /// → настоящие копии повторов → объекты из коробок → колонны → здания → стены по линии.
    /// - во время жеста (<see cref="LiveBuilderGestureTracker"/>) — черновой проход (<see cref="LiveBuilderBuildQuality.Draft"/>) по задетому (<see cref="LiveBuilderRebuildScope"/>)
    /// - после жеста — чистовой по всей сцене через 0,3 с; правка вне жеста — сразу чистовой; он же доводит черновики, которых не нашёл, и чистит память об удалённом
    /// </summary>
    [InitializeOnLoad]
    public static class LiveBuilderRebuildQueue
    {
        // Чистовой проход — через столько после последней правки или конца жеста, с.
        private const double FinalDelaySeconds = 0.3;

        // Правка вне жеста, Ctrl+Z, открытие сцены, сплайн кодом — чистовой почти сразу, без черновика, с.
        private const double QuickFinalDelaySeconds = 0.05;

        private static bool isDraftPending;
        private static bool isFinalPending;
        private static double finalTime;
        private static double lastGestureTime;

        // Идёт проход — новые просьбы проверить не нужны: следующие шаги прохода и так возьмут свежее.
        private static bool isPassRunning;

        // Что задели правки с прошлого прохода — для чернового прохода.
        private static LiveBuilderRebuildScope pendingScope = new();


        static LiveBuilderRebuildQueue()
        {
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            Undo.undoRedoPerformed += OnUndoRedo;
            Spline.Changed += OnSplineChanged;
            EditorApplication.update += OnEditorUpdate;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;

            // Скрытые копии массива правок редактора не дают, а их вырезы режут стены.
            ArrayModifierBuilder.PreviewCopiesChanged += RequestCheck;
            RequestQuickFinal();
        }


        // Идёт проход: то, что он сохраняет сам (пересобранный префаб), — уже чистовое.
        public static bool IsPassRunning => isPassRunning;


        // ── Очередь ──────────────────────────────────────────────────────

        // Правка неизвестно чего: в жесте — черновой проход по всей сцене, вне жеста — чистовой.
        public static void RequestCheck()
        {
            if (isPassRunning)
                return;

            pendingScope.MarkWholeScene();
            MarkPending();
        }


        // Правка объекта кодом, без событий редактора (замер, проверки): черновой проход — только по тому, что она могла задеть.
        public static void RequestCheck(GameObject changed)
        {
            if (isPassRunning)
                return;

            BoxModelRebuilder.ForgetCachedResults();
            NoteChanged(changed);
            MarkPending();
        }

        // Объект — в область чернового прохода; вырезы в нём, поменянные в жесте, режут заготовку (<see cref="GeometryCutterDragBlanks"/>).
        private static void NoteChanged(GameObject changed)
        {
            BoxModelPrefabInstances.ForgetCheck(changed);
            var cutters = changed.GetComponentsInChildren<GeometryCutter>(true);
            pendingScope.AddChangedObject(changed, cutters);

            if (LiveBuilderGestureTracker.IsGestureActive)
                foreach (var cutter in cutters)
                    GeometryCutterDragBlanks.NoteMoving(cutter);
        }

        // Черновик вне жеста мигнул бы фаской — там сразу чистовой, как после Ctrl+Z.
        private static void MarkPending()
        {
            if (!LiveBuilderGestureTracker.IsGestureActive)
            {
                RequestQuickFinal();
                return;
            }

            isDraftPending = true;
            isFinalPending = true;
            finalTime = EditorApplication.timeSinceStartup + FinalDelaySeconds;
        }

        private static void RequestQuickFinal()
        {
            if (isPassRunning)
                return;

            var quickTime = EditorApplication.timeSinceStartup + QuickFinalDelaySeconds;
            finalTime = isFinalPending ? System.Math.Min(finalTime, quickTime) : quickTime;
            isFinalPending = true;
        }


        // Чистовой проход сейчас, без паузы: замер и проверки меняют сцену кодом, а события редактора о правке придут только на следующем кадре.
        public static void RunFinalPassNow()
        {
            RunPassNow(LiveBuilderBuildQuality.Final);
        }


        // Проход нужного качества сейчас: черновой — кадр жеста в замере и проверках.
        public static void RunPassNow(LiveBuilderBuildQuality quality)
        {
            if (isPassRunning)
                return;

            isDraftPending = false;

            if (quality == LiveBuilderBuildQuality.Final)
                isFinalPending = false;

            var scope = quality == LiveBuilderBuildQuality.Draft ? pendingScope : LiveBuilderRebuildScope.WholeScene;
            pendingScope = new LiveBuilderRebuildScope();
            RebuildChanged(quality, scope);
        }


        // Отложенный чистовой проход ещё не прошёл — сейчас: сцену сохраняют или запускают.
        public static void RunScheduledFinalPassNow()
        {
            if (isFinalPending)
                RunFinalPassNow();
        }


        // Один проход: каждый шаг сам решает, что у него устарело. Сверка этажей и проёмов правит числа без событий редактора —
        // поэтому после неё расчёт коробок забывается.
        private static void RebuildChanged(LiveBuilderBuildQuality quality, LiveBuilderRebuildScope scope)
        {
            isPassRunning = true;
            LiveBuilderRebuildProfiler.CountPass();
            GeometryCutterLookup.BeginPass();
            LiveBuilderMeshWriter.BeginPass();

            try
            {
                RunStep(LiveBuilderRebuildStage.LevelSync, () => SplineBuildingLevelSync.SyncAll(scope));
                RunStep(LiveBuilderRebuildStage.StoreySync, () => SplineBuildingStoreySync.SyncAll(scope));
                RunStep(LiveBuilderRebuildStage.AnchorSync, () => SplineWallBuildingAnchors.SyncAll(scope));
                RunStep(LiveBuilderRebuildStage.LineSync, () => GeometryCutterLineSync.SyncAll(scope));
                RunStep(LiveBuilderRebuildStage.ArraySync, SyncArrayCopies);

                // Копии повторов — после сверок: те двигают объекты, а копии считаются от их мест.
                LiveBuilderArrayCopyPoses.BeginPass();
                BoxModelRebuilder.ForgetCachedResults(scope);
                RunStep(LiveBuilderRebuildStage.BoxModelPass, () => BoxModelRebuilder.RebuildChangedModels(scope, quality));
                RunStep(LiveBuilderRebuildStage.ColumnPass, () => LiveBuilderColumnRebuilder.RebuildChangedColumns(scope, quality));
                RunStep(LiveBuilderRebuildStage.BuildingPass, () => SplineBuildingRebuilder.RebuildChangedBuildings(scope, quality));
                RunStep(LiveBuilderRebuildStage.WallPass, () => SplineWallRebuilder.RebuildChangedWalls(scope, quality));

                if (quality == LiveBuilderBuildQuality.Final && scope.IsWholeScene)
                    FinishWholeScenePass();
            }
            finally
            {
                LiveBuilderArrayCopyPoses.EndPass();
                LiveBuilderMeshWriter.EndPass();
                GeometryCutterLookup.EndPass();
                isPassRunning = false;
            }
        }

        // Настоящие копии повторов появились или переехали — их вырезы режут стены: список вырезов прохода собирается заново.
        private static void SyncArrayCopies()
        {
            if (LiveBuilderArraySync.SyncAll())
                GeometryCutterLookup.BeginPass();
        }

        // Ошибка одного шага — в консоль; следующие шаги всё равно идут.
        private static void RunStep(LiveBuilderRebuildStage stage, System.Action step)
        {
            try
            {
                using (LiveBuilderRebuildProfiler.Measure(stage))
                    step();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        // Черновик выключенного объекта или сцены за открытым префабом проход не находит — без доводки напрямую он ждал бы вечно.
        private static void FinishWholeScenePass()
        {
            LiveBuilderDraftBuilds.FinishAll();
            ForgetDestroyedObjects();
        }


        // ── Память об удалённом ──────────────────────────────────────────

        // Постройку удалили или закрыли её сцену — её расчёты в памяти больше не нужны.
        private static void ForgetDestroyedObjects()
        {
            BoxModelBuildResults.ForgetDestroyed();
            BoxModelRebuilder.ForgetDestroyed();
            SplineWallRebuilder.ForgetDestroyed();
            SplineBuildingUncutParts.ForgetDestroyed();
            GeometryCutterLastTargets.ForgetDestroyed();
        }


        // ── События редактора ────────────────────────────────────────────

        // Поменяли числа объекта — забыть сверку только его экземпляра префаба, черновик — по задетому им; что-то другое (создали, удалили, переставили) —
        // все сверки и черновик по всей сцене.
        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            LiveBuilderGestureTracker.NoteEdit();
            BoxModelRebuilder.ForgetCachedResults();

            for (var index = 0; index < stream.length; index++)
            {
                if (stream.GetEventType(index) != ObjectChangeKind.ChangeGameObjectOrComponentProperties)
                {
                    BoxModelPrefabInstances.ForgetAllChecks();
                    pendingScope.MarkWholeScene();
                    continue;
                }

                stream.GetChangeGameObjectOrComponentPropertiesEvent(index, out var change);
                var changed = EditorUtility.EntityIdToObject(change.entityId);
                var changedObject = changed as GameObject ?? (changed as Component)?.gameObject;

                if (changedObject != null)
                {
                    NoteChanged(changedObject);
                }
                else
                {
                    BoxModelPrefabInstances.ForgetAllChecks();
                    pendingScope.MarkWholeScene();
                }
            }

            MarkPending();
        }

        private static void OnUndoRedo()
        {
            BoxModelRebuilder.ForgetCachedResults();
            BoxModelPrefabInstances.ForgetAllChecks();
            RequestQuickFinal();
        }

        // Точку тянут ручкой — как правка жеста, черновик по постройке этого сплайна; сплайн поменяли кодом — сразу начисто.
        private static void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            LiveBuilderGestureTracker.NoteEdit();
            BoxModelRebuilder.ForgetCachedResults();

            if (!LiveBuilderGestureTracker.IsGestureActive)
            {
                RequestQuickFinal();
                return;
            }

            if (isPassRunning)
                return;

            pendingScope.AddChangedSpline(spline);
            MarkPending();
        }

        // Открытие сцены — не правка, само проверку не зовёт; а меши со сценой могли разойтись, пока она была закрыта.
        private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            BoxModelPrefabInstances.ForgetAllChecks();
            BoxModelRebuilder.ForgetCachedResults();
            RequestQuickFinal();
        }

        private static void OnSceneClosed(UnityEngine.SceneManagement.Scene scene)
        {
            ForgetDestroyedObjects();
        }

        // Пока идёт жест — только черновики; чистовой — через паузу после конца жеста. Черновик без прохода в очереди
        // (собран сразу в конце рисования) — тоже повод для чистового.
        private static void OnEditorUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var now = EditorApplication.timeSinceStartup;

            if (LiveBuilderGestureTracker.IsGestureActive)
            {
                lastGestureTime = now;

                if (isDraftPending)
                    RunPassNow(LiveBuilderBuildQuality.Draft);

                return;
            }

            if (!isFinalPending && LiveBuilderDraftBuilds.HasPending)
            {
                isFinalPending = true;
                finalTime = now + FinalDelaySeconds;
            }

            if (isFinalPending && now >= finalTime && now >= lastGestureTime + FinalDelaySeconds)
                RunPassNow(LiveBuilderBuildQuality.Final);
            else if (isDraftPending)
                RunPassNow(LiveBuilderBuildQuality.Draft);
        }
    }
}
