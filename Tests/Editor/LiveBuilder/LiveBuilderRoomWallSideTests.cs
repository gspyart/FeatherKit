using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Куда встают стены комнаты от нарисованной линии (<see cref="LiveBuilderRoomWalls"/>): у замкнутой стены «наружу» — со стороны, где не комната,
    /// как бы ни обходили её точки; у комнаты-здания «внутрь» — контур это габарит, стены внутри него.
    /// </summary>
    public class LiveBuilderRoomWallSideTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private LiveBuilderRoomWallSide savedWallSide;


        [SetUp]
        public void OpenScene()
        {
            testScene = new LiveBuilderTestScene();
            savedWallSide = LiveBuilderRoomSettings.WallSide;
        }


        [TearDown]
        public void CloseScene()
        {
            LiveBuilderRoomSettings.WallSide = savedWallSide;
            testScene.Dispose();
        }


        // ── Замкнутая стена ──────────────────────────────────────────────

        [Test]
        public void ClosedWall_Outward_CounterClockwise_StandsOutsideLine()
        {
            CreateSquareWall(false, LiveBuilderRoomWallSide.Outward);
            Assert.AreEqual(-0.3f, WallFaceZ(), 0.01f);
        }


        [Test]
        public void ClosedWall_Outward_Clockwise_StandsOutsideLine()
        {
            CreateSquareWall(true, LiveBuilderRoomWallSide.Outward);
            Assert.AreEqual(-0.3f, WallFaceZ(), 0.01f);
        }


        [Test]
        public void ClosedWall_Inward_StandsInsideLine()
        {
            CreateSquareWall(true, LiveBuilderRoomWallSide.Inward);
            Assert.AreEqual(0f, WallFaceZ(), 0.01f);
        }


        [Test]
        public void ClosedWall_Centered_StandsOnLineMiddle()
        {
            CreateSquareWall(false, LiveBuilderRoomWallSide.Centered);
            Assert.AreEqual(-0.15f, WallFaceZ(), 0.01f);
        }


        // ── Комната-здание ───────────────────────────────────────────────

        [Test]
        public void ContourRoom_Inward_KeepsWallsInsideContour()
        {
            LiveBuilderRoomSettings.WallSide = LiveBuilderRoomWallSide.Inward;
            var room = BoxModelFactory.CreateContourRoom(null, Origin, Quaternion.identity);
            room.WallThickness = 0.3f;
            AddSquare(room.GetComponent<SplineContainer>().Spline, false);
            SplineBuildingRebuilder.Rebuild(room);

            Assert.AreEqual(SplineBuildingContourSide.Outside, room.ContourSide);
            Assert.AreEqual(0f, WallFaceZ(room.FirstFloorWorldHeight + 1f), 0.01f, "наружная грань стены — на линии контура");
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Замкнутая стена-квадрат 4 × 4 от Origin, толщина 0,3; сторона — как выставит инструмент «Комната».
        private SplineWall CreateSquareWall(bool isClockwise, LiveBuilderRoomWallSide side)
        {
            var wall = testScene.CreateWall();
            var spline = wall.GetComponent<SplineContainer>().Spline;
            AddSquare(spline, isClockwise);
            wall.Alignment = LiveBuilderRoomWalls.AlignmentFor(spline, side);
            SplineWallRebuilder.Rebuild(wall);
            return wall;
        }

        private static void AddSquare(Spline spline, bool isClockwise)
        {
            var corners = new[] { new Vector3(0f, 0f, 0f), new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 4f), new Vector3(0f, 0f, 4f) };

            if (isClockwise)
                System.Array.Reverse(corners);

            spline.Clear();

            foreach (var corner in corners)
                spline.Add(new BezierKnot((float3)corner), TangentMode.Linear);

            spline.Closed = true;
        }

        // Где луч снаружи впервые упёрся в стену у стороны z = 0, по Z от Origin.
        private static float WallFaceZ(float height = 1f)
        {
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(new Vector3(Origin.x + 2f, height, Origin.z - 3f), Vector3.forward, out var hit, 6f), "луч не попал в стену");
            return hit.point.z - Origin.z;
        }
    }
}
