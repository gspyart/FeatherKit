using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Кто что правит (<see cref="LiveBuilderToolScope"/>): «Выделение» — всё, «Коробка» — только коробки, «Стена» — только стены по линии,
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

            Assert.IsTrue(LiveBuilderToolScope.CanEdit(LiveBuilderTool.Select, wall));
            Assert.IsTrue(LiveBuilderToolScope.CanEdit(LiveBuilderTool.Select, model.gameObject), "объект из коробок целиком — только в «Выделении»");

            Assert.IsTrue(LiveBuilderToolScope.CanEdit(LiveBuilderTool.Box, box));
            Assert.IsFalse(LiveBuilderToolScope.CanEdit(LiveBuilderTool.Box, wall), "«Коробка» не правит стены");
            Assert.IsFalse(LiveBuilderToolScope.CanEdit(LiveBuilderTool.Box, model.gameObject));

            Assert.IsTrue(LiveBuilderToolScope.CanEdit(LiveBuilderTool.Wall, wall));
            Assert.IsFalse(LiveBuilderToolScope.CanEdit(LiveBuilderTool.Wall, box));
            Assert.IsFalse(LiveBuilderToolScope.CanEdit(LiveBuilderTool.Wall, building));

            Assert.IsTrue(LiveBuilderToolScope.CanEdit(LiveBuilderTool.Building, building));
            Assert.IsTrue(LiveBuilderToolScope.CanEdit(LiveBuilderTool.Building, building.GetComponentInChildren<SplineBuildingPart>()), "часть здания — как здание");
            Assert.IsFalse(LiveBuilderToolScope.CanEdit(LiveBuilderTool.Room, building), "здание в три этажа — не комната");

            Assert.IsTrue(LiveBuilderToolScope.CanEdit(LiveBuilderTool.Opening, cutBox), "вырезающая коробка — проём");
            Assert.IsFalse(LiveBuilderToolScope.CanEdit(LiveBuilderTool.Opening, box));

            Assert.IsFalse(LiveBuilderToolScope.CanEdit(LiveBuilderTool.Ruler, box));
            Assert.IsTrue(LiveBuilderToolScope.CanEditFaces(LiveBuilderTool.Box));
            Assert.IsFalse(LiveBuilderToolScope.CanEditFaces(LiveBuilderTool.Wall));
        }


        [Test]
        public void ContourRoom_EditedByRoomTool()
        {
            var room = BoxModelFactory.CreateContourRoom(null, Origin, Quaternion.identity);
            var closedWall = testScene.CreateWall(new Vector3(10f, 0f, 0f), new Vector3(14f, 0f, 0f), new Vector3(14f, 0f, 4f));
            closedWall.GetComponent<UnityEngine.Splines.SplineContainer>().Spline.Closed = true;

            Assert.IsTrue(LiveBuilderToolScope.CanEdit(LiveBuilderTool.Room, room), "комната по контуру в пустоте — здание в один этаж");
            Assert.IsTrue(LiveBuilderToolScope.CanEdit(LiveBuilderTool.Room, closedWall), "в здании и доме — замкнутая стена");
        }
    }
}
