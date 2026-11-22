using System;
using System.Collections.Generic;
using System.IO;
using FeatherKit.EditorTools;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LevelDesign;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Отпечатки «по виду» (<see cref="LiveBuilderLookDigest"/>) чужих сцен и префабов — для копий сцен автора на втором стенде, не для его проекта:
    /// сцена открывается и пересобирается нынешним кодом, сохранять её нельзя.
    /// - два файла на сцену: «.saved.look.txt» — меши как сохранены, «.look.txt» — после полной пересборки нынешним кодом (эталон «как было»)
    /// - запуск: -executeMethod LiveBuilderSceneLookWriter.WriteFromCommandLine -lookAssets "путь;путь" -lookOut "папка"
    /// </summary>
    public static class LiveBuilderSceneLookWriter
    {
        private const string AssetsArgument = "-lookAssets";
        private const string OutputArgument = "-lookOut";
        private const string SavedSuffix = ".saved.look.txt";
        private const string RebuiltSuffix = ".look.txt";
        private const string LogPrefix = "LIVEBUILDER look ";


        public static void WriteFromCommandLine()
        {
            var assetPaths = ArgumentAfter(AssetsArgument).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var outputFolder = ArgumentAfter(OutputArgument);
            Directory.CreateDirectory(outputFolder);

            foreach (var assetPath in assetPaths)
            {
                var name = Path.GetFileNameWithoutExtension(assetPath);
                var isPrefab = assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
                var (saved, rebuilt, rootCount) = isPrefab ? DescribePrefab(assetPath) : DescribeScene(assetPath);

                File.WriteAllLines(Path.Combine(outputFolder, name + SavedSuffix), WithHeader(saved));
                File.WriteAllLines(Path.Combine(outputFolder, name + RebuiltSuffix), WithHeader(rebuilt));

                var differences = LiveBuilderLookDigest.Compare(saved, rebuilt);
                Debug.Log(LogPrefix + assetPath + ": " + rootCount + " construction roots, " + rebuilt.Count + " lines; saved vs rebuilt — "
                          + (differences.Count == 0 ? "same" : differences.Count + " differences:\n" + string.Join("\n", differences)));
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static string ArgumentAfter(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, name);

            if (index < 0 || index + 1 >= arguments.Length)
                throw new ArgumentException("Missing command line argument " + name);

            return arguments[index + 1];
        }

        private static IEnumerable<string> WithHeader(List<string> lines)
        {
            yield return LiveBuilderLookDigest.Header;

            foreach (var line in lines)
                yield return line;
        }


        // ── Сцена ────────────────────────────────────────────────────────

        // Скрытые копии Array FeatherKit в файл сцены не пишутся — сначала их собрать, иначе «как было» неполное.
        private static (List<string> Saved, List<string> Rebuilt, int RootCount) DescribeScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var saved = LiveBuilderLookDigest.DescribeScene(scene);

            foreach (var array in StageUtility.GetCurrentStageHandle().FindComponentsOfType<ArrayModifier>())
                ArrayModifierBuilder.Rebuild(array);

            LiveBuilderRebuildQueue.RunFinalPassNow();
            var roots = LiveBuilderLookDigest.ConstructionRootsOf(scene);
            RebuildEverything(roots);
            return (saved, LiveBuilderLookDigest.DescribeScene(scene), roots.Count);
        }


        // ── Префаб ───────────────────────────────────────────────────────

        private static (List<string> Saved, List<string> Rebuilt, int RootCount) DescribePrefab(string prefabPath)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            BoxModelMeshStorage.BeginLoadedPrefab(contents.scene, prefabPath);

            try
            {
                var saved = LiveBuilderLookDigest.Describe(contents.transform);
                var roots = LiveBuilderLookDigest.ConstructionRootsUnder(contents.transform);
                RebuildEverything(roots);
                return (saved, LiveBuilderLookDigest.Describe(contents.transform), roots.Count);
            }
            finally
            {
                BoxModelMeshStorage.EndLoadedPrefab(contents.scene);
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }


        // ── Пересборка ───────────────────────────────────────────────────

        // Все постройки заново, в порядке очереди: объекты, здания, стены. Экземпляр префаба без своих правок берёт меш префаба, как в очереди.
        private static void RebuildEverything(List<Transform> roots)
        {
            BoxModelRebuilder.ForgetCachedResults();

            foreach (var root in roots)
            foreach (var model in root.GetComponentsInChildren<BoxModel>(true))
                if (!BoxModelRebuilder.TryUsePrefabGeometry(model))
                    BoxModelRebuilder.Rebuild(model);

            foreach (var root in roots)
            foreach (var building in root.GetComponentsInChildren<SplineBuilding>(true))
                if (!SplineBuildingRebuilder.TryUsePrefabGeometry(building))
                    SplineBuildingRebuilder.Rebuild(building);

            foreach (var root in roots)
            foreach (var wall in root.GetComponentsInChildren<SplineWall>(true))
                if (!SplineWallRebuilder.TryUsePrefabGeometry(wall))
                    SplineWallRebuilder.Rebuild(wall);
        }
    }
}
