using System.Linq;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Инструмент «Module» (<see cref="LiveBuilderModuleTool"/>): клик по стене здания ставит префаб по нормали на пол этажа плюс его высоту,
    /// край по сетке, вырез префаба режет стену; Shift + клик — ряд вдоль стороны, каждый проём прорезан; мимо стены — ничего.
    /// </summary>
    public class LiveBuilderModuleToolTests
    {
        private const string TempFolder = "Assets/LiveBuilderModuleToolTemp";
        private const float GridStep = 0.5f;

        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private GameObject windowPrefab;
        private GameObject prefabBefore;
        private float rowStepBefore;
        private float rowMarginBefore;
        private float gridStepBefore;
        private bool isOnAllStoreysBefore;
        private Object[] selectionBefore;
        private SplineBuilding building;


        [SetUp]
        public void OpenScene()
        {
            testScene = new LiveBuilderTestScene();
            windowPrefab = LiveBuilderModuleTestPrefabs.CreateWindow(TempFolder);
            prefabBefore = LiveBuilderModuleSettings.Prefab;
            rowStepBefore = LiveBuilderModuleSettings.RowStep;
            rowMarginBefore = LiveBuilderModuleSettings.RowMargin;
            gridStepBefore = LiveBuilderGrid.CurrentStep;
            isOnAllStoreysBefore = LiveBuilderBuildingContext.IsOnAllStoreys;
            selectionBefore = Selection.objects;

            LiveBuilderModuleSettings.Prefab = windowPrefab;
            LiveBuilderModuleSettings.RowStep = 3f;
            LiveBuilderModuleSettings.RowMargin = 1f;
            LiveBuilderGrid.SetStep(GridStep);
            LiveBuilderBuildingContext.IsOnAllStoreys = false;
            building = testScene.CreateBuilding(3, Vector3.zero, new Vector3(12f, 0f, 0f), new Vector3(12f, 0f, 8f), new Vector3(0f, 0f, 8f));
        }


        [TearDown]
        public void CloseScene()
        {
            foreach (var tool in LiveBuilderToolPalette.Tools)
                tool.Cancel();

            testScene.Dispose();
            LiveBuilderModuleSettings.Prefab = prefabBefore;
            LiveBuilderModuleSettings.RowStep = rowStepBefore;
            LiveBuilderModuleSettings.RowMargin = rowMarginBefore;
            LiveBuilderGrid.SetStep(gridStepBefore);
            LiveBuilderBuildingContext.IsOnAllStoreys = isOnAllStoreysBefore;
            Selection.objects = selectionBefore;

            if (AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.DeleteAsset(TempFolder);
        }


        // ── Клик ─────────────────────────────────────────────────────────

        [Test]
        public void Click_OnBuildingWall_PlacesModuleOnStoreyFloorAndCutsWall()
        {
            var secondFloor = building.FirstFloorWorldHeight + building.StoreyHeight;
            Assert.IsTrue(Driver().Click(RayAtFacade(5.3f, secondFloor + 1.7f)), "клик по стене забран");

            var module = Selection.activeGameObject;
            Assert.IsNotNull(module);
            Assert.AreEqual(windowPrefab, PrefabUtility.GetCorrespondingObjectFromSource(module), "экземпляр префаба из палитры");
            Assert.AreEqual(SplineBuildingStoreys.ContainerOf(building, 1).transform, module.transform.parent, "в этаже под мышью");
            Assert.AreEqual(secondFloor + LiveBuilderModuleTestPrefabs.WindowBottomAboveFloor, module.transform.position.y, 0.001f, "низ — пол этажа плюс высота модуля");
            Assert.Greater(Vector3.Dot(module.transform.forward, Vector3.back), 0.999f, "+Z — наружу, к мыши");

            // Рама 1,4 м: левый край вдоль стены — на сетке от угла здания.
            var leftEdge = module.transform.position.x - Origin.x - 0.7f;
            Assert.AreEqual(0f, Mathf.Abs(Mathf.Repeat(leftEdge + GridStep / 2f, GridStep) - GridStep / 2f), 0.001f, "край по сетке, а не " + leftEdge);

            LiveBuilderRebuildQueue.RunFinalPassNow();
            Assert.IsTrue(LiveBuilderModuleTestPrefabs.IsOpenThrough(module), "вырез префаба режет стену");
            Assert.IsFalse(LiveBuilderModuleTestPrefabs.IsOpenThrough(module, Vector3.right * 2f), "рядом стена цела");
        }


        [Test]
        public void ShiftClick_PlacesRowAlongSideWithEveryOpeningCut()
        {
            Assert.IsTrue(Driver().Click(RayAtFacade(5.3f, building.FirstFloorWorldHeight + 1.7f), true));

            var row = Selection.activeGameObject;
            var array = row.GetComponent<LiveBuilderArray>();
            Assert.IsNotNull(array, "ряд — повтор");
            Assert.AreEqual(LiveBuilderArrayAlong.Wall, array.Along);
            Assert.AreEqual(LiveBuilderArrayMode.Fill, array.Mode);
            Assert.AreEqual(1, array.Variants.Count, "модуль — вариант A");
            Assert.AreEqual(row.transform, array.Variants[0].parent);

            LiveBuilderRebuildQueue.RunFinalPassNow();
            var module = array.Variants[0].gameObject;
            var container = row.GetComponentsInChildren<LiveBuilderGeneratedCopies>(true).Single();
            var modules = container.Copies.Prepend(module).ToList();

            Assert.IsTrue(module.activeSelf, "оригинал — первое место ряда");
            Assert.Greater(modules.Count, 2, "ряд во всю сторону 12 м");

            foreach (var placed in modules)
            {
                var along = placed.transform.position.x - Origin.x;
                Assert.IsTrue(along > LiveBuilderModuleSettings.RowMargin && along < 12f - LiveBuilderModuleSettings.RowMargin, "между углами: " + along);
                Assert.IsTrue(LiveBuilderModuleTestPrefabs.IsOpenThrough(placed), "проём прорезан у " + placed.name);
            }
        }


        [Test]
        public void Click_BesideWalls_PlacesNothing()
        {
            var ray = new Ray(Origin + new Vector3(40f, 5f, -10f), Vector3.forward);
            Assert.IsFalse(Driver().Click(ray));
            Assert.IsEmpty(building.GetComponentsInChildren<LiveBuilderModule>(true), "модулей нет");
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static LiveBuilderToolDriver Driver()
        {
            return new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderModuleTool>());
        }

        // Луч снаружи на фасад z = 0 в точке along от угла здания на высоте мира.
        private static Ray RayAtFacade(float along, float worldHeight)
        {
            return new Ray(new Vector3(Origin.x + along, worldHeight, Origin.z - 5f), Vector3.forward);
        }
    }
}
