using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Вырез (<see cref="GeometryCutter"/>) в стенах и зданиях: прорезает дыру, копии вверх, едет за точками стены, встаёт на все этажи здания.
    /// </summary>
    public class GeometryCutterTests
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


        // ── Стена по линии ───────────────────────────────────────────────

        [Test]
        public void CutterInWall_CutsHoleThrough()
        {
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(10f, 0f, 0f));
            CreateWindow(wall, 4f, 2f, 1.5f);

            Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(5f, 1.25f, -2f), Vector3.forward, 4f), "в окне — дыра");
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(2f, 1.25f, -2f), Vector3.forward, 4f), "рядом — стена");
        }


        [Test]
        public void CutterCopiesUp_CutsEveryCopy()
        {
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(10f, 0f, 0f));
            var window = CreateWindow(wall, 4f, 2f, 1.5f);
            window.CopiesUp = 2;
            window.CopyStep = 2f;
            SplineWallRebuilder.Rebuild(wall);

            Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(5f, 2.75f, -2f), Vector3.forward, 4f), "копия выше — тоже дыра");
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(5f, 2.25f, -2f), Vector3.forward, 4f), "между копиями — стена");
        }


        [Test]
        public void CutterOnWallLine_FollowsMovedKnot()
        {
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(10f, 0f, 0f));
            var window = CreateWindow(wall, 4f, 2f, 1.5f);
            HoleCutterLineSync.SyncAll();

            LiveBuilderTestScene.MoveKnot(wall.GetComponent<SplineContainer>().Spline, 1, new Vector3(10f, 0f, 10f));
            HoleCutterLineSync.SyncAll();

            Assert.That(Vector3.Distance(window.transform.position, Origin + new Vector3(5f, 0.5f, 5f)), Is.LessThan(0.01f), "вырез — на середине нового отрезка");
            Assert.That(Vector3.Angle(window.transform.right, new Vector3(1f, 0f, 1f)), Is.LessThan(0.5f), "и повёрнут вдоль него");
        }


        // ── Здание ───────────────────────────────────────────────────────

        [Test]
        public void CutterInBuilding_LinkedToAllStoreys()
        {
            var building = testScene.CreateBuilding(3, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var paths = SplineWallPath.FromContainer(building.GetComponent<SplineContainer>(), building.CurveStep);
            var firstFloor = building.transform.InverseTransformPoint(new Vector3(0f, building.FirstFloorWorldHeight, 0f)).y;
            LiveBuilderOpeningPlacer.FitCutterToWall(building, paths[0], 3f, 2f, firstFloor + 1f, out var bottom, out var rotation, out var depth);

            var window = BoxModelFactory.CreateCutter(building.transform, building.transform.TransformPoint(bottom), building.transform.rotation * rotation, new Vector2(2f, 1.5f), depth);
            window.FollowsWallLine = true;
            SplineBuildingLinkSync.Attach(window.gameObject, building, true);
            SplineBuildingRebuilder.Rebuild(building);

            Assert.AreEqual(3, window.CopiesUp, "копий — по числу этажей");

            for (var storey = 0; storey < 3; storey++)
            {
                var height = building.FirstFloorWorldHeight + 1.75f + storey * building.StoreyHeight;
                Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(4f, height, -2f), Vector3.forward, 2.4f), "дыра на этаже " + (storey + 1));
                Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(1.5f, height, -2f), Vector3.forward, 2.4f), "стена рядом на этаже " + (storey + 1));
            }
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Окно, как ставит «Проём»: вдоль стены от alongFrom, низ на bottomHeight над линией, насквозь.
        private static GeometryCutter CreateWindow(SplineWall wall, float alongFrom, float width, float height)
        {
            var paths = SplineWallPath.FromWall(wall);
            LiveBuilderOpeningPlacer.FitCutterToWall(wall, paths[0], alongFrom, width, 0.5f, out var bottom, out var rotation, out var depth);

            var window = BoxModelFactory.CreateCutter(wall.transform, wall.transform.TransformPoint(bottom), wall.transform.rotation * rotation, new Vector2(width, height), depth);
            window.FollowsWallLine = true;
            SplineWallRebuilder.Rebuild(wall);
            return window;
        }
    }
}
