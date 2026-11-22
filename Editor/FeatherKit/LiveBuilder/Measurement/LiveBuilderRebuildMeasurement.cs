using System;
using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Замер пересборки на эталонном уровне (<see cref="LiveBuilderReferenceLevel"/>) — меню «Measure Rebuild»: отчёт в консоль и в буфер обмена.
    /// - уровень — в новой сцене вместо открытых; потом открытые сцены возвращаются, файлы мешей замера удаляются
    /// - первая сборка по стадиям, второй проход за ней, запись сцены
    /// - бюджетные действия ТЗ по многу раз: сдвиг коробки, сдвиг окна, этажи 9 → 10
    /// - у жестов — черновой проход и кадр с отрисовкой, отдельно — чистовой проход после отпускания
    /// </summary>
    public static class LiveBuilderRebuildMeasurement
    {
        private const int DefaultRunCount = 20;
        private const int SlowActionRunCount = 5;
        private const double SlowActionMilliseconds = 1000.0;

        // Шаг жеста — клетка сетки, м.
        private const float GridStep = 0.5f;

        // Бюджеты ТЗ, мс.
        private const double BoxMoveBudget = 30.0;
        private const double WindowMoveBudget = 16.0;
        private const double StoreysBudget = 150.0;


        // ── Меню ─────────────────────────────────────────────────────────

        [MenuItem("Tools/FeatherKit/Live Builder/Measure Rebuild")]
        private static void MeasureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var report = Measure(DefaultRunCount);
            Debug.Log(report);
            EditorGUIUtility.systemCopyBuffer = report;
        }


        // ── Замер ────────────────────────────────────────────────────────

        // runCount — прогонов на действие; действие дольше секунды — не больше пяти. Несохранённые сцены — ничего не делает.
        public static string Measure(int runCount)
        {
            if (HasUnsavedScenes())
                return "Live Builder measurement: save or revert the open scenes first, the measurement replaces them for a while.";

            StageUtility.GoToMainStage();
            var sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            var meshFilesBefore = MeshFilesInFallbackFolder();
            var report = new LiveBuilderRebuildReport(runCount);

            // Шейдеры — сразу, а не заглушкой: иначе первые кадры дешевле настоящих.
            ShaderUtil.allowAsyncCompilation = false;

            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                MeasureLevel(report, runCount);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = EditorSettings.asyncShaderCompilation;
                RestoreScenes(sceneSetup);
                DeleteNewMeshFiles(meshFilesBefore);
            }

            return report.ToText();
        }

        private static bool HasUnsavedScenes()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    return true;

            return false;
        }

        private static void MeasureLevel(LiveBuilderRebuildReport report, int runCount)
        {
            LiveBuilderRebuildProfiler.BeginRecording();
            var targets = LiveBuilderReferenceLevel.Build(Vector3.zero);
            report.FirstBuild = LiveBuilderRebuildProfiler.EndRecording();

            LiveBuilderRebuildProfiler.BeginRecording();
            LiveBuilderRebuildQueue.RunFinalPassNow();
            report.SecondPass = LiveBuilderRebuildProfiler.EndRecording();

            LiveBuilderRebuildProfiler.BeginRecording();
            BoxModelMeshSaving.SaveMeshFiles(targets.Root.gameObject);
            BoxModelMeshStorage.SavePendingNow();
            report.SceneSave = LiveBuilderRebuildProfiler.EndRecording();

            var column = targets.Column.transform;
            var window = targets.Window.transform;
            var building = targets.Building;

            using var sceneRender = new LiveBuilderSceneRender(targets.Root);
            // Жест — два шага в одну сторону, каждый на новое место: память солвера и заготовки не подсказывает ответ; после прогона — назад.
            report.Add(MeasureAction(new LiveBuilderMeasuredAction("Move one box in the 300-box object", "Box", BoxMoveBudget, true), runCount, sceneRender,
                () => Move(column, Vector3.right * GridStep), () => Move(column, Vector3.left * (2f * GridStep))));
            report.Add(MeasureAction(new LiveBuilderMeasuredAction("Move a window row (8 x 9 copies) on the building wall", "Window", WindowMoveBudget, true), runCount, sceneRender,
                () => Move(window, building.transform.right * GridStep), () => Move(window, building.transform.right * (-2f * GridStep))));
            report.Add(MeasureAction(new LiveBuilderMeasuredAction("Building storeys 9 -> 10", "Storeys", StoreysBudget, false), runCount, null,
                () => SetStoreyCount(building, 10), () => SetStoreyCount(building, 9)));
        }

        // Правка кодом и весть о ней очереди — так же, как редактор сообщает о правке ручкой.
        private static void Move(Transform target, Vector3 offset)
        {
            target.position += offset;
            LiveBuilderRebuildQueue.RequestCheck(target.gameObject);
        }

        private static void SetStoreyCount(SplineBuilding building, int storeyCount)
        {
            building.StoreyCount = storeyCount;
            LiveBuilderRebuildQueue.RequestCheck(building.gameObject);
        }


        // ── Действие ─────────────────────────────────────────────────────

        // Первый прогон — разгон; действие дольше секунды — он идёт в счёт, а прогонов меньше: разгон рядом с секундами не виден, а каждый прогон дорог.
        private static LiveBuilderMeasuredAction MeasureAction(LiveBuilderMeasuredAction action, int runCount, LiveBuilderSceneRender sceneRender,
            Action change, Action revert)
        {
            var warmUp = MeasureRun(action.IsGesture, change, revert, sceneRender);
            var isSlow = warmUp.ActionMilliseconds >= SlowActionMilliseconds;
            var wantedRuns = isSlow ? Math.Min(runCount, SlowActionRunCount) : runCount;

            if (isSlow)
            {
                action.Add(warmUp);
                action.IsShortened = wantedRuns < runCount;
            }

            while (action.Runs.Count < wantedRuns)
                action.Add(MeasureRun(action.IsGesture, change, revert, sceneRender));

            return action;
        }

        // Правка и проход очереди. Жест — два шага: первый (взяли ручку) меряется отдельно, второй — обычный кадр жеста; после отпускания — чистовой проход,
        // как его делает очередь через 0,3 с. Отрисовка — после шага; отложенная запись на диск — отдельно. Возврат правки в замер не идёт.
        private static LiveBuilderMeasuredRun MeasureRun(bool isGesture, Action change, Action revert, LiveBuilderSceneRender sceneRender)
        {
            LiveBuilderRebuildRecording firstStep = null;
            LiveBuilderRebuildRecording pass;
            double renderMilliseconds;

            using (isGesture ? LiveBuilderGestureTracker.BeginExplicitGesture() : default)
            {
                if (isGesture)
                {
                    LiveBuilderRebuildProfiler.BeginRecording();
                    change();
                    LiveBuilderRebuildQueue.RunPassNow(LiveBuilderBuildQuality.Draft);
                    firstStep = LiveBuilderRebuildProfiler.EndRecording();
                }

                LiveBuilderRebuildProfiler.BeginRecording();
                change();
                LiveBuilderRebuildQueue.RunPassNow(isGesture ? LiveBuilderBuildQuality.Draft : LiveBuilderBuildQuality.Final);
                pass = LiveBuilderRebuildProfiler.EndRecording();
                renderMilliseconds = sceneRender != null ? sceneRender.RenderMilliseconds() : 0.0;
            }

            LiveBuilderRebuildRecording finalPass = null;

            if (isGesture)
            {
                LiveBuilderRebuildProfiler.BeginRecording();
                LiveBuilderRebuildQueue.RunFinalPassNow();
                finalPass = LiveBuilderRebuildProfiler.EndRecording();
            }

            LiveBuilderRebuildProfiler.BeginRecording();
            BoxModelMeshStorage.SavePendingNow();
            var diskSave = LiveBuilderRebuildProfiler.EndRecording();

            if (revert != null)
            {
                revert();
                LiveBuilderRebuildQueue.RunFinalPassNow();
                BoxModelMeshStorage.SavePendingNow();
            }

            return new LiveBuilderMeasuredRun(pass, renderMilliseconds, finalPass, diskSave, firstStep);
        }


        // ── Уборка ───────────────────────────────────────────────────────

        // Безымянную сцену не вернуть — вместо неё пустая.
        private static void RestoreScenes(SceneSetup[] sceneSetup)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            if (sceneSetup.Length > 0 && Array.TrueForAll(sceneSetup, scene => !string.IsNullOrEmpty(scene.path)))
                EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        }


        // Сцена замера не сохранена — её меши в общей папке несохранённых сцен: все новые там файлы — замера.
        private static HashSet<string> MeshFilesInFallbackFolder()
        {
            var paths = new HashSet<string>();
            var folder = BoxModelMeshStorage.FallbackFolder;

            if (!AssetDatabase.IsValidFolder(folder))
                return paths;

            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { folder }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));

            return paths;
        }

        private static void DeleteNewMeshFiles(HashSet<string> filesBefore)
        {
            var newFiles = MeshFilesInFallbackFolder();
            newFiles.ExceptWith(filesBefore);

            foreach (var path in newFiles)
                if (BoxModelMeshStorage.IsBoxModelMeshPath(path))
                    AssetDatabase.DeleteAsset(path);
        }
    }
}
