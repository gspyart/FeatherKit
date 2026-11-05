using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Прямоугольник на поверхности и проём «Проёма»: толщина тонкой стены изнутри шахты — из настроек стены, вырез изнутри режет всю толщину;
    /// углы прилипают к краю стены, стоящей не по крупной сетке, а сетка вдоль стены идёт от её края — и у протянутого проёма, и у проёма кликом «По сетке».
    /// </summary>
    public class LiveBuilderSurfaceRectangleTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private Vector3 previousGridSize;
        private bool previousIsThrough;
        private bool previousIsSnappedToGrid;
        private LiveBuilderCutterShape previousShape;


        [SetUp]
        public void OpenScene()
        {
            testScene = new LiveBuilderTestScene();
            previousGridSize = EditorSnapSettings.gridSize;
            previousIsThrough = LiveBuilderCutterSettings.IsThrough;
            previousIsSnappedToGrid = LiveBuilderCutterSettings.IsSnappedToGrid;
            previousShape = LiveBuilderCutterSettings.Shape;
            EditorSnapSettings.gridSize = Vector3.one;
            LiveBuilderCutterSettings.Shape = LiveBuilderCutterShape.Rectangle;
        }


        [TearDown]
        public void CloseScene()
        {
            EditorSnapSettings.gridSize = previousGridSize;
            LiveBuilderCutterSettings.IsThrough = previousIsThrough;
            LiveBuilderCutterSettings.IsSnappedToGrid = previousIsSnappedToGrid;
            LiveBuilderCutterSettings.Shape = previousShape;
            Selection.activeObject = null;
            testScene.Dispose();
        }


        // ── Толщина ──────────────────────────────────────────────────────

        [Test]
        public void ThinClosedWall_ThicknessFromInnerFace_IsWallThickness()
        {
            CreateShaft();
            var rectangle = new LiveBuilderSurfaceRectangle();

            Assert.IsTrue(rectangle.TryBegin(RayFromShaftCenter()), "изнутри шахты луч попал в стену");
            Assert.Greater(rectangle.Normal.z, 0.9f, "это внутренняя сторона, смотрит в шахту");
            Assert.AreEqual(0.1f, rectangle.ThicknessAt(rectangle.Origin), 0.001f);
        }


        [Test]
        public void CutterFromInnerFace_CoversWholeWallThickness()
        {
            CreateShaft();
            LiveBuilderCutterSettings.IsThrough = true;
            var gesture = new LiveBuilderCutterGesture();
            var innerFaceZ = Origin.z + 0.1f;

            Assert.IsTrue(gesture.TryBegin(RayFromShaftCenter(0.9f, 1f)));
            gesture.Update(RayFromShaftCenter(2f, 2.5f));
            gesture.Commit();

            var cutter = Selection.activeGameObject.GetComponent<GeometryCutter>();
            var depthAxis = cutter.transform.forward;
            var nearZ = cutter.transform.position.z - Mathf.Abs(depthAxis.z) * cutter.Depth / 2f;
            var farZ = cutter.transform.position.z + Mathf.Abs(depthAxis.z) * cutter.Depth / 2f;

            Assert.Greater(Mathf.Abs(depthAxis.z), 0.99f, "вырез поперёк стены");
            Assert.Less(nearZ, Origin.z - 0.001f, "вырез выходит за наружную сторону");
            Assert.Greater(farZ, innerFaceZ + 0.001f, "вырез выходит за внутреннюю сторону");
        }

        // Замкнутая стена 3 × 3 м толщиной 0,1 — шахта лифта. Обход 0 → +X → +Z: шахта слева по ходу, туда и стена.
        // Нижняя стена — от z = Origin.z до Origin.z + 0,1, внутренняя сторона — вторая.
        private SplineWall CreateShaft()
        {
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(3f, 0f, 0f), new Vector3(3f, 0f, 3f), new Vector3(0f, 0f, 3f));
            wall.GetComponent<UnityEngine.Splines.SplineContainer>().Spline.Closed = true;
            wall.Thickness = 0.1f;
            wall.Alignment = SplineWallAlignment.Left;
            SplineWallRebuilder.Rebuild(wall);
            Physics.SyncTransforms();
            return wall;
        }

        // Из шахты к нижней стене: x вдоль неё и высота — от угла шахты.
        private static Ray RayFromShaftCenter(float x = 1.5f, float height = 1.5f)
        {
            return new Ray(Origin + new Vector3(x, height, 1.5f), Vector3.back);
        }


        // ── Края стены ───────────────────────────────────────────────────

        [Test]
        public void CornerNearOffGridEdge_SnapsToEdge_AndGridContinuesFromEdge()
        {
            // Стена с x = 0,3 до 3,75: края не на сетке в 1 м.
            testScene.CreateWall(new Vector3(0.3f, 0f, 0f), new Vector3(3.75f, 0f, 0f));
            Physics.SyncTransforms();
            var rectangle = new LiveBuilderSurfaceRectangle();

            Assert.IsTrue(rectangle.TryBegin(RayToFrontFace(0.6f)));
            Assert.AreEqual(Origin.x + 0.3f, rectangle.Origin.x, 0.001f, "у края — к краю, а не к сетке мира");
            Assert.IsTrue(rectangle.IsOriginOnEdge);
            rectangle.Cancel();

            Assert.IsTrue(rectangle.TryBegin(RayToFrontFace(1.45f)));
            Assert.AreEqual(Origin.x + 1.3f, rectangle.Origin.x, 0.001f, "сетка — от края стены: 0,3 → 1,3");
            Assert.IsFalse(rectangle.IsOriginOnEdge);
        }


        [Test]
        public void DraggedCornerNearEdge_SnapsToEdge_OtherwiseWholeCells()
        {
            testScene.CreateWall(new Vector3(0.3f, 0f, 0f), new Vector3(3.75f, 0f, 0f));
            Physics.SyncTransforms();
            var rectangle = new LiveBuilderSurfaceRectangle();
            Assert.IsTrue(rectangle.TryBegin(RayToFrontFace(0.4f)));

            rectangle.Update(RayToFrontFace(3.6f));
            Assert.AreEqual(3.45f, rectangle.End.x, 0.001f, "у дальнего края — вровень с ним");
            Assert.IsTrue(rectangle.IsEndOnEdge);

            rectangle.Update(RayToFrontFace(2.2f));
            Assert.AreEqual(2f, rectangle.End.x, 0.001f, "посередине — целые клетки от первого угла");
        }


        [Test]
        public void ClickOpeningOnGrid_NearEndOnGridFromWallEdge()
        {
            testScene.CreateWall(new Vector3(0.3f, 0f, 0f), new Vector3(6.3f, 0f, 0f));
            Physics.SyncTransforms();
            LiveBuilderCutterSettings.IsSnappedToGrid = true;
            var placer = new LiveBuilderOpeningPlacer();
            var width = LiveBuilderOpeningPresets.Selected.Width;
            var mouseX = 0.3f + width / 2f + 0.8f;

            placer.UpdateHover(LiveBuilderSceneHit.Find(RayToFrontFace(mouseX)));

            var leftEnd = mouseX - width / 2f;
            var expectedLeft = 0.3f + Mathf.Round(leftEnd - 0.3f);
            var cutterBottom = (Vector3)typeof(LiveBuilderOpeningPlacer)
                .GetField("cutterLocalBottom", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(placer);

            Assert.IsTrue(placer.HasTarget);
            Assert.AreEqual(expectedLeft + width / 2f, cutterBottom.x, 0.001f, "левый край — через целые клетки от края стены 0,3");
        }

        // Луч к передней стороне стены вдоль X (z = −0,15), на высоте 1,5.
        private static Ray RayToFrontFace(float x)
        {
            return new Ray(Origin + new Vector3(x, 1.5f, -5f), Vector3.forward);
        }
    }
}
