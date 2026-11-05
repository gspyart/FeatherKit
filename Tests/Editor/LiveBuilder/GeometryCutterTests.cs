using System.Collections.Generic;
using FeatherKit.EditorTools;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LevelDesign;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Вырез (<see cref="GeometryCutter"/>) в стенах и зданиях: прорезает дыру, копии вверх, едет за точками стены, стена со своими вырезами
    /// переезжает без перерезки, встаёт на все этажи здания,
    /// ряд копий модификатором Array встаёт «от края» по размеру выреза; сдвиг формы двигает дыру, а не объект, и дыру держит её отрезок стены.
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
            GeometryCutterLineSync.SyncAll();

            LiveBuilderTestScene.MoveKnot(wall.GetComponent<SplineContainer>().Spline, 1, new Vector3(10f, 0f, 10f));
            GeometryCutterLineSync.SyncAll();

            Assert.That(Vector3.Distance(window.transform.position, Origin + new Vector3(5f, 0.5f, 5f)), Is.LessThan(0.01f), "вырез — на середине нового отрезка");
            Assert.That(Vector3.Angle(window.transform.right, new Vector3(1f, 0f, 1f)), Is.LessThan(0.5f), "и повёрнут вдоль него");
        }


        // Стену двигают и поворачивают вместе с её проёмами: подпись та же — стену не перерезают на каждом шаге,
        // а порядок проёмов не зависит от того, где стена в мире.
        [Test]
        public void MovingWallWithChildCutters_KeepsSignature()
        {
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(10f, 0f, 0f));
            CreateWindow(wall, 1f, 2f, 1.5f);
            CreateWindow(wall, 6f, 2f, 1.5f);
            var signature = wall.BuiltSignature;

            for (var step = 1; step <= 12; step++)
            {
                wall.transform.position += new Vector3(0.137f * step, 0.011f, -0.293f * step);
                wall.transform.rotation *= Quaternion.Euler(0f, 7.3f, 0f);
                SplineWallRebuilder.Rebuild(wall);

                Assert.AreEqual(signature, wall.BuiltSignature, "шаг " + step + ": подпись стены та же");
            }
        }


        // ── Сдвиг формы ──────────────────────────────────────────────────

        [Test]
        public void MovingHole_KeepsObjectInPlace()
        {
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(10f, 0f, 0f));
            var window = CreateWindow(wall, 4f, 2f, 1.5f);
            var objectPosition = window.transform.position;
            var cutters = new List<GeometryCutter> { window };
            var keyBefore = GeometryCutterLookup.KeyOf(wall, cutters);

            GeometryCutterHandles.MoveShapeBottom(window, GeometryCutterOutline.ShapeBottomOf(window) + new Vector3(-3f, 0f, 0f));
            Assert.AreNotEqual(keyBefore, GeometryCutterLookup.KeyOf(wall, cutters), "подпись стены другая — пересоберётся сама");
            SplineWallRebuilder.Rebuild(wall);

            Assert.That(Vector3.Distance(window.transform.position, objectPosition), Is.LessThan(0.001f), "объект стоит");
            Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(2f, 1.25f, -2f), Vector3.forward, 4f), "дыра — на новом месте");
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(5f, 1.25f, -2f), Vector3.forward, 4f), "на старом — стена");
        }


        [Test]
        public void ShiftedHoleOnWallLine_FollowsMovedKnot()
        {
            // Объект — у края стены, дыра сдвигом — посередине: на линии держится дыра.
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(10f, 0f, 0f));
            var window = CreateWindow(wall, 1f, 2f, 1.5f);
            GeometryCutterHandles.MoveShapeBottom(window, GeometryCutterOutline.ShapeBottomOf(window) + new Vector3(3f, 0f, 0f));
            GeometryCutterLineSync.SyncAll();

            LiveBuilderTestScene.MoveKnot(wall.GetComponent<SplineContainer>().Spline, 1, new Vector3(10f, 0f, 10f));
            GeometryCutterLineSync.SyncAll();

            var holeBottom = GeometryCutterOutline.ShapeBottomOf(window);
            Assert.That(Vector3.Distance(holeBottom, Origin + new Vector3(5f, 0.5f, 5f)), Is.LessThan(0.01f), "дыра — на середине нового отрезка");
            Assert.That(Vector3.Distance(window.transform.position, holeBottom), Is.EqualTo(3f).Within(0.01f), "объект — на прежнем сдвиге от неё");
        }


        // ── Ряд копий ────────────────────────────────────────────────────

        [Test]
        public void ArrayOfCutters_StepsFromCutterEdge()
        {
            // Окна 2 м шириной с зазором 1 м: дыры 1–3, 4–6, 7–9 м по стене.
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(12f, 0f, 0f));
            var window = CreateWindow(wall, 1f, 2f, 1.5f);

            var array = window.gameObject.AddComponent<ArrayModifier>();
            array.Counts = new Vector3Int(3, 1, 1);
            array.StepsFromSize = true;
            array.Gap = new Vector3(1f, 0f, 0f);
            ArrayModifierBuilder.Rebuild(array);
            GeometryCutterLineSync.SyncAll();
            SplineWallRebuilder.Rebuild(wall);

            foreach (var holeCenter in new[] { 2f, 5f, 8f })
                Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(holeCenter, 1.25f, -2f), Vector3.forward, 4f), "дыра на " + holeCenter + " м");

            foreach (var gapCenter in new[] { 3.5f, 6.5f })
                Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(gapCenter, 1.25f, -2f), Vector3.forward, 4f), "простенок на " + gapCenter + " м");
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
