using System.Linq;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// «Готово, когда» этапа 3: секция с нуля только инструментами и числами, которые правит панель, — окно Live Builder не открывается.
    /// Контур 24 × 12, 9 этажей, ряд окон на всех этажах, лестница, шахта во всю высоту, две квартиры режимом Room и дверь Cut.
    /// </summary>
    public class LiveBuilderSectionFromScratchTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private Object[] selectionBefore;
        private int storeyBefore;
        private bool isOnAllStoreysBefore;
        private bool isNewModelRequestedBefore;
        private LiveBuilderBoxMode boxModeBefore;
        private int presetBefore;
        private float customWidthBefore;
        private float customHeightBefore;
        private float customSillBefore;
        private bool isSnappedToGridBefore;


        [SetUp]
        public void OpenScene()
        {
            selectionBefore = Selection.objects;
            storeyBefore = LiveBuilderStoreys.CurrentStorey;
            isOnAllStoreysBefore = LiveBuilderBuildingContext.IsOnAllStoreys;
            isNewModelRequestedBefore = LiveBuilderBoxTool.IsNewModelRequested;
            boxModeBefore = LiveBuilderBoxModeSettings.Mode;
            presetBefore = LiveBuilderOpeningPresets.SelectedIndex;
            customWidthBefore = LiveBuilderOpeningPresets.CustomWidth;
            customHeightBefore = LiveBuilderOpeningPresets.CustomHeight;
            customSillBefore = LiveBuilderOpeningPresets.CustomSill;
            isSnappedToGridBefore = LiveBuilderCutterSettings.IsSnappedToGrid;

            Selection.objects = new Object[0];
            testScene = new LiveBuilderTestScene();
            LiveBuilderStoreys.CurrentStorey = 0;
            LiveBuilderBuildingContext.IsOnAllStoreys = false;
            LiveBuilderBoxTool.IsNewModelRequested = false;
            LiveBuilderBoxModeSettings.Mode = LiveBuilderBoxMode.Solid;
            LiveBuilderOpeningPresets.SelectedIndex = LiveBuilderOpeningPresets.CustomIndex;
            LiveBuilderCutterSettings.IsSnappedToGrid = false;
        }


        [TearDown]
        public void CloseScene()
        {
            foreach (var tool in LiveBuilderToolPalette.Tools)
                tool.Cancel();

            testScene.Dispose();
            Selection.objects = selectionBefore;
            LiveBuilderStoreys.CurrentStorey = storeyBefore;
            LiveBuilderBuildingContext.IsOnAllStoreys = isOnAllStoreysBefore;
            LiveBuilderBoxTool.IsNewModelRequested = isNewModelRequestedBefore;
            LiveBuilderBoxModeSettings.Mode = boxModeBefore;
            LiveBuilderOpeningPresets.SelectedIndex = presetBefore;
            LiveBuilderOpeningPresets.CustomWidth = customWidthBefore;
            LiveBuilderOpeningPresets.CustomHeight = customHeightBefore;
            LiveBuilderOpeningPresets.CustomSill = customSillBefore;
            LiveBuilderCutterSettings.IsSnappedToGrid = isSnappedToGridBefore;
        }


        [Test]
        public void ReferenceSection_FromScratch_WithToolsOnly()
        {
            var isWindowOpenBefore = Resources.FindObjectsOfTypeAll<LiveBuilderWindow>().Length > 0;

            var building = DrawBuilding();
            SetStoreysAsPanelDoes(building, 9);
            var floor = building.FirstFloorWorldHeight;

            var windows = PlaceWindowRowOnAllStoreys(floor);
            Assert.AreEqual(LiveBuilderArrayAlong.Wall, windows.Along, "Shift + клик — ряд вдоль стены");
            Assert.IsTrue(windows.CountYToBuildingTop, "To top — на всех этажах до верха");
            LiveBuilderRebuildQueue.RunFinalPassNow();
            Assert.GreaterOrEqual(LiveBuilderArrayLayout.CountsOf(windows, LiveBuilderArrayCopyPoses.FilledContextOf(windows)).x, 5, "ряд во всю сторону 24 м");
            Assert.IsTrue(LiveBuilderTestScene.IsOpen(new Vector3(windows.transform.position.x, floor + 8f * building.StoreyHeight + 1.6f, Origin.z - 1f),
                Vector3.forward, 2f), "окно прорезано и на девятом этаже");

            var stairs = DrawStairs(floor, building.StoreyHeight);
            Assert.AreEqual(building, SplineBuildingStoreys.BuildingOf(stairs.transform), "лестница — часть здания");

            var shaft = DrawShaft(floor);
            Assert.IsTrue(shaft.GetComponent<LiveBuilderArray>().CountYToBuildingTop, "шахта — повтор до верха здания");
            Assert.AreEqual(9, LiveBuilderArrayCopyPoses.CopiesOf(shaft.transform).Count, "шахта на всех девяти этажах, без щелей между ними");

            var firstFlat = DrawRoom(floor, 1f, 1f, 9f, 6f);
            var secondFlat = DrawRoom(floor, 1f, 7f, 9f, 11f);
            Assert.AreNotEqual(firstFlat, secondFlat, "две квартиры");
            Assert.AreEqual(building, SplineBuildingStoreys.BuildingOf(firstFlat.transform), "квартира — в здании");

            var door = PlaceDoor(floor);
            Assert.IsNotNull(door, "дверь — вырез кликом");

            Assert.AreEqual(isWindowOpenBefore, Resources.FindObjectsOfTypeAll<LiveBuilderWindow>().Length > 0, "окно Live Builder не открывалось");
        }


        // ── Шаги ─────────────────────────────────────────────────────────

        // «5», четыре клика по углам и Enter.
        private static SplineBuilding DrawBuilding()
        {
            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBuildingTool>());
            driver.Click(FloorRay(0f, 0f));
            driver.Click(FloorRay(24f, 0f));
            driver.Click(FloorRay(24f, 12f));
            driver.Click(FloorRay(0f, 12f));
            driver.Press(KeyCode.Return);

            var building = InTestScene<SplineBuilding>().Single();
            Assert.IsNotNull(building.GetComponentInParent<BoxModel>(), "здание встало в свой объект");
            return building;
        }


        // Число этажей — первое поле секции Construction: она пишет поле здания так же, через SerializedObject.
        private static void SetStoreysAsPanelDoes(SplineBuilding building, int storeys)
        {
            var serializedBuilding = new SerializedObject(building);
            serializedBuilding.FindProperty("storeyCount").intValue = storeys;
            serializedBuilding.ApplyModifiedProperties();
            LiveBuilderRebuildQueue.RunFinalPassNow();
            Physics.SyncTransforms();
            Assert.AreEqual(storeys, building.StoreyCount);
        }


        // «3», To top, Shift + клик по южному фасаду первого этажа.
        private static LiveBuilderArray PlaceWindowRowOnAllStoreys(float floor)
        {
            LiveBuilderOpeningPresets.CustomWidth = 1.2f;
            LiveBuilderOpeningPresets.CustomHeight = 1.5f;
            LiveBuilderOpeningPresets.CustomSill = 0.9f;
            LiveBuilderBuildingContext.IsOnAllStoreys = true;

            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderCutTool>());
            driver.Click(new Ray(new Vector3(Origin.x + 5.3f, floor + 1.7f, Origin.z - 3f), Vector3.forward), true);
            LiveBuilderBuildingContext.IsOnAllStoreys = false;
            Selection.objects = new Object[0];

            return InTestScene<LiveBuilderArray>().Single(array => array.GetComponent<GeometryCutter>() != null);
        }


        // «6», протянуть на полу первого этажа, набрать высоту этажа, Enter.
        private static BoxModelStairs DrawStairs(float floor, float storeyHeight)
        {
            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderStairsTool>());
            driver.Drag(DownRay(20f, 6f, floor + 1.5f), DownRay(21.5f, 11f, floor + 1.5f));
            driver.Type(Mathf.RoundToInt(storeyHeight).ToString());
            driver.Press(KeyCode.Return);
            Selection.objects = new Object[0];
            return InTestScene<BoxModelStairs>().Single();
        }


        // «2», New и To top, протянуть 2 × 2 на полу, высота 2, Enter.
        private static BoxModel DrawShaft(float floor)
        {
            LiveBuilderBoxTool.IsNewModelRequested = true;
            LiveBuilderBuildingContext.IsOnAllStoreys = true;

            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>());
            driver.Drag(DownRay(17f, 1f, floor + 1.5f), DownRay(19f, 3f, floor + 1.5f));
            driver.Type("2");
            driver.Press(KeyCode.Return);

            LiveBuilderBuildingContext.IsOnAllStoreys = false;
            var shaft = Selection.activeGameObject.GetComponentInParent<LiveBuilderArray>();
            Assert.IsNotNull(shaft, "шахта — с повтором по этажам");
            Selection.objects = new Object[0];
            return shaft.GetComponent<BoxModel>();
        }


        // «2» ещё раз — Room; протянуть прямоугольник квартиры на полу первого этажа.
        private static BoxModelRoom DrawRoom(float floor, float fromX, float fromZ, float toX, float toZ)
        {
            LiveBuilderBoxModeSettings.Mode = LiveBuilderBoxMode.Room;
            var roomsBefore = InTestScene<BoxModelRoom>();

            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>());
            driver.Drag(DownRay(fromX, fromZ, floor + 1.5f), DownRay(toX, toZ, floor + 1.5f));

            LiveBuilderBoxModeSettings.Mode = LiveBuilderBoxMode.Solid;
            Selection.objects = new Object[0];
            LiveBuilderRebuildQueue.RunFinalPassNow();
            Physics.SyncTransforms();
            return InTestScene<BoxModelRoom>().Single(room => !roomsBefore.Contains(room));
        }


        // «3», клик по стене квартиры изнутри здания.
        private static GeometryCutter PlaceDoor(float floor)
        {
            LiveBuilderOpeningPresets.CustomWidth = 0.9f;
            LiveBuilderOpeningPresets.CustomHeight = 2.1f;
            LiveBuilderOpeningPresets.CustomSill = 0f;
            var cuttersBefore = InTestScene<GeometryCutter>();

            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderCutTool>());
            driver.Click(new Ray(new Vector3(Origin.x + 12f, floor + 1f, Origin.z + 3.5f), Vector3.left));
            return InTestScene<GeometryCutter>().FirstOrDefault(cutter => !cuttersBefore.Contains(cutter));
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static Ray FloorRay(float x, float z)
        {
            var floor = LiveBuilderStoreys.CurrentFloorHeight;
            return new Ray(new Vector3(Origin.x + x, floor + 10f, Origin.z + z), Vector3.down);
        }

        // Луч вниз с высоты y — изнутри этажа, чтобы не задеть перекрытия выше.
        private static Ray DownRay(float x, float z, float y)
        {
            return new Ray(new Vector3(Origin.x + x, y, Origin.z + z), Vector3.down);
        }

        private static T[] InTestScene<T>() where T : Component
        {
            var scene = SceneManager.GetActiveScene();
            return Object.FindObjectsByType<T>(FindObjectsInactive.Exclude).Where(component => component.gameObject.scene == scene).ToArray();
        }
    }
}
