using System.Linq;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Инструменты целиком, как их нажимает окно сцены (<see cref="LiveBuilderToolDriver"/>): протянул, набрал цифры, Enter или Esc.
    /// Одни правила у всех: цифры + Enter — точно; Esc при наборе стирает только число; Esc без набора бросает начатое целиком —
    /// и коробку, и лестницу, и стену, и здание; оставить — Enter; набрано не то — жест ждёт дальше.
    /// </summary>
    public class LiveBuilderToolGestureTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private Object[] selectionBefore;


        [SetUp]
        public void OpenScene()
        {
            selectionBefore = Selection.objects;

            // Выделенное на уровне — цель для нового: без этого коробка встала бы в выделенный объект настоящей сцены.
            Selection.objects = new Object[0];
            testScene = new LiveBuilderTestScene();
        }


        [TearDown]
        public void CloseScene()
        {
            foreach (var tool in LiveBuilderToolPalette.Tools)
                tool.Cancel();

            testScene.Dispose();
            Selection.objects = selectionBefore;
        }


        // ── Коробка ──────────────────────────────────────────────────────

        [Test]
        public void BoxTool_DragThenTypedDepthAndEnter_MakesBoxThatHigh()
        {
            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>());

            driver.Drag(FloorRay(0f, 0f), FloorRay(4f, 3f));
            driver.Type("2");
            driver.Press(KeyCode.Return);

            var boxes = InTestScene<BoxModelPart>();
            Assert.AreEqual(1, boxes.Length, "одна коробка");
            Assert.AreEqual(2f, boxes[0].Size.y, 0.001f, "высота — набранная");
            Assert.IsFalse(driver.IsBusy, "готово — инструмент свободен");
        }


        [Test]
        public void BoxTool_EscWhileTyping_ClearsOnlyNumber_EscAgain_DropsBox()
        {
            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>());

            driver.Drag(FloorRay(0f, 0f), FloorRay(4f, 3f));
            driver.Type("5");
            driver.Press(KeyCode.Escape);
            Assert.IsTrue(driver.IsBusy, "Esc при наборе стёр число — коробка ещё рисуется");

            driver.Type("1,5");
            driver.Press(KeyCode.Return);
            Assert.AreEqual(1.5f, InTestScene<BoxModelPart>().Single().Size.y, 0.001f, "после стёртого числа — новое");

            driver.Drag(FloorRay(10f, 0f), FloorRay(14f, 3f));
            driver.Press(KeyCode.Escape);
            Assert.IsFalse(driver.IsBusy, "Esc без набора — бросить");
            Assert.AreEqual(1, InTestScene<BoxModelPart>().Length, "брошенная коробка не встала");
        }


        [Test]
        public void BoxTool_SidesTypedWhileDragging_ThenDepth_MakeExactBox()
        {
            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>());

            driver.Hold(FloorRay(0f, 0f), FloorRay(1f, 1f));
            driver.Type("4x3");
            driver.Release();
            driver.Type("2");
            driver.Press(KeyCode.Return);

            var size = InTestScene<BoxModelPart>().Single().Size;
            Assert.AreEqual(4f, size.x, 0.001f, "набрано пока тянул — по X");
            Assert.AreEqual(3f, size.z, 0.001f, "и по Z");
            Assert.AreEqual(2f, size.y, 0.001f, "глубина — после");
        }


        // Балкон: коробка наружу со стены здания — его часть, и с «И этажи выше» встаёт на все этажи, хотя середина вне его стен.
        [Test]
        public void BoxTool_OnOuterWallOfBuilding_WithAllStoreys_AttachesToBuilding()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = testScene.CreateBuilding(model, 2, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var floor = building.FirstFloorWorldHeight;
            var allStoreysBefore = LiveBuilderBuildingContext.IsOnAllStoreys;
            var drawModeBefore = BoxModelDrawModeSetting.Current;
            LiveBuilderBuildingContext.IsOnAllStoreys = true;
            BoxModelDrawModeSetting.Current = BoxModelDrawMode.Auto;

            try
            {
                var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>());
                driver.Drag(WallRay(floor + 0.5f, 2f), WallRay(floor + 2.5f, 6f));
                driver.Type("2");
                driver.Press(KeyCode.Return);

                var link = InTestScene<BoxModelPart>().Single().GetComponent<SplineBuildingStoreyLink>();
                Assert.IsNotNull(link, "балкон привязан к зданию");
                Assert.AreEqual(building, link.Building, "к тому, на чьей стене нарисован");
                Assert.AreEqual(2, link.CopiesFrom(link.Storey), "«И этажи выше» — на обоих этажах");
            }
            finally
            {
                LiveBuilderBuildingContext.IsOnAllStoreys = allStoreysBefore;
                BoxModelDrawModeSetting.Current = drawModeBefore;
            }
        }

        // Луч с запада на стену x = 0 тестового здания: на высоте мира и в метрах по Z от начала сцены.
        private static Ray WallRay(float worldHeight, float z)
        {
            return new Ray(new Vector3(Origin.x - 10f, worldHeight, Origin.z + z), Vector3.right);
        }


        // ── Комната ──────────────────────────────────────────────────────

        [Test]
        public void RoomTool_SidesTypedWhileDragging_MakeRoomThatSize()
        {
            var shapeBefore = LiveBuilderRoomSettings.Shape;
            var wallSideBefore = LiveBuilderRoomSettings.WallSide;
            LiveBuilderRoomSettings.Shape = LiveBuilderRoomShape.Rectangle;
            LiveBuilderRoomSettings.WallSide = LiveBuilderRoomWallSide.Outward;

            try
            {
                var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderRoomTool>());
                driver.Hold(FloorRay(0f, 0f), FloorRay(1f, 1f));
                driver.Type("5x3,5");
                driver.Release();

                var size = InTestScene<BoxModelRoom>().Single().Size;
                Assert.AreEqual(5f, size.x, 0.001f, "внутри по X — набранное");
                Assert.AreEqual(3.5f, size.z, 0.001f, "по Z — тоже");
            }
            finally
            {
                LiveBuilderRoomSettings.Shape = shapeBefore;
                LiveBuilderRoomSettings.WallSide = wallSideBefore;
            }
        }


        // ── Лестница ─────────────────────────────────────────────────────

        [Test]
        public void StairsTool_BadTypedHeightWaits_GoodOneMakesStairs()
        {
            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderStairsTool>());

            driver.Drag(FloorRay(0f, 0f), FloorRay(1.5f, 5f));
            driver.Type(",");
            driver.Press(KeyCode.Return);
            Assert.IsTrue(driver.IsBusy, "набрано не число — лестница ждёт, как и коробка");
            Assert.AreEqual(0, InTestScene<BoxModelStairs>().Length, "лестницы ещё нет");

            driver.Type("3");
            driver.Press(KeyCode.Return);

            var stairs = InTestScene<BoxModelStairs>();
            Assert.AreEqual(1, stairs.Length, "лестница встала");
            Assert.AreEqual(3f, stairs[0].Height, 0.001f, "высота — набранная");
        }


        // ── Линия ────────────────────────────────────────────────────────

        [Test]
        public void WallTool_ClicksAndEnter_MakeWallThroughThosePoints()
        {
            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderWallTool>());

            driver.Click(FloorRay(0f, 0f));
            driver.Click(FloorRay(6f, 0f));
            driver.Click(FloorRay(6f, 4f));
            driver.Press(KeyCode.Return);

            var wall = InTestScene<SplineWall>().Single();
            Assert.AreEqual(3, wall.GetComponent<SplineContainer>().Spline.Count, "три точки");
            Assert.IsFalse(driver.IsBusy, "Enter — готово");
        }


        [Test]
        public void WallTool_Esc_DropsWholeLine()
        {
            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderWallTool>());

            driver.Click(FloorRay(0f, 0f));
            driver.Click(FloorRay(6f, 0f));
            driver.Move(FloorRay(6f, 4f));
            driver.Press(KeyCode.Escape);

            Assert.IsEmpty(InTestScene<SplineWall>(), "Esc — передумал: начатой стены нет, как недорисованной коробки");
            Assert.IsFalse(driver.IsBusy, "рисование брошено");
        }


        [Test]
        public void BuildingTool_Esc_DropsContour_EnterKeepsIt()
        {
            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBuildingTool>());

            driver.Click(FloorRay(0f, 0f));
            driver.Click(FloorRay(8f, 0f));
            driver.Click(FloorRay(8f, 6f));
            driver.Press(KeyCode.Escape);
            Assert.IsEmpty(InTestScene<SplineBuilding>(), "Esc — здание не замкнулось и не построилось, его нет");

            driver.Click(FloorRay(20f, 0f));
            driver.Click(FloorRay(28f, 0f));
            driver.Click(FloorRay(28f, 6f));
            driver.Press(KeyCode.Return);
            Assert.AreEqual(1, InTestScene<SplineBuilding>().Length, "Enter — здание осталось");
        }


        [Test]
        public void BuildingTool_ClicksAndEnter_MakeClosedContour()
        {
            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBuildingTool>());

            driver.Click(FloorRay(0f, 0f));
            driver.Click(FloorRay(8f, 0f));
            driver.Click(FloorRay(8f, 6f));
            driver.Click(FloorRay(0f, 6f));
            driver.Press(KeyCode.Return);

            var spline = InTestScene<SplineBuilding>().Single().GetComponent<SplineContainer>().Spline;
            Assert.AreEqual(4, spline.Count, "четыре точки");
            Assert.IsTrue(spline.Closed, "здание замкнулось само");
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Луч сверху на пол текущего этажа, в метрах от начала тестовой сцены.
        private static Ray FloorRay(float x, float z)
        {
            var floor = LiveBuilderStoreys.CurrentFloorHeight;
            return new Ray(new Vector3(Origin.x + x, floor + 10f, Origin.z + z), Vector3.down);
        }

        private static T[] InTestScene<T>() where T : Component
        {
            var scene = SceneManager.GetActiveScene();
            return Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(component => component.gameObject.scene == scene).ToArray();
        }
    }
}
