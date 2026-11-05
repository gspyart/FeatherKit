using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Перенос и копия стрелками Live Builder (<see cref="LiveBuilderMoveHandle"/>): сдвиг целыми клетками сетки, копия сразу за оригиналом и только
    /// с первого целого шага; выбранные точки стены едут стрелками вместе, и вверх (<see cref="LiveBuilderContourMoveHandle"/>); подпись здания — в его осях и только по задевающим его вырезающим коробкам: здание едет со своей дырой без пересборки,
    /// копия здания поодаль оригинал не трогает. И здание под мышью: платформа перед зданием закрывает пол за ней.
    /// </summary>
    public class LiveBuilderMoveCopyTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private Object[] selectionBefore;


        [SetUp]
        public void OpenScene()
        {
            selectionBefore = Selection.objects;
            testScene = new LiveBuilderTestScene();
        }


        [TearDown]
        public void CloseScene()
        {
            LiveBuilderMoveHandle.EndDrag();
            testScene.Dispose();
            Selection.objects = selectionBefore;
        }


        // ── Перенос и копия ──────────────────────────────────────────────

        [Test]
        public void Drag_MovesByWholeGridSteps()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var box = BoxModelFactory.CreatePart(model, Vector3.zero, Vector3.one, BoxModelPartMode.Add);
            var start = box.transform.position;
            var step = LiveBuilderGrid.StepOn(0);

            LiveBuilderMoveHandle.BeginDrag(new[] { box.transform }, start, Quaternion.identity, false);
            LiveBuilderMoveHandle.DragTo(start + new Vector3(step * 2.3f, 0f, 0f));
            LiveBuilderMoveHandle.EndDrag();

            Assert.That(Vector3.Distance(box.transform.position, start + new Vector3(step * 2f, 0f, 0f)), Is.LessThan(0.0001f), "на две целые клетки");
        }


        [Test]
        public void CopyDrag_KeepsOriginalAndPutsCopyRightAfterIt()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var original = BoxModelFactory.CreatePart(model, Vector3.zero, Vector3.one, BoxModelPartMode.Add);
            BoxModelFactory.CreatePart(model, new Vector3(5f, 0f, 0f), new Vector3(6f, 1f, 1f), BoxModelPartMode.Add);
            var start = original.transform.position;
            var step = LiveBuilderGrid.StepOn(0);

            LiveBuilderMoveHandle.BeginDrag(new[] { original.transform }, start, Quaternion.identity, true);
            LiveBuilderMoveHandle.DragTo(start + new Vector3(step * 0.3f, 0f, 0f));
            Assert.AreEqual(2, model.GetComponentsInChildren<BoxModelPart>().Length, "меньше клетки — копии ещё нет");

            LiveBuilderMoveHandle.DragTo(start + new Vector3(step * 3f, 0f, 0f));
            LiveBuilderMoveHandle.EndDrag();

            var copy = Selection.activeGameObject;
            Assert.AreEqual(3, model.GetComponentsInChildren<BoxModelPart>().Length, "копия — третья коробка");
            Assert.That(Vector3.Distance(original.transform.position, start), Is.LessThan(0.0001f), "оригинал стоит");
            Assert.AreEqual(original.transform.GetSiblingIndex() + 1, copy.transform.GetSiblingIndex(), "копия — сразу за оригиналом");
            Assert.That(Vector3.Distance(copy.transform.position, start + new Vector3(step * 3f, 0f, 0f)), Is.LessThan(0.0001f), "копия — на три клетки дальше");
        }


        // ── Точки контура ────────────────────────────────────────────────

        [Test]
        public void SelectedWallPoints_MoveTogetherByGridSteps()
        {
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(4f, 0f, 0f), new Vector3(8f, 0f, 0f));
            var container = wall.GetComponent<SplineContainer>();
            var step = LiveBuilderGrid.StepOn(2);
            var stepUp = LiveBuilderGrid.StepOn(1);

            try
            {
                LiveBuilderContourSelection.SetOwner(wall);
                LiveBuilderContourSelection.Add(0, 1);
                LiveBuilderContourSelection.Add(0, 2);

                var handle = new LiveBuilderContourMoveHandle();
                var center = Origin + new Vector3(6f, 0f, 0f);
                handle.BeginDrag(wall, container, center, Quaternion.identity, true);
                handle.DragTo(wall, container, center + new Vector3(0f, stepUp * 2.2f, step * 3.4f));
                handle.EndDrag();
            }
            finally
            {
                LiveBuilderContourSelection.SetOwner(null);
            }

            var spline = container.Spline;
            var shift = new Vector3(0f, stepUp * 2f, step * 3f);
            Assert.That(Vector3.Distance(spline[0].Position, Vector3.zero), Is.LessThan(0.0001f), "невыбранная точка стоит");
            Assert.That(Vector3.Distance(spline[1].Position, new Vector3(4f, 0f, 0f) + shift), Is.LessThan(0.0001f), "выбранная — на целые клетки, и вверх");
            Assert.That(Vector3.Distance(spline[2].Position, new Vector3(8f, 0f, 0f) + shift), Is.LessThan(0.0001f), "вторая выбранная — так же");
        }


        // ── Подпись здания ───────────────────────────────────────────────

        [Test]
        public void BuildingMovedWithItsSlabHole_IsNotRebuilt()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = CreateBuildingWithSlabHole(model);
            var signature = building.BuiltSignature;

            building.transform.position += new Vector3(5f, 0f, 3f);
            BoxModelRebuilder.Rebuild(model);
            SplineBuildingRebuilder.RebuildChangedBuildings();

            Assert.AreEqual(signature, building.BuiltSignature, "здание переехало вместе со своей дырой — подпись та же");
        }


        [Test]
        public void BuildingCopyFarAway_KeepsOriginalSignature()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = CreateBuildingWithSlabHole(model);
            var signature = building.BuiltSignature;

            var copy = Object.Instantiate(building.gameObject, building.transform.parent);
            copy.transform.SetSiblingIndex(building.transform.GetSiblingIndex() + 1);
            copy.transform.position += new Vector3(30f, 0f, 0f);
            BoxModelRebuilder.Rebuild(model);
            SplineBuildingRebuilder.RebuildChangedBuildings();

            Assert.AreEqual(signature, building.BuiltSignature, "дыра копии далеко — оригинал не пересобирается");
        }

        // Здание в два этажа в объекте из коробок и вырезающая коробка в его интерьере сквозь перекрытие между этажами.
        private SplineBuilding CreateBuildingWithSlabHole(BoxModel model)
        {
            var building = testScene.CreateBuilding(model, 2, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var floor = building.FirstFloorWorldHeight - Origin.y;
            var hole = BoxModelFactory.CreatePart(model, new Vector3(2f, floor + 2f, 2f), new Vector3(4f, floor + 5f, 4f), BoxModelPartMode.Cut);
            hole.transform.SetParent(BoxModelFactory.InteriorOf(building), true);

            BoxModelRebuilder.Rebuild(model);
            SplineBuildingRebuilder.Rebuild(building);
            return building;
        }


        // ── Здание под мышью ─────────────────────────────────────────────

        // Мышь на платформе перед зданием, а пол этажа по тому же лучу — уже в плане здания: строишь на платформе, не в здании.
        [Test]
        public void PlatformInFrontOfBuilding_HidesBuildingFloorBehindIt()
        {
            var floor = LiveBuilderStoreys.GroundLevel + LiveBuilderStoreys.CurrentStorey * LiveBuilderStoreys.StoreyHeight;
            var building = testScene.CreateBuilding(1, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            building.transform.position = new Vector3(Origin.x, floor, Origin.z);
            SplineBuildingRebuilder.Rebuild(building);

            var platform = BoxModelFactory.CreateModel(new Vector3(Origin.x - 4f, floor, Origin.z + 2f));
            BoxModelFactory.CreatePart(platform, Vector3.zero, new Vector3(3f, 1f, 4f), BoxModelPartMode.Add);
            BoxModelRebuilder.Rebuild(platform);

            var onPlatform = new Vector3(Origin.x - 2f, floor + 1f, Origin.z + 4f);
            var direction = new Vector3(3f, -1f, 0f).normalized;
            var ray = new Ray(onPlatform - direction * 10f, direction);

            Assert.IsFalse(LiveBuilderBuildingContext.TryFind(ray, out _), "пол за платформой — не пол здания");

            var intoBuilding = new Ray(new Vector3(Origin.x + 4f, floor + 10f, Origin.z + 4f), Vector3.down);
            Assert.IsTrue(LiveBuilderBuildingContext.TryFind(intoBuilding, out _), "сверху в здание — его этаж");
        }
    }
}
