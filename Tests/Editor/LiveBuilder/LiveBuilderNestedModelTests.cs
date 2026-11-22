using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Объект в объекте: вложенный объект из коробок в этаже здания с повтором до верха повторяет все свои коробки на каждом этаже одним мешем
    /// («И этажи выше» у коробки внутри копий не перемножает), а вырез в нём («всё вокруг», «только перекрытия») пробивает перекрытия здания — шахта.
    /// Лестница с «и на последнем этаже» не теряет его при сверке; объект на крыше едет за ней.
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
        public void NestedModelRepeatedToTop_RepeatsAllBoxesOnEveryStorey()
        {
            var building = CreateBuilding(null, 3);
            var shaft = CreateShaft(building);

            Assert.AreEqual(3, BoxModelRebuilder.GetSolverBoxes(shaft).Count, "одна коробка — на каждом из трёх этажей");

            var bounds = shaft.GetComponent<MeshRenderer>().bounds;
            Assert.AreEqual(2f * StoreyHeight + 3f, bounds.size.y, 0.01f, "один меш на все три этажа");
        }


        // «И этажи выше» у коробки в уже повторённом объекте копий не перемножает: этажей три — коробок три, а не девять.
        [Test]
        public void RepeatedBoxInsideRepeatedModel_DoesNotMultiplyCopies()
        {
            var building = CreateBuilding(null, 3);
            var shaft = CreateShaft(building);
            var box = BoxModelRebuilder.CollectParts(shaft)[0];
            SplineBuildingInteriorPlacement.RepeatOnStoreysAbove(box.transform, building, false);
            BoxModelRebuilder.Rebuild(shaft);

            Assert.AreEqual(3, BoxModelRebuilder.GetSolverBoxes(shaft).Count, "по коробке на этаж");
            Assert.IsTrue(SplineBuildingInteriorPlacement.IsRepeatedOnStoreys(box.transform, building), "коробке этажи уже повторяет объект");
            Assert.IsNull(box.GetComponent<LiveBuilderArray>(), "своего повтора у коробки нет");
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
            cutter.Scope = GeometryCutterScope.EverythingItTouches;
            cutter.CutTargets = LiveBuilderCutTargets.SlabsOnly;
            SplineBuildingRebuilder.Rebuild(building);

            Assert.IsTrue(LiveBuilderTestScene.IsOpen(new Vector3(Origin.x + 6f, floor + StoreyHeight + 0.5f, Origin.z + 6f), Vector3.down, 1f),
                "перекрытие второго этажа прорезано");
            Assert.IsTrue(LiveBuilderTestScene.IsOpen(new Vector3(Origin.x + 6f, floor + 2f * StoreyHeight + 0.5f, Origin.z + 6f), Vector3.down, 1f),
                "и третьего");
        }

        // Объект из коробок в первом этаже здания: коробка 2 × 3 × 2, «И этажи выше» — повтор до верха.
        private static BoxModel CreateShaft(SplineBuilding building)
        {
            var shaft = BoxModelFactory.CreateModel(new Vector3(Origin.x + 2f, building.FirstFloorWorldHeight, Origin.z + 2f));
            BoxModelFactory.CreatePart(shaft, Vector3.zero, new Vector3(2f, 3f, 2f), BoxModelPartMode.Add);
            SplineBuildingInteriorPlacement.PutOnStorey(shaft.transform, building, true, false, "Create Shaft");

            BoxModelRebuilder.Rebuild(shaft);
            return shaft;
        }


        // ── Лестница на последнем этаже ──────────────────────────────────

        [Test]
        public void StairsOnTopStorey_SurvivesStoreySync()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = CreateBuilding(model, 3);
            var stairs = BoxModelFactory.CreateStairs(model, new Vector3(2f, building.FirstFloorWorldHeight - Origin.y, 2f), BoxModelStairsDirection.PositiveX,
                1f, StoreyHeight, 0.3f, false);
            SplineBuildingInteriorPlacement.PutOnStorey(stairs.transform, building, true, true, "Create Stairs");
            var array = stairs.GetComponent<LiveBuilderArray>();

            Assert.AreEqual(2, LiveBuilderArrayCopyPoses.CopiesOf(stairs.transform).Count, "новая лестница — без верхнего этажа");

            array.SkipY = "";
            SplineBuildingStoreySync.SyncAll();

            Assert.AreEqual("", array.SkipY, "сверка не возвращает «без верхнего этажа»");
            Assert.AreEqual(3, LiveBuilderArrayCopyPoses.CopiesOf(stairs.transform).Count, "копия и на верхнем этаже");
        }


        // ── На крыше ─────────────────────────────────────────────────────

        // Чердак — отдельный объект на крыше: находится как «на крыше здания», встаёт в «Roof» и едет за ней,
        // когда добавляют этажи и меняют их высоту; спрятанным не считается и по этажам не повторяется.
        [Test]
        public void ModelOnRoof_InRoof_FollowsStoreyCountAndHeight()
        {
            var building = CreateBuilding(null, 2);
            var roofTop = building.FirstFloorWorldHeight + 2 * StoreyHeight;
            var attic = BoxModelFactory.CreateModel(new Vector3(Origin.x + 1f, roofTop, Origin.z + 1f));
            BoxModelFactory.CreatePart(attic, Vector3.zero, new Vector3(6f, 3f, 6f), BoxModelPartMode.Add);
            BoxModelRebuilder.Rebuild(attic);

            Assert.AreEqual(building, LiveBuilderBuildingContext.BuildingWithRoofUnder(attic.gameObject), "стоит на крыше этого здания");

            SplineBuildingInteriorPlacement.PutOnRoof(attic.transform, building, "Move To Roof");
            Assert.AreEqual(0f, attic.transform.localPosition.y, 0.001f, "низ — ровно на крыше");

            building.StoreyCount = 3;
            SplineBuildingStoreySync.SyncAll();
            Assert.AreEqual(roofTop + StoreyHeight, attic.transform.position.y, 0.001f, "добавили этаж — поднялся с крышей");
            Assert.IsTrue(attic.gameObject.activeInHierarchy, "не спрятан: этажа «над верхним» и не бывает");
            Assert.AreEqual(1, LiveBuilderArrayCopyPoses.CopiesOf(attic.transform).Count, "по этажам не повторяется");

            building.StoreyHeight = 5f;
            SplineBuildingStoreySync.SyncAll();
            Assert.AreEqual(building.FirstFloorWorldHeight + 3 * 5f, attic.transform.position.y, 0.001f, "этажи выше — тоже за крышей");
            Assert.IsFalse(LiveBuilderLevelCheck.FindIssues().Exists(issue => issue.Target == attic.gameObject), "проверка уровня не ругается");
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Здание 8 × 8, этажи по 4 м.
        private SplineBuilding CreateBuilding(BoxModel parentModel, int storeyCount)
        {
            return testScene.CreateBuilding(parentModel, storeyCount, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
        }
    }
}
