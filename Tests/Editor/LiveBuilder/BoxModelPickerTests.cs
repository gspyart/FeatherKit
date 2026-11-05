using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Поиск поверхности под мышью: здание, стоящее на объекте из коробок, закрывает его — коробка, нарисованная на полу здания,
    /// встаёт в объект здания на его пол, а не на объект под ним. Вырез, стоящий на самом объекте, не перехватывает клик по его граням;
    /// клик через пустую часть коробки выреза мимо дыры — по грани за ней, сквозь дыру — по вырезу.
    /// Поверхность в дыре выреза не ловится: сквозь дверь — стена за ней, а дно ниши закрывает то, что за стеной.
    /// Выключенный объект из коробок не ловится.
    /// </summary>
    public class BoxModelPickerTests
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


        // ── Здание на объекте из коробок ─────────────────────────────────

        [Test]
        public void BuildingFloorOverPlatform_HidesPlatform_DrawsOnBuildingFloor()
        {
            var platform = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(platform, new Vector3(-2f, -1f, -2f), new Vector3(10f, 0f, 10f), BoxModelPartMode.Add);
            BoxModelRebuilder.Rebuild(platform);

            var buildingModel = BoxModelFactory.CreateModel(Origin);
            var building = BoxModelFactory.CreateBuilding(buildingModel, Origin, Quaternion.identity);
            building.StoreyCount = 2;
            building.StoreyHeight = 4f;
            building.WallType = null;
            var spline = building.GetComponent<SplineContainer>().Spline;

            foreach (var point in new[] { Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f) })
                spline.Add(new BezierKnot((float3)point), TangentMode.Linear);

            spline.Closed = true;
            SplineBuildingRebuilder.Rebuild(building);
            Physics.SyncTransforms();

            var ray = new Ray(Origin + new Vector3(4f, building.FirstFloorWorldHeight - Origin.y + 1.5f, 4f), Vector3.down);

            Assert.IsFalse(BoxModelPicker.TryPickSurface(ray, out _), "пол здания закрывает объект под ним");
            Assert.IsTrue(BoxModelDrawPlaneFinder.TryFind(ray, false, out var plane, out var localPoint));
            Assert.AreSame(buildingModel, plane.Model, "коробка встаёт в объект здания");
            Assert.AreEqual(building.FirstFloorWorldHeight, plane.FrameToWorld.MultiplyPoint3x4(localPoint).y, 0.01f, "на пол здания");
        }


        // ── Вырез на самом объекте ───────────────────────────────────────

        [Test]
        public void CutterOnModelItself_DoesNotHideItsFaces()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 1f, 2f), BoxModelPartMode.Add);
            var cutter = model.gameObject.AddComponent<GeometryCutter>();
            cutter.Width = 4f;
            cutter.Height = 3f;
            cutter.Depth = 4f;
            BoxModelRebuilder.Rebuild(model);
            Physics.SyncTransforms();

            var hit = LiveBuilderSceneHit.Find(new Ray(Origin + new Vector3(1f, 5f, 1f), Vector3.down));

            Assert.IsNotNull(hit.GeometryCutter, "луч проходит через коробку выреза");
            Assert.IsTrue(hit.HasBoxSurface, "грань объекта под лучом");
            Assert.IsFalse(hit.IsGeometryCutterInFront, "клик по своей грани — грань, а не вырез");
        }


        [Test]
        public void CutterOnOtherObject_StillWinsOverFacesInsideIt()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 1f, 2f), BoxModelPartMode.Add);
            BoxModelRebuilder.Rebuild(model);
            var cutterObject = new GameObject("Cutter");
            cutterObject.transform.position = Origin + new Vector3(1f, 0f, 1f);
            var cutter = cutterObject.AddComponent<GeometryCutter>();
            cutter.Width = 4f;
            cutter.Height = 3f;
            cutter.Depth = 4f;
            Physics.SyncTransforms();

            var hit = LiveBuilderSceneHit.Find(new Ray(Origin + new Vector3(1f, 5f, 1f), Vector3.down));

            Assert.IsTrue(hit.IsGeometryCutterInFront, "чужой вырез, в чьей коробке грань, — по-прежнему он");
        }


        [Test]
        public void RayThroughEmptyPartOfCutterBox_PicksFaceBehind_RayThroughHole_PicksCutter()
        {
            var floor = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(floor, new Vector3(-5f, -1f, -5f), new Vector3(5f, 0f, 5f), BoxModelPartMode.Add);
            BoxModelRebuilder.Rebuild(floor);
            var cutterObject = new GameObject("Cutter");
            cutterObject.transform.position = Origin + new Vector3(0f, 2f, 0f);
            var cutter = cutterObject.AddComponent<GeometryCutter>();
            cutter.Width = 2f;
            cutter.Height = 1f;
            cutter.Depth = 2f;
            Physics.SyncTransforms();

            var past = LiveBuilderSceneHit.Find(new Ray(Origin + new Vector3(0.5f, 5f, 0.5f), Vector3.down));
            var through = LiveBuilderSceneHit.Find(new Ray(Origin + new Vector3(0f, 2.5f, 5f), Vector3.back));

            Assert.IsNotNull(past.GeometryCutter, "луч проходит через коробку выреза");
            Assert.IsFalse(past.IsGeometryCutterInFront, "мимо дыры, через пустую часть коробки — грань за ней");
            Assert.IsTrue(through.IsGeometryCutterInFront, "сквозь дыру — вырез");
        }


        // ── Сквозь дыру выреза ───────────────────────────────────────────

        // Клик сквозь дверь в стене-объекте — по стене за ней, а не по грани расчёта в дыре; рядом с дверью — по самой стене.
        [Test]
        public void RayThroughCutterHole_PicksSurfaceBehind()
        {
            var wall = CreateWallModel(0f);
            var backWall = CreateWallModel(3f);
            CreateCutterAt(new Vector3(2f, 0.5f, 0.1f), 0.6f);
            BoxModelRebuilder.Rebuild(wall);
            Physics.SyncTransforms();

            Assert.IsTrue(BoxModelPicker.TryPickSurface(new Ray(Origin + new Vector3(2f, 1.5f, -2f), Vector3.forward), out var throughHole), "сквозь дыру — что-то за ней");
            Assert.AreSame(backWall, throughHole.Model, "сквозь дыру — стена за ней");

            Assert.IsTrue(BoxModelPicker.TryPickSurface(new Ray(Origin + new Vector3(0.5f, 1.5f, -2f), Vector3.forward), out var besideHole));
            Assert.AreSame(wall, besideHole.Model, "рядом с дырой — сама стена");
        }


        // Ниша не насквозь: её дно — меш стены, оно закрывает то, что за стеной.
        [Test]
        public void RayIntoNiche_DoesNotPickSurfaceBehindWall()
        {
            var wall = CreateWallModel(0f);
            CreateWallModel(3f);
            CreateCutterAt(new Vector3(2f, 0.5f, 0f), 0.1f);
            BoxModelRebuilder.Rebuild(wall);
            Physics.SyncTransforms();

            Assert.IsFalse(BoxModelPicker.TryPickSurface(new Ray(Origin + new Vector3(2f, 1.5f, -2f), Vector3.forward), out _), "дно ниши закрывает стену за ней");
        }

        // Стена-объект из коробок 4 × 3 × 0,2 на расстоянии z от Origin.
        private static BoxModel CreateWallModel(float z)
        {
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(model, new Vector3(0f, 0f, z), new Vector3(4f, 3f, z + 0.2f), BoxModelPartMode.Add);
            BoxModelRebuilder.Rebuild(model);
            return model;
        }

        // Вырез-дверь 1 × 2 м отдельным объектом, низ посередине — в точке от Origin.
        private static void CreateCutterAt(Vector3 bottom, float depth)
        {
            var cutterObject = new GameObject("Door Cutter");
            cutterObject.transform.position = Origin + bottom;
            var cutter = cutterObject.AddComponent<GeometryCutter>();
            cutter.Width = 1f;
            cutter.Height = 2f;
            cutter.Depth = depth;
        }


        // ── Выключенный объект ───────────────────────────────────────────

        [Test]
        public void InactiveModel_IsNotPicked()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(4f, 1f, 4f), BoxModelPartMode.Add);
            BoxModelRebuilder.Rebuild(model);
            var ray = new Ray(Origin + new Vector3(2f, 5f, 2f), Vector3.down);
            Assert.IsTrue(BoxModelPicker.TryPickSurface(ray, out _), "включённый ловится");

            model.gameObject.SetActive(false);

            Assert.IsFalse(BoxModelPicker.TryPickSurface(ray, out _), "выключенный — нет");
        }
    }
}
