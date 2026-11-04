using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Кто что правит (<see cref="LiveBuilderTool.CanEdit(UnityEngine.GameObject)"/>): «Выделение» — всё, «Коробка» — только коробки, «Стена» — только стены по линии,
    /// «Здание» — здания и их части, «Комната» — комнаты, в том числе по контуру, «Проём» — вырезы и вырезающие коробки, «Линейка» — ничего.
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
            var cutBox = BoxModelFactory.CreatePart(model, new Vector3(0.5f, 0.5f, -1f), new Vector3(1.5f, 1.5f, 3f), BoxModelPartMode.Cut);
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
            Assert.IsFalse(Tool<LiveBuilderRoomTool>().CanEdit(building), "здание в три этажа — не комната");

            Assert.IsTrue(Tool<LiveBuilderOpeningTool>().CanEdit(cutBox), "вырезающая коробка — проём");
            Assert.IsFalse(Tool<LiveBuilderOpeningTool>().CanEdit(box));

            Assert.IsFalse(Tool<LiveBuilderRulerTool>().CanEdit(box));
            Assert.IsTrue(Tool<LiveBuilderBoxTool>().CanEditFaces);
            Assert.IsFalse(Tool<LiveBuilderWallTool>().CanEditFaces);
        }


        [Test]
        public void ContourRoom_EditedByRoomTool()
        {
            var room = BoxModelFactory.CreateContourRoom(null, Origin, Quaternion.identity);
            var closedWall = testScene.CreateWall(new Vector3(10f, 0f, 0f), new Vector3(14f, 0f, 0f), new Vector3(14f, 0f, 4f));
            closedWall.GetComponent<UnityEngine.Splines.SplineContainer>().Spline.Closed = true;

            Assert.IsTrue(Tool<LiveBuilderRoomTool>().CanEdit(room), "комната по контуру в пустоте — здание в один этаж");
            Assert.IsTrue(Tool<LiveBuilderRoomTool>().CanEdit(closedWall), "в здании и доме — замкнутая стена");
        }


        private static T Tool<T>() where T : LiveBuilderTool
        {
            return LiveBuilderToolPalette.Get<T>();
        }
    }
}
