using FeatherKit.EditorTools;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Перенос пивота у построек Live Builder общим инструментом «Пивот»: пивот встаёт куда просили, а здание, стена и объект из коробок
    /// после пересборки — там же в мире, где были; поставленное внутрь не едет; Ctrl+Z возвращает всё одним шагом.
    /// </summary>
    public class LiveBuilderPivotMoverTests
    {
        private const float Tolerance = 0.002f;

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


        // ── Здание ───────────────────────────────────────────────────────

        [Test]
        public void Building_PivotToBottomCenter_StaysInPlace()
        {
            var building = testScene.CreateBuilding(2, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 6f), new Vector3(0f, 0f, 6f));
            var cutter = BoxModelFactory.CreateCutter(building.transform, Origin + new Vector3(4f, 1f, -0.15f), Quaternion.identity, new Vector2(1f, 1f), 0.5f);
            var boundsBefore = WorldBoundsOf(building.transform);
            var cutterBefore = cutter.transform.position;

            PivotMover.MoveToChildrenCenter(new[] { building.transform }, ChildrenCenterPoint.BottomCenter);
            SplineBuildingRebuilder.Rebuild(building);

            AssertNear(BottomCenterOf(boundsBefore), building.transform.position, "пивот — в центре низа");
            AssertSameBounds(boundsBefore, WorldBoundsOf(building.transform));
            AssertNear(cutterBefore, cutter.transform.position, "вырез в здании не уехал");
        }


        [Test]
        public void Building_UndoAfterPivotMove_RestoresPivotAndPoints()
        {
            var building = testScene.CreateBuilding(1, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 6f), new Vector3(0f, 0f, 6f));
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();

            // Откат до своего шага, а не PerformUndo: тот снимает последнюю запись истории, а ею может оказаться чужая — смена выделения.
            PivotMover.MoveToChildrenCenter(new[] { building.transform }, ChildrenCenterPoint.BottomCenter);
            Undo.RevertAllDownToGroup(undoGroup);

            AssertNear(Origin, building.transform.position, "пивот вернулся");
            AssertNear(Vector3.zero, (Vector3)building.GetComponent<SplineContainer>().Spline[0].Position, "точка контура вернулась");
        }


        // ── Стена по линии ───────────────────────────────────────────────

        [Test]
        public void Wall_PivotToBottomCenter_StaysInPlace()
        {
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(6f, 0f, 0f), new Vector3(6f, 0f, 4f));
            var boundsBefore = WorldBoundsOf(wall.transform);

            PivotMover.MoveToChildrenCenter(new[] { wall.transform }, ChildrenCenterPoint.BottomCenter);
            SplineWallRebuilder.Rebuild(wall);

            AssertNear(BottomCenterOf(boundsBefore), wall.transform.position, "пивот — в центре низа");
            AssertSameBounds(boundsBefore, WorldBoundsOf(wall.transform));
        }


        // ── Объект из коробок ────────────────────────────────────────────

        [Test]
        public void BoxModel_PivotToCenter_StaysInPlace()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var part = BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 1f, 3f), BoxModelPartMode.Add);
            BoxModelRebuilder.Rebuild(model);
            var boundsBefore = WorldBoundsOf(model.transform);
            var partBefore = part.transform.position;

            PivotMover.MoveToChildrenCenter(new[] { model.transform }, ChildrenCenterPoint.Center);
            BoxModelRebuilder.Rebuild(model);

            AssertNear(Origin + new Vector3(1f, 0.5f, 1.5f), model.transform.position, "пивот — в центре коробки");
            AssertSameBounds(boundsBefore, WorldBoundsOf(model.transform));
            AssertNear(partBefore, part.transform.position, "коробка не уехала");
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static Bounds WorldBoundsOf(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            Assert.IsNotEmpty(renderers, "у постройки нет мешей");

            var bounds = renderers[0].bounds;

            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);

            return bounds;
        }

        private static Vector3 BottomCenterOf(Bounds bounds)
        {
            return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        private static void AssertSameBounds(Bounds expected, Bounds actual)
        {
            AssertNear(expected.min, actual.min, "низ-угол габаритов на месте");
            AssertNear(expected.max, actual.max, "верх-угол габаритов на месте");
        }

        private static void AssertNear(Vector3 expected, Vector3 actual, string message)
        {
            Assert.Less(Vector3.Distance(expected, actual), Tolerance, message + ": ждали " + expected.ToString("F3") + ", стало " + actual.ToString("F3"));
        }
    }
}
