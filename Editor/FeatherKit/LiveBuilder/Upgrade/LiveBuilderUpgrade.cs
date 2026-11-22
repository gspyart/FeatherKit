using System;
using System.Collections.Generic;
using System.IO;
using FeatherKit.LevelDesign;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Когда обновлять старые данные Live Builder: при открытии сцены и префаба проекта — сами, итог в консоль.
    /// - сцена: сначала префабы-ассеты, чьи экземпляры в ней старые (базовые раньше вариантов), потом объекты самой сцены
    /// - повторно ничего не делает: версии данных плюс признаки старых компонентов; ассеты пакетов не трогает
    /// - порядок шагов одного прохода — <see cref="LiveBuilderUpgradeRun"/>; на весь проект — <see cref="LiveBuilderUpgradeProjectCommand"/>
    /// </summary>
    [InitializeOnLoad]
    public static class LiveBuilderUpgrade
    {
        private const string ProjectAssetsFolder = "Assets/";

        // Сколько раз попросили не обновлять при открытии: команда на проект и проверки обновляют сами.
        private static int automaticSuppressions;


        static LiveBuilderUpgrade()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
        }


        // Обновили сцену или префаб при открытии — итог тем, кому он нужен (проверки).
        public static event Action<LiveBuilderUpgradeReport> Upgraded;


        // ── Открытие ─────────────────────────────────────────────────────

        // Пока жив ответ — открытие сцен и префабов их не обновляет.
        public static IDisposable SuppressAutomatic()
        {
            automaticSuppressions++;
            return new LiveBuilderUpgradeSuppression(() => automaticSuppressions--);
        }


        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (automaticSuppressions > 0 || !IsProjectAsset(scene.path) || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var report = UpgradeScene(scene);

            if (!report.HasChanges)
                return;

            report.Log();
            Upgraded?.Invoke(report);
        }

        private static void OnPrefabStageOpened(PrefabStage stage)
        {
            if (automaticSuppressions > 0 || !IsProjectAsset(stage.assetPath) || !NeedsUpgrade(new[] { stage.prefabContentsRoot }, true))
                return;

            var report = new LiveBuilderUpgradeReport(Path.GetFileName(stage.assetPath));
            LiveBuilderUpgradeRun.Run(new[] { stage.prefabContentsRoot }, true, report);
            EditorSceneManager.MarkSceneDirty(stage.scene);
            report.Log();
            Upgraded?.Invoke(report);
        }


        // ── Сцена ────────────────────────────────────────────────────────

        // Префабы её старых экземпляров — на диск сразу; сама сцена помечается изменённой: откатить — закрыть без сохранения.
        public static LiveBuilderUpgradeReport UpgradeScene(Scene scene)
        {
            var report = new LiveBuilderUpgradeReport(scene.name);
            var roots = scene.GetRootGameObjects();

            foreach (var prefabPath in OldPrefabsUsedIn(roots))
                UpgradePrefab(prefabPath, report);

            if (NeedsUpgrade(roots, false))
            {
                LiveBuilderUpgradeRun.Run(roots, false, report);
                EditorSceneManager.MarkSceneDirty(scene);
            }

            return report;
        }


        // Префаб-ассет: загрузить содержимое, обновить, пересобрать меши в его папку и записать. Был старым — да.
        public static bool UpgradePrefab(string prefabPath, LiveBuilderUpgradeReport report)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            BoxModelMeshStorage.BeginLoadedPrefab(contents.scene, prefabPath);

            try
            {
                if (!NeedsUpgrade(new[] { contents }, true))
                    return false;

                var prefabReport = new LiveBuilderUpgradeReport(Path.GetFileName(prefabPath));
                LiveBuilderUpgradeRun.Run(new[] { contents }, true, prefabReport);
                BoxModelMeshSaving.SaveMeshFiles(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                report.Include(prefabReport);
                report.AddUpgradedPrefab(prefabPath);
                return true;
            }
            finally
            {
                BoxModelMeshStorage.EndLoadedPrefab(contents.scene);
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }


        // Префабы-ассеты проекта, чьи экземпляры (и вложенные в них префабы) лежат в сцене и старые. Вложенные и базовые — раньше.
        public static List<string> OldPrefabsUsedIn(IReadOnlyList<GameObject> roots)
        {
            var used = new HashSet<string>();

            foreach (var root in roots)
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject))
                        used.Add(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(transform.gameObject)));

            var withDependencies = new HashSet<string>(used);

            foreach (var path in used)
                foreach (var dependency in AssetDatabase.GetDependencies(path, true))
                    if (dependency.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                        withDependencies.Add(dependency);

            var old = new List<string>();

            foreach (var path in withDependencies)
                if (IsProjectAsset(path) && NeedsUpgrade(new[] { AssetDatabase.LoadAssetAtPath<GameObject>(path) }, true))
                    old.Add(path);

            SortDependenciesFirst(old);
            return old;
        }

        // Префаб, от которого зависят другие из списка, — раньше их: вариант после базового, внешний — после вложенного.
        public static void SortDependenciesFirst(List<string> prefabPaths)
        {
            var dependencyCounts = new Dictionary<string, int>();

            foreach (var path in prefabPaths)
                dependencyCounts[path] = AssetDatabase.GetDependencies(path, true).Length;

            prefabPaths.Sort((first, second) => dependencyCounts[first].CompareTo(dependencyCounts[second]));
        }


        // ── Старые ли данные ─────────────────────────────────────────────

        // Есть признак старых данных: версия 0, старые повтор или привязка, Array на объекте Live Builder, копии вверх, вырезающая коробка,
        // «Своя группа», а в сцене — «Own object» без постройки над вырезом. Содержимое экземпляров префабов решает их ассет.
        public static bool NeedsUpgrade(IReadOnlyList<GameObject> roots, bool isPrefabAsset)
        {
            foreach (var root in roots)
            {
                if (root == null)
                    continue;

                foreach (var component in root.GetComponentsInChildren<Component>(true))
                    if (component != null && IsOldData(component, isPrefabAsset) && (isPrefabAsset || !IsFromPrefabAsset(component)))
                        return true;
            }

            return false;
        }

        private static bool IsOldData(Component component, bool isPrefabAsset)
        {
            return component switch
            {
                BoxModel model => LiveBuilderCutRules.IsLegacy(model),
                SplineBuilding building => !SplineBuildingStoreys.HasStoreyContainers(building),
                SplineBuildingStoreyLink => true,
                BoxModelRepeat => true,
                ArrayModifier array => array.GetComponent<ArrayPreviewCopy>() == null && LiveBuilderRepeatUpgrade.IsLiveBuilderObject(array.gameObject),
                SplineWall wall => wall.LegacyCopiesUp > 1,
                BoxModelPart part => part.Mode == BoxModelPartMode.Cut,
                GeometryCutter cutter => cutter.LegacyCopiesUp > 1 || cutter.Scope == GeometryCutterScope.LegacyOwnGroup
                                         || !isPrefabAsset && LiveBuilderCutBoxUpgrade.IsOwnObjectWithoutConstruction(cutter),
                _ => false
            };
        }

        // В экземпляре префаба компонент из ассета обновляет сам ассет; «Own object» без постройки — правка экземпляра, её можно здесь.
        private static bool IsFromPrefabAsset(Component component)
        {
            return PrefabUtility.IsPartOfPrefabInstance(component) && !(component is GeometryCutter cutter && LiveBuilderCutBoxUpgrade.IsOwnObjectWithoutConstruction(cutter));
        }

        // Сцены и префабы проекта; ассеты пакетов (в том числе образцы проверок FeatherKit) не наши — их не трогаем.
        public static bool IsProjectAsset(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith(ProjectAssetsFolder, StringComparison.Ordinal);
        }
    }


    /// <summary>Запрет обновлять при открытии, пока жив: снимается по Dispose.</summary>
    public sealed class LiveBuilderUpgradeSuppression : IDisposable
    {
        private Action release;


        public LiveBuilderUpgradeSuppression(Action release)
        {
            this.release = release;
        }


        // IDisposable
        public void Dispose()
        {
            release?.Invoke();
            release = null;
        }
    }
}
