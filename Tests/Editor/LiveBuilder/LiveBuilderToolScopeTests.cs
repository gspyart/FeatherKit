using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Кто что правит (<see cref="LiveBuilderTool.CanEdit(UnityEngine.GameObject)"/>): «Выделение» — всё, «Коробка» — коробки, вырез-коробки и комнаты из коробок,
    /// «Стена» — только стены по линии, «Здание» — здания и их части, «Cut» — только вырезы, «Линейка» — ничего.
    /// </summary>
    public class LiveBuilderToolScopeTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;


        [SetUp]
        public void OpenScene()
        {
            testScene = new LiveBuilderTestScene();
        }


        [TearDown]
        public void CloseScene()
        {
            testScene.Dispose();
        }


        [Test]
        public void EachToolEditsOnlyItsOwn()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var box = BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 2f, 2f), BoxModelPartMode.Add);
            var cutBox = BoxModelFactory.CreateCutterBox(model, new Vector3(0.5f, 0.5f, -1f), new Vector3(1.5f, 1.5f, 3f));
            var legacyCutBox = BoxModelFactory.CreatePart(model, new Vector3(0.5f, 0.5f, -1f), new Vector3(1.5f, 1.5f, 3f), BoxModelPartMode.Cut);
            var wall = testScene.CreateWall(new Vector3(10f, 0f, 0f), new Vector3(16f, 0f, 0f));
            var building = testScene.CreateBuilding(3, new Vector3(30f, 0f, 0f), new Vector3(38f, 0f, 0f), new Vector3(38f, 0f, 8f), new Vector3(30f, 0f, 8f));

            Assert.IsTrue(Tool<LiveBuilderSelectTool>().CanEdit(wall));
            Assert.IsTrue(Tool<LiveBuilderSelectTool>().CanEdit(model.gameObject), "объект из коробок целиком — только в «Выделении»");

            Assert.IsTrue(Tool<LiveBuilderBoxTool>().CanEdit(box));
            Assert.IsFalse(Tool<LiveBuilderBoxTool>().CanEdit(wall), "«Коробка» не правит стены");
            Assert.IsFalse(Tool<LiveBuilderBoxTool>().CanEdit(model.gameObject));

            Assert.IsTrue(Tool<LiveBuilderWallTool>().CanEdit(wall));
            Assert.IsFalse(Tool<LiveBuilderWallTool>().CanEdit(box));
            Assert.IsFalse(Tool<LiveBuilderWallTool>().CanEdit(building));

            Assert.IsTrue(Tool<LiveBuilderBuildingTool>().CanEdit(building));
            Assert.IsTrue(Tool<LiveBuilderBuildingTool>().CanEdit(building.GetComponentInChildren<SplineBuildingPart>()), "часть здания — как здание");
            Assert.IsFalse(Tool<LiveBuilderBoxTool>().CanEdit(building), "здание в три этажа — не комната");

            Assert.IsTrue(Tool<LiveBuilderCutTool>().CanEdit(cutBox), "вырез-коробка — вырез");
            Assert.IsTrue(Tool<LiveBuilderBoxTool>().CanEdit(cutBox), "вырез-коробку тянут за грани, как коробку");
            Assert.IsFalse(Tool<LiveBuilderCutTool>().CanEdit(legacyCutBox), "старая вырезающая коробка — коробка, не вырез");
            Assert.IsFalse(Tool<LiveBuilderCutTool>().CanEdit(box));

            Assert.IsFalse(Tool<LiveBuilderRulerTool>().CanEdit(box));
            Assert.IsTrue(Tool<LiveBuilderBoxTool>().CanEditFaces);
            Assert.IsFalse(Tool<LiveBuilderWallTool>().CanEditFaces);
        }


        [Test]
        public void BoxTool_EditsBoxModelRooms_NotBuildingsOrClosedWalls()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var room = BoxModelFactory.CreateRoom(model, Vector3.zero, new Vector3(4f, 3f, 4f), true);
            var oneStoreyBuilding = testScene.CreateBuilding(1, new Vector3(20f, 0f, 0f), new Vector3(24f, 0f, 0f), new Vector3(24f, 0f, 4f));
            var closedWall = testScene.CreateWall(new Vector3(10f, 0f, 0f), new Vector3(14f, 0f, 0f), new Vector3(14f, 0f, 4f));
            closedWall.GetComponent<UnityEngine.Splines.SplineContainer>().Spline.Closed = true;

            Assert.IsTrue(Tool<LiveBuilderBoxTool>().CanEdit(room), "комната из коробок — «Коробка» в любом режиме");
            Assert.IsFalse(Tool<LiveBuilderBoxTool>().CanEdit(oneStoreyBuilding), "здание в один этаж — здание");
            Assert.IsFalse(Tool<LiveBuilderBoxTool>().CanEdit(closedWall), "замкнутая стена — стена");
        }


        private static T Tool<T>() where T : LiveBuilderTool
        {
            return LiveBuilderToolPalette.Get<T>();
        }
    }
}
