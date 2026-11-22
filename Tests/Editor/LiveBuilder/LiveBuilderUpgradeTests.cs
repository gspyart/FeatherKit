using System.Collections.Generic;
using System.IO;
using FeatherKit.EditorTools.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Обновление старых данных (<see cref="LiveBuilderUpgrade"/>) на копиях сцен-образцов «как было» (<see cref="LiveBuilderFixtureCopies"/>):
    /// - при открытии сцена обновляется сама, выглядит как раньше (<see cref="LiveBuilderLookDigest.DescribeSceneAsSeen"/>), итог — с ожидаемыми строками
    /// - второе открытие ничего не меняет и сцену изменённой не помечает; префаб обновлён один раз, экземпляр — без лишних правок
    /// </summary>
    public class LiveBuilderUpgradeTests
    {
        private static IEnumerable<string> FixtureNames => LiveBuilderUpgradeFixtures.Names;

        // Строки итога, которые обязаны быть у образца: кусок текста строки.
        private static readonly Dictionary<string, string[]> ExpectedReportLines = new()
        {
            ["AddAfterCut"] = new[] { "'Hall Block/Kept From Cuts/Column' — moved into 'Kept From Cuts'", "'Hall Block/Column Slot' — now 'Everything it touches'" },
            ["RoomKinds"] = new[] { "'Single Storey Building Object' — new object around the building", "'Hollow House/Room' — room without walls: floor and ceiling switched off" },
            ["StoreyLinks"] = new[] { "repeat Y to the building top, skipping the last", "its storey is gone", "'Linked Tower/Building/Storey 1/Shaft Opening' — moved out" },
            ["Repeats"] = new[] { "repeat group became a Live Builder repeat: X × 5", "repeat along two axes at once unrolled" },
            ["CopiesUp"] = new[] { "'Wall Copies Up' — copies up became a Live Builder repeat: Y × 3" },
            ["ArrayModifierOnParts"] = new[] { "'Tower Facade/Opening' — Array became a Live Builder repeat: Y × 6" },
            ["ArrayModifierOnCutters"] = new[] { "Array became a Live Builder repeat: X × 4", "repeat Y to the building top" },
            ["CutBoxes"] = new[] { "Wall In Void' — moved into 'Kept From Cuts'", "deepened to the wall thickness", "its material showed on no face and is dropped" },
            ["LegacyPrefab"] = new[] { "1 prefabs upgraded (LegacyPrefabBuilding.prefab)" }
        };

        // Расхождения отпечатка, которые не видны глазом, — с причиной. Строка расхождения начинается так.
        private static readonly Dictionary<string, string[]> InvisibleDifferences = new()
        {
            // Дверь в перегородке вычитанием оставляла на полу проёма лишнюю грань вверх, вровень с перекрытием и того же материала;
            // точный проём по линии её не строит.
            ["StoreyLinks"] = new[] { "changed: * [LiveBuilder Gray] plane 0.0000 1.0000 0.0000 / 0.2000" }
        };

        private readonly List<LiveBuilderUpgradeReport> reports = new();


        [OneTimeSetUp]
        public void CopyFixtures()
        {
            LiveBuilderFixtureCopies.CopyIntoProject();
            LiveBuilderUpgrade.Upgraded += reports.Add;
        }

        [OneTimeTearDown]
        public void DeleteCopies()
        {
            LiveBuilderUpgrade.Upgraded -= reports.Add;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LiveBuilderFixtureCopies.Delete();
        }


        // ── Вид и итог ───────────────────────────────────────────────────

        [TestCaseSource(nameof(FixtureNames))]
        public void Upgrade_LooksAsBefore_ReportsWhatChanged_AndRunsOnce(string name)
        {

            reports.Clear();
            var scene = OpenSingle(LiveBuilderFixtureCopies.ScenePath(name));

            Assert.AreEqual(1, reports.Count, "сцена обновилась при открытии один раз");
            Assert.IsFalse(LiveBuilderUpgrade.NeedsUpgrade(scene.GetRootGameObjects(), false), "старых данных не осталось");
            var reportText = reports[0].ToText();
            Debug.Log("LIVEBUILDER upgrade report " + name + "\n" + reportText);

            foreach (var expected in ExpectedReportLines[name])
                StringAssert.Contains(expected, reportText, "строка итога");

            LiveBuilderRebuildQueue.RunFinalPassNow();
            var baseline = LiveBuilderLookDigest.SectionOf(File.ReadAllLines(LiveBuilderUpgradeFixtures.LookFilePath(name)), LiveBuilderLookDigest.WholeSection);
            var actual = LiveBuilderLookDigest.SectionOf(LiveBuilderLookDigest.DescribeSceneAsSeen(scene), LiveBuilderLookDigest.WholeSection);
            var differences = LiveBuilderLookDigest.Compare(baseline, actual);

            if (InvisibleDifferences.TryGetValue(name, out var invisible))
                differences.RemoveAll(difference => System.Array.Exists(invisible, known => difference.StartsWith(known, System.StringComparison.Ordinal)));

            AssertTextureAlignmentKept(scene);
            Assert.IsEmpty(differences, "вид после обновления — как до него:\n" + string.Join("\n", differences));

            EditorSceneManager.SaveScene(scene);
            reports.Clear();
            scene = OpenSingle(LiveBuilderFixtureCopies.ScenePath(name));

            Assert.IsEmpty(reports, "второе открытие ничего не обновляет");
            Assert.IsFalse(scene.isDirty, "и сцену изменённой не помечает");
        }


        // Старые данные читаются с развёрткой «Object» — и созданные обновлением объекты тоже: иначе поменялись бы UV, а отпечаток вида их не видит.
        private static void AssertTextureAlignmentKept(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var model in root.GetComponentsInChildren<FeatherKit.LiveBuilder.BoxModel>(true))
                    Assert.AreEqual(FeatherKit.LiveBuilder.LiveBuilderTextureAlignment.Object, model.TextureAlignment, model.name + ": развёртка как раньше");

                foreach (var wall in root.GetComponentsInChildren<FeatherKit.LiveBuilder.SplineWall>(true))
                    Assert.AreEqual(FeatherKit.LiveBuilder.LiveBuilderTextureAlignment.Object, wall.TextureAlignment, wall.name + ": развёртка как раньше");

                foreach (var building in root.GetComponentsInChildren<FeatherKit.LiveBuilder.SplineBuilding>(true))
                    Assert.AreEqual(FeatherKit.LiveBuilder.LiveBuilderTextureAlignment.Object, building.TextureAlignment, building.name + ": развёртка как раньше");
            }
        }


        // ── Префаб ───────────────────────────────────────────────────────

        // Свою копию образцов делает заново и после себя возвращает: обновляет и сохраняет префаб и сцену.
        [Test]
        public void Upgrade_LegacyPrefab_UpgradedOnce_InstanceWithoutExtraOverrides()
        {
            try
            {
                CheckLegacyPrefabUpgradedOnce();
            }
            finally
            {
                OpenSingleEmpty();
                LiveBuilderFixtureCopies.CopyIntoProject();
            }
        }

        private void CheckLegacyPrefabUpgradedOnce()
        {
            OpenSingleEmpty();
            LiveBuilderFixtureCopies.CopyIntoProject();
            var prefabPath = LiveBuilderFixtureCopies.Folder + "/LegacyPrefabBuilding.prefab";
            var scenePath = LiveBuilderFixtureCopies.ScenePath("LegacyPrefab");
            int overridesBefore;

            using (LiveBuilderUpgrade.SuppressAutomatic())
                overridesBefore = OverrideCountOf(OpenSingle(scenePath));

            reports.Clear();
            var scene = OpenSingle(scenePath);

            Assert.AreEqual(1, reports.Count);
            CollectionAssert.AreEqual(new[] { prefabPath }, reports[0].UpgradedPrefabs, "префаб обновлён");
            Assert.LessOrEqual(OverrideCountOf(scene), overridesBefore, "у экземпляра нет новых правок");

            EditorSceneManager.SaveScene(scene);
            var prefabText = File.ReadAllText(prefabPath);
            reports.Clear();
            OpenSingle(scenePath);

            Assert.IsEmpty(reports, "второе открытие префаб не обновляет");
            Assert.AreEqual(prefabText, File.ReadAllText(prefabPath), "файл префаба тот же");
        }

        private static int OverrideCountOf(Scene scene)
        {
            var count = 0;

            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(transform.gameObject))
                        count += PrefabUtility.GetObjectOverrides(transform.gameObject).Count + PrefabUtility.GetAddedComponents(transform.gameObject).Count
                                 + PrefabUtility.GetAddedGameObjects(transform.gameObject).Count + PrefabUtility.GetRemovedComponents(transform.gameObject).Count;

            return count;
        }


        // ── Сцена ────────────────────────────────────────────────────────

        private static Scene OpenSingle(string path)
        {
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        // Копию образцов можно заменить, только когда ни одна её сцена не открыта.
        private static void OpenSingleEmpty()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
