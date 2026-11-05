using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Точки перегородок на стенах здания (<see cref="SplineWallBuildingAnchors"/>): сдвинул стену здания — точки на ней и в её углах едут следом,
    /// точки на других стенах стоят; добавил точку контуру — точки не прыгают.
    /// </summary>
    public class SplineWallBuildingAnchorTests
    {
        // Внутренняя сторона стен квадрата 10 × 10 при толщине 0,3 внутрь.
        private const float Inner = 0.3f;

        private LiveBuilderTestScene testScene;
        private SplineBuilding building;
        private Spline contour;


        [SetUp]
        public void OpenScene()
        {
            testScene = new LiveBuilderTestScene();
            building = testScene.CreateBuilding(1, Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f), new Vector3(0f, 0f, 10f));
            contour = building.GetComponent<SplineContainer>().Spline;
        }


        [TearDown]
        public void CloseScene()
        {
            testScene.Dispose();
        }


        [Test]
        public void MovedBuildingWall_PullsKnotsOnIt()
        {
            var partition = CreatePartition(false, new Vector3(5f, 0f, Inner), new Vector3(5f, 0f, 10f - Inner));
            var room = CreatePartition(true, new Vector3(Inner, 0f, Inner), new Vector3(4f, 0f, Inner), new Vector3(4f, 0f, 4f), new Vector3(Inner, 0f, 4f));
            SplineWallBuildingAnchors.SyncAll();

            // Нижняя стена здания — на 2 м наружу.
            LiveBuilderTestScene.MoveKnot(contour, 0, new Vector3(0f, 0f, -2f));
            LiveBuilderTestScene.MoveKnot(contour, 1, new Vector3(10f, 0f, -2f));
            SplineWallBuildingAnchors.SyncAll();

            AssertKnot(partition, 0, new Vector3(5f, 0f, Inner - 2f), "перегородка — за стеной");
            AssertKnot(partition, 1, new Vector3(5f, 0f, 10f - Inner), "другой конец — на месте");
            AssertKnot(room, 0, new Vector3(Inner, 0f, Inner - 2f), "угол комнаты — в новый угол");
            AssertKnot(room, 1, new Vector3(4f, 0f, Inner - 2f), "точка комнаты на этой стене — за ней");
            AssertKnot(room, 2, new Vector3(4f, 0f, 4f), "точка не на стене — на месте");
            AssertKnot(room, 3, new Vector3(Inner, 0f, 4f), "точка на левой стене вдоль неё не едет");
        }


        [Test]
        public void AddedContourKnot_KeepsKnotsInPlace()
        {
            var partition = CreatePartition(false, new Vector3(5f, 0f, Inner), new Vector3(5f, 0f, 10f - Inner));
            SplineWallBuildingAnchors.SyncAll();

            contour.Insert(1, new BezierKnot(new float3(5f, 0f, 0f)), TangentMode.Linear);
            SplineWallBuildingAnchors.SyncAll();
            AssertKnot(partition, 0, new Vector3(5f, 0f, Inner), "новая точка контура — точки перегородки не прыгают");

            LiveBuilderTestScene.MoveKnot(contour, 1, new Vector3(5f, 0f, -2f));
            SplineWallBuildingAnchors.SyncAll();
            Assert.That(PositionOf(partition, 0).z, Is.LessThan(-1f), "потом точка держится уже за новую точку контура");
        }


        // ── Высота перегородки ───────────────────────────────────────────

        // До потолка — этаж минус перекрытие; весь этаж — сквозь перекрытие; своя — сверка не трогает.
        [Test]
        public void PartitionHeight_FollowsChosenMode()
        {
            var partition = CreatePartition(false, new Vector3(5f, 0f, Inner), new Vector3(5f, 0f, 10f - Inner));
            Assert.AreEqual(building.ClearHeight, partition.Height, 0.001f, "по умолчанию — до потолка");

            partition.HeightInBuilding = SplineWallHeightInBuilding.FullStorey;
            SplineBuildingLinkSync.Sync(partition.GetComponent<SplineBuildingStoreyLink>());
            Assert.AreEqual(building.StoreyHeight, partition.Height, 0.001f, "весь этаж — сквозь перекрытие");

            partition.HeightInBuilding = SplineWallHeightInBuilding.Custom;
            partition.Height = 2.5f;
            SplineBuildingLinkSync.Sync(partition.GetComponent<SplineBuildingStoreyLink>());
            Assert.AreEqual(2.5f, partition.Height, 0.001f, "своя высота остаётся");
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Перегородка в здании, привязанная к его этажам, — как ставит «Стена» в здании.
        private SplineWall CreatePartition(bool isClosed, params Vector3[] points)
        {
            var wall = testScene.CreateWall(points);
            wall.GetComponent<SplineContainer>().Spline.Closed = isClosed;
            wall.transform.SetParent(BoxModelFactory.InteriorOf(building), true);
            SplineBuildingLinkSync.Attach(wall.gameObject, building, false);
            return wall;
        }

        private static Vector3 PositionOf(SplineWall wall, int knot)
        {
            return wall.GetComponent<SplineContainer>().Spline[knot].Position;
        }

        private static void AssertKnot(SplineWall wall, int knot, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(PositionOf(wall, knot), expected), Is.LessThan(0.01f), message + ": " + PositionOf(wall, knot));
        }
    }
}
