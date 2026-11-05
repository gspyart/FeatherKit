using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Объект в объекте: вложенный объект из коробок в здании, привязанный к этажам целиком, повторяет все свои коробки на каждом этаже одним мешем
    /// (привязанная внутри него коробка копий не перемножает),
    /// а вырез в нём («всё вокруг», «только перекрытия») пробивает перекрытия здания — шахта. Лестница с «и на последнем этаже» не теряет его при сверке.
    /// </summary>
    public class LiveBuilderNestedModelTests
    {
        private const float StoreyHeight = 4f;

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


        // ── Вложенный объект ─────────────────────────────────────────────

        [Test]
        public void NestedModelLinkedToTop_RepeatsAllBoxesOnEveryStorey()
        {
            var building = CreateBuilding(null, 3);
            var shaft = CreateShaft(building);

            Assert.AreEqual(3, BoxModelRebuilder.GetSolverBoxes(shaft).Count, "одна коробка — на каждом из трёх этажей");

            var bounds = shaft.GetComponent<MeshRenderer>().bounds;
            Assert.AreEqual(2f * StoreyHeight + 3f, bounds.size.y, 0.01f, "один меш на все три этажа");
        }


        // Коробка в уже привязанном объекте, привязанная сама, копий не перемножает: этажей три — коробок три, а не девять.
        [Test]
        public void LinkedBoxInsideLinkedModel_DoesNotMultiplyCopies()
        {
            var building = CreateBuilding(null, 3);
            var shaft = CreateShaft(building);
            var box = BoxModelRebuilder.CollectParts(shaft)[0];
            SplineBuildingLinkSync.Attach(box.gameObject, building, true);
            BoxModelRebuilder.Rebuild(shaft);

            Assert.AreEqual(3, BoxModelRebuilder.GetSolverBoxes(shaft).Count, "по коробке на этаж");
            Assert.IsTrue(BoxModelShapeCollector.IsInsideStoreyCopies(shaft, box.transform.parent), "коробке этажи уже повторяет объект");
            Assert.IsTrue(BoxModelShapeCollector.HasStoreyLinksInside(shaft.transform), "в объекте есть привязанная коробка");
        }


        [Test]
        public void CutterInsideNestedModel_EverythingSlabsOnly_CutsBuildingFloors()
        {
            var building = CreateBuilding(null, 3);
            var shaft = CreateShaft(building);
            var floor = building.FirstFloorWorldHeight;

            // Высокий вырез сбоку от коробок шахты — через перекрытия второго и третьего этажей.
            var cutter = BoxModelFactory.CreateCutter(shaft.transform, new Vector3(Origin.x + 6f, floor + 1f, Origin.z + 6f), Quaternion.identity,
                new Vector2(1f, 10f), 1f);
            cutter.Scope = GeometryCutterScope.Everything;
            cutter.CutTargets = LiveBuilderCutTargets.SlabsOnly;
            SplineBuildingRebuilder.Rebuild(building);

            Assert.IsTrue(LiveBuilderTestScene.IsOpen(new Vector3(Origin.x + 6f, floor + StoreyHeight + 0.5f, Origin.z + 6f), Vector3.down, 1f),
                "перекрытие второго этажа прорезано");
            Assert.IsTrue(LiveBuilderTestScene.IsOpen(new Vector3(Origin.x + 6f, floor + 2f * StoreyHeight + 0.5f, Origin.z + 6f), Vector3.down, 1f),
                "и третьего");
        }

        // Объект из коробок в интерьере здания, на полу первого этажа: коробка 2 × 3 × 2, привязан «до верха».
        private static BoxModel CreateShaft(SplineBuilding building)
        {
            var shaft = BoxModelFactory.CreateModel(new Vector3(Origin.x + 2f, building.FirstFloorWorldHeight, Origin.z + 2f));
            BoxModelFactory.CreatePart(shaft, Vector3.zero, new Vector3(2f, 3f, 2f), BoxModelPartMode.Add);
            shaft.transform.SetParent(BoxModelFactory.InteriorOf(building), true);
            SplineBuildingLinkSync.Attach(shaft.gameObject, building, true);

            BoxModelRebuilder.Rebuild(shaft);
            return shaft;
        }


        // ── Лестница на последнем этаже ──────────────────────────────────

        [Test]
        public void StairsOnTopStorey_SurvivesLinkSync()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = CreateBuilding(model, 3);
            var stairs = BoxModelFactory.CreateStairs(model, new Vector3(2f, building.FirstFloorWorldHeight - Origin.y, 2f), BoxModelStairsDirection.PositiveX,
                1f, StoreyHeight, 0.3f, false);
            stairs.transform.SetParent(BoxModelFactory.InteriorOf(building), true);
            var link = SplineBuildingLinkSync.Attach(stairs.gameObject, building, true, true);

            Assert.AreEqual(2, link.CopiesFrom(link.Storey), "новая лестница — без верхнего этажа");

            link.SkipsTopStorey = false;
            SplineBuildingLinkSync.Sync(link);

            Assert.IsFalse(link.SkipsTopStorey, "сверка не возвращает «без верхнего этажа»");
            Assert.AreEqual(3, link.CopiesFrom(link.Storey), "копия и на верхнем этаже");
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Здание 8 × 8, этажи по 4 м.
        private SplineBuilding CreateBuilding(BoxModel parentModel, int storeyCount)
        {
            return testScene.CreateBuilding(parentModel, storeyCount, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
        }
    }
}
