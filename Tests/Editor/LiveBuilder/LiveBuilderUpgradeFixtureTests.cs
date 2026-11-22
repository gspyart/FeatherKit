using System.Collections.Generic;
using System.IO;
using FeatherKit.EditorTools;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LevelDesign;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Сцены-образцы «как было» (<see cref="LiveBuilderUpgradeFixtures"/>) открываются из пакета без пересборки и без миграции:
    /// меши лежат рядом со сценой, и вид совпадает с отпечатком — по этим мешам миграция снимает «как было» (<see cref="LiveBuilderUpgradeSnapshot"/>).
    /// Подписи объектов из коробок при открытии те же, что у собранных мешей, даже со скрытыми копиями Array FeatherKit.
    /// Сцена открывается рядом с открытыми и закрывается без сохранения.
    /// </summary>
    public class LiveBuilderUpgradeFixtureTests
    {
        private static IEnumerable<string> FixtureNames => LiveBuilderUpgradeFixtures.Names;


        [TestCaseSource(nameof(FixtureNames))]
        public void UpgradeFixtures_AreReadable(string name)
        {
            var lookPath = LiveBuilderUpgradeFixtures.LookFilePath(name);
            Assert.IsTrue(File.Exists(lookPath), "нет отпечатка образца");

            var scene = OpenFixture(name, out var replacedUntitledScene);

            try
            {
                var meshFolder = Path.GetDirectoryName(LiveBuilderUpgradeFixtures.ScenePath(name))?.Replace('\\', '/') + "/";
                var meshCount = 0;

                foreach (var root in LiveBuilderLookDigest.ConstructionRootsOf(scene))
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!BoxModelMeshStorage.IsBoxModelMeshFile(filter.sharedMesh))
                        continue;

                    meshCount++;
                    StringAssert.StartsWith(meshFolder, UnityEditor.AssetDatabase.GetAssetPath(filter.sharedMesh), filter.name + ": меш — из папки образцов");
                }

                Assert.Greater(meshCount, 0, "меши построек на месте");

                var differences = LiveBuilderLookDigest.Compare(File.ReadAllLines(lookPath), LiveBuilderLookDigest.DescribeScene(scene));
                Assert.IsEmpty(differences, string.Join("\n", differences));
            }
            finally
            {
                CloseFixture(scene, replacedUntitledScene);
            }
        }


        // Скрытые копии Array FeatherKit при открытии ложатся в конец родителя, и порядок массивов каждый раз свой — подпись от этого не зависит.
        [TestCaseSource(nameof(FixtureNames))]
        public void UpgradeFixtures_OpenWithoutRebuild(string name)
        {
            var scene = OpenFixture(name, out var replacedUntitledScene);

            try
            {
                foreach (var array in Object.FindObjectsByType<ArrayModifier>(FindObjectsInactive.Include))
                    if (!ArrayModifierBuilder.IsUpToDate(array))
                        ArrayModifierBuilder.Rebuild(array);

                BoxModelBuildResults.ForgetAll();

                foreach (var root in scene.GetRootGameObjects())
                foreach (var model in root.GetComponentsInChildren<BoxModel>(true))
                    if (!string.IsNullOrEmpty(model.BuiltSignature))
                        Assert.AreEqual(GeometryCutterLookup.WithoutCutterKey(model.BuiltSignature), BoxModelBuildResults.Of(model).Signature,
                            model.name + ": подпись при открытии — та же, по которой собран меш");
            }
            finally
            {
                CloseFixture(scene, replacedUntitledScene);
            }
        }


        // ── Сцена ────────────────────────────────────────────────────────

        // Рядом с несохранённой безымянной сценой Unity другую не откроет — тогда вместо неё.
        internal static Scene OpenFixture(string name, out bool replacedUntitledScene)
        {
            replacedUntitledScene = false;

            for (var index = 0; index < SceneManager.sceneCount; index++)
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(index).path))
                    replacedUntitledScene = true;

            return EditorSceneManager.OpenScene(LiveBuilderUpgradeFixtures.ScenePath(name), replacedUntitledScene ? OpenSceneMode.Single : OpenSceneMode.Additive);
        }

        internal static void CloseFixture(Scene scene, bool replacedUntitledScene)
        {
            if (replacedUntitledScene)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else
                EditorSceneManager.CloseScene(scene, true);
        }
    }
}
