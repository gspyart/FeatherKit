using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Правка уже построенного не ломает поставленное в него: сменил толщину стен здания — окна остались сквозными;
    /// сменил высоту этажа — шахта во весь этаж выросла с ним; повернул стену здания и вернул — конец перегородки на прежнем месте;
    /// выключил у комнаты потолок или пол — там открыто, стены на месте; у замкнутой стены — свои пол и потолок, у открытой их нет.
    /// </summary>
    public class LiveBuilderEditingTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        // Внутренняя сторона стен квадрата 10 × 10 при толщине 0,3 внутрь.
        private const float Inner = 0.3f;

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


        // ── Толщина стен ─────────────────────────────────────────────────

        [Test]
        public void ThickerBuildingWalls_KeepWindowThrough()
        {
            var building = testScene.CreateBuilding(1, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var window = CreateWindow(building, 3f, 2f);
            var depthBefore = window.Depth;
            var height = building.FirstFloorWorldHeight + 1.75f;

            building.WallThickness = 0.6f;
            GeometryCutterLineSync.SyncAll();
            SplineBuildingRebuilder.Rebuild(building);

            Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(4f, height, -2f), Vector3.forward, 3f), "стена толще — окно всё равно насквозь");
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(1.5f, height, -2f), Vector3.forward, 3f), "рядом — стена");

            building.WallThickness = 0.3f;
            GeometryCutterLineSync.SyncAll();
            SplineBuildingRebuilder.Rebuild(building);

            Assert.AreEqual(depthBefore, window.Depth, 0.001f, "вернули толщину — глубина выреза прежняя");
            Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(4f, height, -2f), Vector3.forward, 3f), "и окно насквозь");
        }


        [Test]
        public void ThickerBuildingWalls_KeepNicheDepth()
        {
            var building = testScene.CreateBuilding(1, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var niche = CreateWindow(building, 3f, 2f);
            niche.Depth = 0.1f;
            GeometryCutterLineSync.SyncAll();

            building.WallThickness = 0.6f;
            GeometryCutterLineSync.SyncAll();

            Assert.AreEqual(0.1f, niche.Depth, 0.001f, "ниша мельче стены — глубина своя");
        }


        // ── Высота этажа ─────────────────────────────────────────────────

        [Test]
        public void TallerStorey_GrowsFullStoreyBoxOnly()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = testScene.CreateBuilding(model, 2, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var floor = building.FirstFloorWorldHeight - Origin.y;

            var shaft = CreateLinkedBox(model, building, new Vector3(2f, floor, 2f), building.StoreyHeight);
            var shelf = CreateLinkedBox(model, building, new Vector3(5f, floor, 5f), 1f);

            building.StoreyHeight = 5f;
            SplineBuildingLinkSync.SyncAll();

            Assert.AreEqual(5f, shaft.Size.y, 0.001f, "коробка во весь этаж — выросла с этажом");
            Assert.AreEqual(1f, shelf.Size.y, 0.001f, "коробка своей высоты — какой была");
        }

        // Коробка в интерьере здания, привязанная к его этажам, — как ставит «Коробка» на полу здания.
        private static BoxModelPart CreateLinkedBox(BoxModel model, SplineBuilding building, Vector3 localMin, float height)
        {
            var part = BoxModelFactory.CreatePart(model, localMin, localMin + new Vector3(1f, height, 1f), BoxModelPartMode.Cut);
            part.transform.SetParent(BoxModelFactory.InteriorOf(building), true);
            SplineBuildingLinkSync.Attach(part.gameObject, building, true);
            return part;
        }


        // ── Комната ──────────────────────────────────────────────────────

        [Test]
        public void RoomWithoutCeilingOrFloor_IsOpenThere()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var room = BoxModelFactory.CreateRoom(model, new Vector3(1f, 1f, 1f), new Vector3(4f, 3f, 4f), true);
            BoxModelRebuilder.Rebuild(model);

            var center = Origin + new Vector3(3f, 2.5f, 3f);
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(center + Vector3.up * 5f, Vector3.down, 4f), "с потолком — сверху закрыто");

            room.HasCeiling = false;
            BoxModelRebuilder.Rebuild(model);
            Assert.IsTrue(LiveBuilderTestScene.IsOpen(center + Vector3.up * 5f, Vector3.down, 4f), "без потолка — сверху открыто");
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(center + Vector3.right * 5f, Vector3.left, 4f), "стены на месте");

            room.HasFloor = false;
            BoxModelRebuilder.Rebuild(model);
            Assert.IsTrue(LiveBuilderTestScene.IsOpen(center + Vector3.up * 5f, Vector3.down, 7f), "без пола — насквозь");
        }


        [Test]
        public void ClosedWallWithFloorAndCeiling_IsClosedThere_OpenWallHasNoSlabs()
        {
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(6f, 0f, 0f), new Vector3(6f, 0f, 6f), new Vector3(0f, 0f, 6f));
            wall.GetComponent<SplineContainer>().Spline.Closed = true;
            wall.HasFloor = true;
            wall.HasCeiling = true;
            wall.SlabThickness = 0.1f;
            SplineWallRebuilder.Rebuild(wall);

            var center = Origin + new Vector3(3f, 1.5f, 3f);
            Assert.AreEqual(3, wall.GetComponent<MeshFilter>().sharedMesh.subMeshCount, "стена, пол, потолок — три части");
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(center + Vector3.up * 5f, Vector3.down, 4f), "сверху — потолок");
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(center, Vector3.down, 2f), "снизу — пол");

            wall.HasCeiling = false;
            SplineWallRebuilder.Rebuild(wall);
            Assert.IsTrue(LiveBuilderTestScene.IsOpen(center + Vector3.up * 5f, Vector3.down, 4.5f), "без потолка — сверху открыто");

            wall.GetComponent<SplineContainer>().Spline.Closed = false;
            SplineWallRebuilder.Rebuild(wall);
            Assert.AreEqual(1, wall.GetComponent<MeshFilter>().sharedMesh.subMeshCount, "открытая линия — без плит");
        }


        // ── Перегородки ──────────────────────────────────────────────────

        [Test]
        public void BuildingWallTurnedAndBack_ReturnsPartitionEnd()
        {
            var building = testScene.CreateBuilding(1, Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f), new Vector3(0f, 0f, 10f));
            var contour = building.GetComponent<SplineContainer>().Spline;
            var partition = testScene.CreateWall(new Vector3(5f, 0f, Inner), new Vector3(5f, 0f, 10f - Inner));
            partition.transform.SetParent(BoxModelFactory.InteriorOf(building), true);
            SplineBuildingLinkSync.Attach(partition.gameObject, building, false);
            SplineWallBuildingAnchors.SyncAll();

            // Нижняя стена здания — наискось и обратно, три раза.
            for (var turn = 0; turn < 3; turn++)
            {
                LiveBuilderTestScene.MoveKnot(contour, 1, new Vector3(10f, 0f, -2f));
                SplineWallBuildingAnchors.SyncAll();

                var tilted = (Vector3)partition.GetComponent<SplineContainer>().Spline[0].Position;
                Assert.AreEqual(5f, tilted.x, 0.001f, "стена наискось — перегородка не повернулась, конец на её продолжении");

                LiveBuilderTestScene.MoveKnot(contour, 1, new Vector3(10f, 0f, 0f));
                SplineWallBuildingAnchors.SyncAll();
            }

            var end = (Vector3)partition.GetComponent<SplineContainer>().Spline[0].Position;
            Assert.That(Vector3.Distance(end, new Vector3(5f, 0f, Inner)), Is.LessThan(0.001f), "стену вернули — конец на прежнем месте: " + end);
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Окно в нижней стене здания, как ставит «Проём»: насквозь, по месту в стене, на все этажи.
        private static GeometryCutter CreateWindow(SplineBuilding building, float alongFrom, float width)
        {
            var paths = SplineWallPath.FromContainer(building.GetComponent<SplineContainer>(), building.CurveStep);
            var firstFloor = building.transform.InverseTransformPoint(new Vector3(0f, building.FirstFloorWorldHeight, 0f)).y;
            LiveBuilderOpeningPlacer.FitCutterToWall(building, paths[0], alongFrom, width, firstFloor + 1f, out var bottom, out var rotation, out var depth);

            var window = BoxModelFactory.CreateCutter(building.transform, building.transform.TransformPoint(bottom), building.transform.rotation * rotation,
                new Vector2(width, 1.5f), depth);
            window.FollowsWallLine = true;
            SplineBuildingLinkSync.Attach(window.gameObject, building, true);
            GeometryCutterLineSync.SyncAll();
            SplineBuildingRebuilder.Rebuild(building);
            return window;
        }
    }
}
