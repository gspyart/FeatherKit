using System.Linq;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Инструмент «Cut» на фасаде здания 12 × 8 в три этажа: клик — проём готового размера, Shift + клик — ряд вдоль стороны,
    /// и To top — у «Cut», «Коробки», «Стены» и «Лестницы» новое повторяется по этажам до верха здания.
    /// </summary>
    public class LiveBuilderCutToolTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private SplineBuilding building;
        private Object[] selectionBefore;
        private bool isOnAllStoreysBefore;
        private int presetBefore;
        private float customWidthBefore;
        private float customHeightBefore;
        private float customSillBefore;
        private LiveBuilderCutterShape shapeBefore;
        private bool isSnappedToGridBefore;
        private LiveBuilderBoxMode boxModeBefore;


        [SetUp]
        public void OpenScene()
        {
            testScene = new LiveBuilderTestScene();
            selectionBefore = Selection.objects;
            isOnAllStoreysBefore = LiveBuilderBuildingContext.IsOnAllStoreys;
            presetBefore = LiveBuilderOpeningPresets.SelectedIndex;
            customWidthBefore = LiveBuilderOpeningPresets.CustomWidth;
            customHeightBefore = LiveBuilderOpeningPresets.CustomHeight;
            customSillBefore = LiveBuilderOpeningPresets.CustomSill;
            shapeBefore = LiveBuilderCutterSettings.Shape;
            isSnappedToGridBefore = LiveBuilderCutterSettings.IsSnappedToGrid;
            boxModeBefore = LiveBuilderBoxModeSettings.Mode;

            Selection.objects = new Object[0];
            LiveBuilderBuildingContext.IsOnAllStoreys = false;
            LiveBuilderOpeningPresets.SelectedIndex = LiveBuilderOpeningPresets.CustomIndex;
            LiveBuilderOpeningPresets.CustomWidth = 1.2f;
            LiveBuilderOpeningPresets.CustomHeight = 1.5f;
            LiveBuilderOpeningPresets.CustomSill = 0.9f;
            LiveBuilderCutterSettings.Shape = LiveBuilderCutterShape.Rectangle;
            LiveBuilderCutterSettings.IsSnappedToGrid = false;
            LiveBuilderBoxModeSettings.Mode = LiveBuilderBoxMode.Solid;
            building = testScene.CreateBuilding(3, Vector3.zero, new Vector3(12f, 0f, 0f), new Vector3(12f, 0f, 8f), new Vector3(0f, 0f, 8f));
        }


        [TearDown]
        public void CloseScene()
        {
            foreach (var tool in LiveBuilderToolPalette.Tools)
                tool.Cancel();

            testScene.Dispose();
            Selection.objects = selectionBefore;
            LiveBuilderBuildingContext.IsOnAllStoreys = isOnAllStoreysBefore;
            LiveBuilderOpeningPresets.SelectedIndex = presetBefore;
            LiveBuilderOpeningPresets.CustomWidth = customWidthBefore;
            LiveBuilderOpeningPresets.CustomHeight = customHeightBefore;
            LiveBuilderOpeningPresets.CustomSill = customSillBefore;
            LiveBuilderCutterSettings.Shape = shapeBefore;
            LiveBuilderCutterSettings.IsSnappedToGrid = isSnappedToGridBefore;
            LiveBuilderBoxModeSettings.Mode = boxModeBefore;
        }


        // ── Клик ─────────────────────────────────────────────────────────

        [Test]
        public void CutTool_ClickOnWall_PlacesPresetOpening()
        {
            var firstFloor = building.FirstFloorWorldHeight;
            Driver().Click(RayAtFacade(5.3f, firstFloor + 1.7f));

            var cutter = Selection.activeGameObject.GetComponent<GeometryCutter>();
            Assert.IsNotNull(cutter, "клик по стене поставил вырез и выделил его");
            Assert.AreEqual(1.2f, cutter.Width, 0.001f, "ширина — готовый размер");
            Assert.AreEqual(1.5f, cutter.Height, 0.001f, "высота — тоже");
            Assert.AreEqual(SplineBuildingStoreys.ContainerOf(building, 0).transform, cutter.transform.parent, "в этаже здания");
            Assert.IsNull(cutter.GetComponent<LiveBuilderArray>(), "без To top и Shift — один");

            LiveBuilderRebuildQueue.RunFinalPassNow();
            var center = cutter.transform.position + Vector3.up * 0.75f;
            Assert.IsTrue(LiveBuilderTestScene.IsOpen(new Vector3(center.x, center.y, Origin.z - 1f), Vector3.forward, 2f), "стена прорезана");
        }


        // Shift + клик — ряд: повтор на самом вырезе вдоль стены, Fill во всю сторону 12 м.
        [Test]
        public void CutTool_ShiftClick_AddsWallArray()
        {
            var firstFloor = building.FirstFloorWorldHeight;
            Driver().Click(RayAtFacade(5.3f, firstFloor + 1.7f), true);

            var array = Selection.activeGameObject.GetComponent<LiveBuilderArray>();
            Assert.IsNotNull(array, "ряд — повтор на вырезе");
            Assert.AreEqual(LiveBuilderArrayAlong.Wall, array.Along);
            Assert.AreEqual(LiveBuilderArrayMode.Fill, array.Mode);
            Assert.AreEqual(1, array.CountY, "без To top — один этаж");

            LiveBuilderRebuildQueue.RunFinalPassNow();
            var perSide = LiveBuilderArrayLayout.CountsOf(array, LiveBuilderArrayCopyPoses.FilledContextOf(array)).x;
            Assert.GreaterOrEqual(perSide, 3, "ряд во всю сторону 12 м");

            var original = array.transform.position;
            Assert.IsTrue(LiveBuilderTestScene.IsOpen(new Vector3(original.x, firstFloor + 1.7f, Origin.z - 1f), Vector3.forward, 2f), "первое место прорезано");
        }


        // ── To top ───────────────────────────────────────────────────────

        // Нажат To top: проём, коробка на полу этажа, перегородка и лестница — каждое с повтором по этажам до верха здания.
        // Здание — в объекте из коробок: лестнице нужен объект, в который встать.
        [Test]
        public void ToTop_SetsCountYToBuildingTop()
        {
            Object.DestroyImmediate(building.gameObject);
            building = testScene.CreateBuilding(BoxModelFactory.CreateModel(Origin), 3,
                Vector3.zero, new Vector3(12f, 0f, 0f), new Vector3(12f, 0f, 8f), new Vector3(0f, 0f, 8f));
            LiveBuilderBuildingContext.IsOnAllStoreys = true;
            var floor = building.FirstFloorWorldHeight;

            Driver().Click(RayAtFacade(5.3f, floor + 1.7f));
            AssertRepeatedToTop(Selection.activeGameObject.transform, "Cut");

            Selection.objects = new Object[0];
            var box = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>());
            box.Drag(DownRay(1f, 1f, floor + 1.5f), DownRay(3f, 3f, floor + 1.5f));
            box.Type("1");
            box.Press(KeyCode.Return);
            AssertRepeatedToTop(Selection.activeGameObject.transform, "Box");

            Selection.objects = new Object[0];
            var wall = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderWallTool>());
            wall.Click(DownRay(6f, 2f, floor + 1.5f));
            wall.Click(DownRay(6f, 6f, floor + 1.5f));
            wall.Press(KeyCode.Return);
            AssertRepeatedToTop(Object.FindObjectsByType<SplineWall>(FindObjectsInactive.Include).Single().transform, "Wall");

            Selection.objects = new Object[0];
            var stairs = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderStairsTool>());
            stairs.Drag(DownRay(8f, 2f, floor + 1.5f), DownRay(10f, 6f, floor + 1.5f));
            stairs.Type("2");
            stairs.Press(KeyCode.Return);
            AssertRepeatedToTop(Object.FindObjectsByType<BoxModelStairs>(FindObjectsInactive.Include).Single().transform, "Stairs");
        }

        // Повтор по этажам — на самом объекте или над ним, до верха здания.
        private void AssertRepeatedToTop(Transform placed, string toolName)
        {
            Assert.IsNotNull(placed, toolName + ": поставлено");
            Assert.AreEqual(building, SplineBuildingStoreys.BuildingOf(placed), toolName + ": в здании");
            Assert.IsTrue(placed.GetComponentsInParent<LiveBuilderArray>(true).Any(array => array.CountYToBuildingTop), toolName + ": повтор до верха здания");
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static LiveBuilderToolDriver Driver()
        {
            return new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderCutTool>());
        }

        // Луч снаружи на фасад z = 0 в точке along от угла здания на высоте мира.
        private static Ray RayAtFacade(float along, float worldHeight)
        {
            return new Ray(new Vector3(Origin.x + along, worldHeight, Origin.z - 5f), Vector3.forward);
        }

        // Луч сверху вниз изнутри этажа: от высоты мира, в метрах от угла здания.
        private static Ray DownRay(float x, float z, float worldHeight)
        {
            return new Ray(new Vector3(Origin.x + x, worldHeight, Origin.z + z), Vector3.down);
        }
    }
}
