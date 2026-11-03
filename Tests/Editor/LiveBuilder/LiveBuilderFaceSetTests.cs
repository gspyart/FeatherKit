using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Набор граней у выделенных коробок (Shift+клик по квадратам): потянул одну из набранных — все уходят на то же расстояние наружу,
    /// каждая поперёк себя. И стену по линии можно замкнуть и разомкнуть, а замкнуть — только из трёх точек и больше.
    /// </summary>
    public class LiveBuilderFaceSetTests
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
            BoxModelFaceSelection.Clear();
            BoxModelFaceMoveHandle.EndDragIfReleased();
            Selection.activeObject = null;
            testScene.Dispose();
        }


        // ── Набор граней ─────────────────────────────────────────────────

        [Test]
        public void CollectedFaces_MoveTogetherOutward()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var first = BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 2f, 2f), BoxModelPartMode.Add);
            var second = BoxModelFactory.CreatePart(model, new Vector3(4f, 0f, 0f), new Vector3(6f, 2f, 2f), BoxModelPartMode.Add);
            Selection.objects = new Object[] { first.gameObject, second.gameObject };

            // Верх первой и правый бок второй; тянут верх первой на метр вверх.
            BoxModelFaceSelection.Toggle(new BoxModelPartFace(first, BoxFaces.Index(1, true)));
            BoxModelFaceSelection.Toggle(new BoxModelPartFace(second, BoxFaces.Index(0, true)));
            BoxModelFaceMoveHandle.DragCollectedFaces(new BoxModelPartFace(first, BoxFaces.Index(1, true)), 3f, false);

            Assert.AreEqual(3f, first.Size.y, 0.001f, "верх первой — на метр выше");
            Assert.AreEqual(3f, second.Size.x, 0.001f, "бок второй — на метр наружу");
            Assert.AreEqual(2f, second.Size.y, 0.001f, "остальное у второй не тронуто");
        }


        [Test]
        public void ShiftClickAgain_RemovesFaceFromSet()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var part = BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 2f, 2f), BoxModelPartMode.Add);
            Selection.activeGameObject = part.gameObject;
            var top = new BoxModelPartFace(part, BoxFaces.Index(1, true));

            BoxModelFaceSelection.Toggle(top);
            Assert.IsTrue(BoxModelFaceSelection.Contains(top), "набрана, хоть выделена коробка, а не объект");

            BoxModelFaceSelection.Toggle(top);
            Assert.IsFalse(BoxModelFaceSelection.Contains(top));
        }


        // ── Замкнуть и разомкнуть ────────────────────────────────────────

        [Test]
        public void Wall_ClosesAndOpens()
        {
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 4f));
            var container = wall.GetComponent<SplineContainer>();

            Assert.IsTrue(LiveBuilderContourKeys.CanToggleClosed(wall, container));
            LiveBuilderContourKeys.ToggleClosed(container);
            Assert.IsTrue(container.Spline.Closed);

            LiveBuilderContourKeys.ToggleClosed(container);
            Assert.IsFalse(container.Spline.Closed);
            Assert.AreEqual(3, container.Spline.Count, "точки на месте");

            var shortWall = testScene.CreateWall(new Vector3(10f, 0f, 0f), new Vector3(14f, 0f, 0f));
            Assert.IsFalse(LiveBuilderContourKeys.CanToggleClosed(shortWall, shortWall.GetComponent<SplineContainer>()), "из двух точек не замкнуть");
        }
    }
}
