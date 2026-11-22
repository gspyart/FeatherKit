using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Повтор этапа 4 (<see cref="LiveBuilderArray"/>): Fill, узор из вариантов, вдоль стены и настоящие копии префабов (<see cref="LiveBuilderArraySync"/>).
    /// Главная проверка — пример ТЗ: фасад 24 м, окна и балконные двери узором ABA на этажах 2–9, все проёмы прорезаны.
    /// </summary>
    public class LiveBuilderArrayPatternTests
    {
        private const string TempFolder = "Assets/LiveBuilderArrayPatternTemp";

        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private GameObject windowPrefab;
        private GameObject balconyPrefab;


        [SetUp]
        public void OpenScene()
        {
            testScene = new LiveBuilderTestScene();
            windowPrefab = LiveBuilderModuleTestPrefabs.CreateWindow(TempFolder);
            balconyPrefab = LiveBuilderModuleTestPrefabs.CreateBalconyDoor(TempFolder);
        }


        [TearDown]
        public void CloseScene()
        {
            testScene.Dispose();

            if (AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.DeleteAsset(TempFolder);
        }


        // ── Расчёт ───────────────────────────────────────────────────────

        // 20 м, отступы по 1 м, копия 1,2 м, шаг не меньше 2,5 м: 7 копий, шаг растянут до 2,8 м — крайние ровно по отступам.
        [Test]
        public void Fill_CountsCopiesAndStretchesStep()
        {
            Assert.AreEqual(7, LiveBuilderArrayLayout.FillCountOf(20f, 1f, 1.2f, 2.5f));
            Assert.AreEqual(2.8f, LiveBuilderArrayLayout.FillStepOf(20f, 1f, 1.2f, 2.5f), 0.0001f);
            Assert.AreEqual(1, LiveBuilderArrayLayout.FillCountOf(1f, 1f, 1.2f, 2.5f), "не влезает — одна");
        }


        [Test]
        public void Pattern_RepeatsLettersAndSkipsDoNotShiftIt()
        {
            var array = new GameObject("Row").AddComponent<LiveBuilderArray>();
            var first = new GameObject("A").transform;
            var second = new GameObject("B").transform;
            first.SetParent(array.transform);
            second.SetParent(array.transform);
            array.Variants.Add(first);
            array.Variants.Add(second);

            array.Pattern = "ABA";
            CollectionAssert.AreEqual(new[] { 0, 1, 0, 0, 1, 0 }, Enumerable.Range(1, 6).Select(x => LiveBuilderArrayPattern.VariantOf(array, x)).ToArray());

            array.Pattern = "";
            Assert.AreEqual(0, LiveBuilderArrayPattern.VariantOf(array, 2), "пусто — всегда первый");

            array.Pattern = "ABC";
            Assert.IsNotNull(LiveBuilderArrayPattern.ProblemOf(array), "буква без варианта");

            array.Pattern = "AB";
            array.CountX = 4;
            array.SkipX = "2";
            array.StepYIsStoreyHeight = false;
            array.CountY = 1;
            var poses = LiveBuilderArrayLayout.PosesOf(array, new LiveBuilderArrayContext(0f, 0));
            CollectionAssert.AreEqual(new[] { 0, 0, 1 }, poses.Select(pose => pose.Variant).ToArray(), "копии 1, 3, 4: A, A, B");
        }


        // ── Пример ТЗ ────────────────────────────────────────────────────

        // Фасад 24 м в здании на 9 этажей: окно A и балконная дверь B узором ABA, Fill во всю сторону, Count Y 9, Skip Y 1.
        [Test]
        public void FacadeExample_WindowsAndBalconiesOnStoreys2To9()
        {
            var building = testScene.CreateBuilding(9, Vector3.zero, new Vector3(24f, 0f, 0f), new Vector3(24f, 0f, 10f), new Vector3(0f, 0f, 10f));
            var clock = Stopwatch.StartNew();

            var row = new GameObject("Facade Row").transform;
            row.SetParent(SplineBuildingStoreys.ContainerOf(building, 0).transform, false);
            row.SetPositionAndRotation(new Vector3(Origin.x + 12f, building.FirstFloorWorldHeight, Origin.z), Quaternion.LookRotation(Vector3.back));

            var window = InstantiateIn(windowPrefab, row, LiveBuilderModuleTestPrefabs.WindowBottomAboveFloor);
            var balcony = InstantiateIn(balconyPrefab, row, 0f);
            var array = row.gameObject.AddComponent<LiveBuilderArray>();
            array.Along = LiveBuilderArrayAlong.Wall;
            array.Mode = LiveBuilderArrayMode.Fill;
            array.Margin = 1f;
            array.StepX = 3f;
            array.CountY = 9;
            array.SkipY = "1";
            array.Variants.Add(window.transform);
            array.Variants.Add(balcony.transform);
            array.Pattern = "ABA";

            LiveBuilderRebuildQueue.RunFinalPassNow();
            clock.Stop();
            Debug.Log("[Facade] built in " + clock.ElapsedMilliseconds + " ms");
            Assert.Less(clock.ElapsedMilliseconds, 60000, "собирается за минуту");

            var perStorey = LiveBuilderArrayLayout.CountsOf(array, LiveBuilderArrayCopyPoses.FilledContextOf(array)).x;
            Assert.AreEqual(7, perStorey, "24 м − отступы 2 м − балкон 2 м, шаг от 3 м: 7 мест");
            Assert.IsEmpty(LiveBuilderArrayCopyPoses.WarningsOf(array));

            var windows = CopiesOf(window);
            var balconies = CopiesOf(balcony);
            Assert.AreEqual(5 * 8, windows.Count, "ABAABAA: 5 окон на этаж, этажи 2–9");
            Assert.AreEqual(2 * 8, balconies.Count, "2 балконные двери на этаж");
            Assert.IsFalse(window.activeSelf || balcony.activeSelf, "оригиналы на первом этаже выключены");

            CollectionAssert.AreEquivalent(Enumerable.Range(1, 8), balconies.Select(copy => StoreyOf(building, copy)).Distinct(), "балконы на этажах 2–9");
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 8), windows.Select(copy => StoreyOf(building, copy)).Distinct(), "окна на этажах 2–9");

            foreach (var copy in windows.Concat(balconies))
            {
                Assert.IsTrue(copy.activeInHierarchy && (copy.hideFlags & HideFlags.NotEditable) != 0, "копия включена и не правится руками");
                Assert.IsTrue(LiveBuilderModuleTestPrefabs.IsOpenThrough(copy), "проём прорезан: " + copy.name + " на этаже " + (StoreyOf(building, copy) + 1));
            }

            foreach (var copy in windows.Where(copy => StoreyOf(building, copy) == 1))
                Assert.IsFalse(LiveBuilderModuleTestPrefabs.IsOpenThrough(copy, Vector3.down * building.StoreyHeight), "первый этаж цел под " + copy.name);

            foreach (var copy in balconies)
                Assert.IsTrue(Physics.Raycast(copy.transform.TransformPoint(new Vector3(0f, 1f, 0.9f)), Vector3.down, 1.5f), "плита балкона у " + copy.name);

            var alongFacade = windows.Concat(balconies).Select(copy => copy.transform.position.x - Origin.x).ToList();
            Assert.AreEqual(2f, alongFacade.Min(), 0.01f, "первое место: отступ 1 м и полбалкона от угла");
            Assert.AreEqual(22f, alongFacade.Max(), 0.01f, "последнее — так же от другого угла");
        }

        private static GameObject InstantiateIn(GameObject prefab, Transform row, float height)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, row);
            instance.transform.SetLocalPositionAndRotation(new Vector3(0f, height, 0f), Quaternion.identity);
            return instance;
        }

        // Этаж копии с нуля — по высоте её опоры.
        private static int StoreyOf(SplineBuilding building, GameObject copy)
        {
            return building.StoreyAtHeight(copy.transform.position.y + 0.01f);
        }


        // ── Настоящие копии ──────────────────────────────────────────────

        // Префаб × 3 по осям: копии — экземпляры в спрятанном контейнере; пропуск оригинала его выключает, снятый пропуск включает; без повтора копий нет.
        [Test]
        public void RealCopies_FollowCountsAndShowOriginalBack()
        {
            var original = (GameObject)PrefabUtility.InstantiatePrefab(windowPrefab);
            original.transform.position = Origin;
            var array = original.AddComponent<LiveBuilderArray>();
            array.CountX = 3;
            array.CountY = 1;
            array.StepX = 2f;

            LiveBuilderRebuildQueue.RunFinalPassNow();
            var copies = CopiesOf(original);
            Assert.AreEqual(2, copies.Count);
            Assert.IsTrue(copies.All(copy => PrefabUtility.GetCorrespondingObjectFromSource(copy) == windowPrefab), "копии — экземпляры префаба");
            Assert.IsTrue(copies.All(copy => copy.GetComponent<LiveBuilderArray>() == null), "без своего повтора");
            Assert.AreEqual(Origin.x + 4f, copies.Max(copy => copy.transform.position.x), 0.001f);
            Assert.IsTrue((ContainerOf(original).gameObject.hideFlags & HideFlags.HideInHierarchy) != 0, "в иерархии не видны");

            array.SkipX = "1";
            LiveBuilderRebuildQueue.RunFinalPassNow();
            Assert.IsFalse(original.activeSelf, "пропущенный оригинал выключен");

            array.SkipX = "";
            LiveBuilderRebuildQueue.RunFinalPassNow();
            Assert.IsTrue(original.activeSelf, "пропуск сняли — включён");

            Object.DestroyImmediate(array);
            LiveBuilderRebuildQueue.RunFinalPassNow();
            Assert.IsNull(ContainerOf(original), "без повтора контейнера нет");
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static LiveBuilderGeneratedCopies ContainerOf(GameObject original)
        {
            var siblings = original.transform.parent != null
                ? original.transform.parent.Cast<Transform>().Select(child => child.gameObject)
                : original.scene.GetRootGameObjects();

            return siblings.Select(sibling => sibling.GetComponent<LiveBuilderGeneratedCopies>())
                .FirstOrDefault(container => container != null && container.Original == original);
        }

        private static List<GameObject> CopiesOf(GameObject original)
        {
            var container = ContainerOf(original);
            return container != null ? container.Copies.Where(copy => copy != null).ToList() : new List<GameObject>();
        }
    }
}
