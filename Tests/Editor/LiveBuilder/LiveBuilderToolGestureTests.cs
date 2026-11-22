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
        private LiveBuilderBoxMode boxModeBefore;


        [SetUp]
        public void OpenScene()
        {
            selectionBefore = Selection.objects;
            boxModeBefore = LiveBuilderBoxModeSettings.Mode;
            LiveBuilderBoxModeSettings.Mode = LiveBuilderBoxMode.Solid;

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
            LiveBuilderBoxModeSettings.Mode = boxModeBefore;
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


        // Балкон: коробка наружу со стены здания — его часть, в его первом этаже, и с «И этажи выше» — повтор на все этажи, хотя середина вне его стен.
        [Test]
        public void BoxTool_OnOuterWallOfBuilding_WithAllStoreys_RepeatsInBuildingStorey()
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

                var balcony = InTestScene<BoxModelPart>().Single().transform;
                Assert.AreEqual(SplineBuildingStoreys.FindContainer(building, 0).transform, balcony.parent, "балкон — в первом этаже того, на чьей стене нарисован");
                Assert.IsTrue(balcony.GetComponent<LiveBuilderArray>().CountYToBuildingTop, "«И этажи выше» — повтор до верха");
                Assert.AreEqual(2, LiveBuilderArrayCopyPoses.CopiesOf(balcony).Count, "на обоих этажах");
            }
            finally
            {
                LiveBuilderBuildingContext.IsOnAllStoreys = allStoreysBefore;
                BoxModelDrawModeSetting.Current = drawModeBefore;
            }
        }

        // Ограждение на балконной плите: коробка на коробке, которая уже часть здания, — тоже его часть, в том же этаже и на тех же этажах.
        [Test]
        public void BoxTool_OnBalconySlab_LandsInSameStoreyToo()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = testScene.CreateBuilding(model, 3, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var secondFloor = building.FirstFloorWorldHeight + building.StoreyHeight;
            var allStoreysBefore = LiveBuilderBuildingContext.IsOnAllStoreys;
            var drawModeBefore = BoxModelDrawModeSetting.Current;
            var gridBefore = EditorSnapSettings.gridSize;
            LiveBuilderBuildingContext.IsOnAllStoreys = true;
            BoxModelDrawModeSetting.Current = BoxModelDrawMode.Auto;

            // Плита — 0,2 м: на сетке крупнее она прилипла бы в ноль, и коробки бы не было.
            EditorSnapSettings.gridSize = Vector3.one * 0.1f;

            try
            {
                var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>());
                driver.Drag(WallRay(secondFloor - 0.2f, 2f), WallRay(secondFloor, 6f));
                driver.Type("1,5");
                driver.Press(KeyCode.Return);
                var slab = Selection.activeGameObject;
                BoxModelRebuilder.Rebuild(model);
                Physics.SyncTransforms();

                // Сверху на плиту второго этажа — ниже плиты третьего.
                driver.Drag(DownRay(-1.4f, 2.1f, secondFloor + 2f), DownRay(-0.1f, 5.9f, secondFloor + 2f));
                driver.Type("1");
                driver.Press(KeyCode.Return);
                var railing = Selection.activeGameObject;

                Assert.AreNotEqual(slab, railing, "встала новая коробка");
                Assert.AreEqual(SplineBuildingStoreys.FindContainer(building, 1).transform, railing.transform.parent, "ограждение — во втором этаже, как и плита");
                Assert.AreEqual(slab.transform.parent, railing.transform.parent, "в том же этаже");
                Assert.IsNotNull(railing.GetComponent<LiveBuilderArray>(), "свой повтор по этажам");
                Assert.AreEqual(LiveBuilderArrayCopyPoses.CopiesOf(slab.transform).Count, LiveBuilderArrayCopyPoses.CopiesOf(railing.transform).Count,
                    "на тех же этажах, что и плита");
            }
            finally
            {
                LiveBuilderBuildingContext.IsOnAllStoreys = allStoreysBefore;
                BoxModelDrawModeSetting.Current = drawModeBefore;
                EditorSnapSettings.gridSize = gridBefore;
            }
        }


        // Второй марш на площадке лестничной клетки: площадка — часть здания, марш на ней — тоже, в том же этаже, с высотой над его полом.
        [Test]
        public void StairsTool_OnLandingInBuilding_LandsInSameStoreyToo()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = testScene.CreateBuilding(model, 3, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var floor = building.FirstFloorWorldHeight;
            var allStoreysBefore = LiveBuilderBuildingContext.IsOnAllStoreys;
            LiveBuilderBuildingContext.IsOnAllStoreys = true;

            try
            {
                var box = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>());
                box.Drag(DownRay(1f, 1f, floor + 1.5f), DownRay(4f, 3f, floor + 1.5f));
                box.Type("2");
                box.Press(KeyCode.Return);
                var landing = Selection.activeGameObject.transform;
                Assert.AreEqual(SplineBuildingStoreys.FindContainer(building, 0).transform, landing.parent, "площадка на полу здания — в первом этаже");
                BoxModelRebuilder.Rebuild(model);
                Physics.SyncTransforms();

                Selection.objects = new Object[0];
                var stairs = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderStairsTool>());
                stairs.Drag(DownRay(3.5f, 2.5f, floor + 3.5f), DownRay(2f, 5.5f, floor + 3.5f));
                stairs.Type("2");
                stairs.Press(KeyCode.Return);

                var flight = InTestScene<BoxModelStairs>().Single();
                Assert.AreEqual(landing.parent, flight.transform.parent, "марш на площадке — в том же этаже");
                Assert.AreEqual(building, SplineBuildingStoreys.BuildingOf(flight.transform), "часть того же здания");
                Assert.AreEqual(2f, flight.transform.position.y - landing.parent.position.y, 0.01f, "стоит на площадке — на 2 над полом этажа");
                Assert.AreEqual("last", flight.GetComponent<LiveBuilderArray>().SkipY, "лестница по этажам — без верхнего");
            }
            finally
            {
                LiveBuilderBuildingContext.IsOnAllStoreys = allStoreysBefore;
            }
        }

        // Коробка на крыше (будка выхода): коробка объекта здания в его «Roof» — добавили этаж, поднялась с крышей.
        [Test]
        public void BoxTool_OnRoof_LandsInRoof_FollowsNewStorey()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = testScene.CreateBuilding(model, 2, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var roofTop = building.FirstFloorWorldHeight + building.StoreyCount * building.StoreyHeight;
            Physics.SyncTransforms();

            var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>());
            driver.Drag(DownRay(2f, 2f, roofTop + 5f), DownRay(5f, 4f, roofTop + 5f));
            driver.Type("2,5");
            driver.Press(KeyCode.Return);

            var hut = Selection.activeGameObject.transform;
            Assert.AreEqual(SplineBuildingStoreys.FindRoof(building).transform, hut.parent, "коробка на крыше — в «Roof»");
            Assert.IsNull(hut.GetComponent<LiveBuilderArray>(), "по этажам не повторяется");

            var heightBefore = hut.transform.position.y;
            building.StoreyCount = 3;
            SplineBuildingStoreySync.SyncAll();
            Assert.AreEqual(heightBefore + building.StoreyHeight, hut.transform.position.y, 0.001f, "добавили этаж — поднялся с крышей");
        }


        // Лестница с площадкой — вся клетка 4 × 7 одним протягиванием: марши и площадка у дальнего конца, этажная площадка у ближнего с целым перекрытием,
        // над маршами — дыра. Поменяли высоту этажа — лестница на весь новый этаж, площадка на середине новой высоты.
        [Test]
        public void StairsTool_Switchback_FillsWell_AndFollowsStoreyHeight()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = testScene.CreateBuilding(model, 3, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var floor = building.FirstFloorWorldHeight;
            var shapeBefore = LiveBuilderStairsSettings.Shape;
            var allStoreysBefore = LiveBuilderBuildingContext.IsOnAllStoreys;
            LiveBuilderStairsSettings.Shape = BoxModelStairsShape.Switchback;
            LiveBuilderBuildingContext.IsOnAllStoreys = true;

            try
            {
                var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderStairsTool>());
                driver.Drag(DownRay(0.5f, 7.5f, floor + 1.5f), DownRay(4.5f, 0.5f, floor + 1.5f));
                driver.Type("4");
                driver.Press(KeyCode.Return);

                var stairs = InTestScene<BoxModelStairs>().Single();
                Assert.IsTrue(stairs.IsSwitchback, "лестница с площадкой");
                Assert.IsNotNull(stairs.GetComponent<LiveBuilderArray>(), "повтор по этажам здания");
                Assert.Less(stairs.Length, 7f, "этажная площадка осталась у ближнего конца клетки");
                RebuildAll(model, building);

                var landingTop = floor + stairs.FirstFlightStepCount * stairs.Height / stairs.StepCount;
                Assert.AreEqual(landingTop, HitHeightBelow(2.5f, 1f, floor + 3.5f), 0.01f, "площадка у дальнего конца — на середине высоты");
                Assert.IsTrue(Physics.Raycast(new Vector3(Origin.x + 2.5f, floor + 1f, Origin.z + 7f), Vector3.up, 3f), "над этажной площадкой перекрытие цело");
                Assert.IsTrue(LiveBuilderTestScene.IsOpen(new Vector3(Origin.x + 1.5f, floor + 2.6f, Origin.z + 3.5f), Vector3.up, 1.3f), "над маршами — дыра в перекрытии");

                building.StoreyHeight = 5f;
                SplineBuildingStoreySync.SyncAll();
                RebuildAll(model, building);

                Assert.AreEqual(5f, stairs.Height, 0.001f, "лестница — на весь новый этаж");
                landingTop = floor + stairs.FirstFlightStepCount * stairs.Height / stairs.StepCount;
                Assert.AreEqual(landingTop, HitHeightBelow(2.5f, 1f, floor + 4.5f), 0.01f, "площадка — на середине новой высоты");
            }
            finally
            {
                LiveBuilderStairsSettings.Shape = shapeBefore;
                LiveBuilderBuildingContext.IsOnAllStoreys = allStoreysBefore;
            }
        }

        private static void RebuildAll(BoxModel model, SplineBuilding building)
        {
            BoxModelRebuilder.Rebuild(model);
            SplineBuildingRebuilder.Rebuild(building);
            Physics.SyncTransforms();
        }

        // Высота мира, в которую упирается луч сверху вниз; не упёрся — минус бесконечность.
        private static float HitHeightBelow(float x, float z, float worldHeight)
        {
            return Physics.Raycast(DownRay(x, z, worldHeight), out var hit, 10f) ? hit.point.y : float.NegativeInfinity;
        }

        // Луч сверху вниз из точки на высоте мира, в метрах по X и Z от начала сцены.
        private static Ray DownRay(float x, float z, float worldHeight)
        {
            return new Ray(new Vector3(Origin.x + x, worldHeight, Origin.z + z), Vector3.down);
        }

        // Луч с запада на стену x = 0 тестового здания: на высоте мира и в метрах по Z от начала сцены.
        private static Ray WallRay(float worldHeight, float z)
        {
            return new Ray(new Vector3(Origin.x - 10f, worldHeight, Origin.z + z), Vector3.right);
        }


        // ── Комната ──────────────────────────────────────────────────────

        [Test]
        public void RoomMode_SidesTypedWhileDragging_MakeRoomThatSize()
        {
            var wallSideBefore = LiveBuilderRoomSettings.WallSide;
            LiveBuilderRoomSettings.WallSide = LiveBuilderRoomWallSide.Outward;
            LiveBuilderBoxModeSettings.Mode = LiveBuilderBoxMode.Room;

            try
            {
                var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>());
                driver.Hold(FloorRay(0f, 0f), FloorRay(1f, 1f));
                driver.Type("5x3,5");
                driver.Release();

                var size = InTestScene<BoxModelRoom>().Single().Size;
                Assert.AreEqual(5f, size.x, 0.001f, "внутри по X — набранное");
                Assert.AreEqual(3.5f, size.z, 0.001f, "по Z — тоже");
            }
            finally
            {
                LiveBuilderRoomSettings.WallSide = wallSideBefore;
            }
        }


        // Клик без протягивания в режиме Room комнату не ставит, а подсказка говорит, чем рисовать комнату по контуру.
        [Test]
        public void RoomMode_ClickWithoutDrag_HintsWallOrBuilding()
        {
            LiveBuilderBoxModeSettings.Mode = LiveBuilderBoxMode.Room;
            var boxTool = LiveBuilderToolPalette.Get<LiveBuilderBoxTool>();

            Assert.IsFalse(new LiveBuilderToolDriver(boxTool).Click(FloorRay(1f, 1f)), "клик — не жест, его выделяет окно сцены");
            Assert.IsEmpty(InTestScene<BoxModelRoom>(), "комнаты нет");
            StringAssert.Contains("Wall", boxTool.Hint);
            StringAssert.Contains("Building", boxTool.Hint);
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
