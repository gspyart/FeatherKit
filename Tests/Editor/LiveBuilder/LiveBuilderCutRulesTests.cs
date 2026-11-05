using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Кого режут вырезающие коробки и вырезы: коробка — только стены и здания выше неё в иерархии (здание-предок — всегда);
    /// что режет — всё, только перекрытия или только стены; вырез внутри постройки — только свою группу, «всё вокруг» — и соседей.
    /// </summary>
    public class LiveBuilderCutRulesTests
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


        // ── Порядок в иерархии ───────────────────────────────────────────

        [Test]
        public void CutBox_CutsOnlyWallAboveItInHierarchy()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var wall = testScene.CreateWall(model, Vector3.zero, new Vector3(10f, 0f, 0f));
            var box = BoxModelFactory.CreatePart(model, new Vector3(4f, 0.5f, -1f), new Vector3(6f, 2.5f, 1f), BoxModelPartMode.Cut);
            BoxModelRebuilder.Rebuild(model);
            SplineWallRebuilder.Rebuild(wall);
            var signatureWithHole = wall.BuiltSignature;

            Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(5f, 1.5f, -2f), Vector3.forward, 4f), "коробка ниже стены — дыра");

            box.transform.SetSiblingIndex(0);
            BoxModelRebuilder.ForgetCachedResults();
            SplineWallRebuilder.Rebuild(wall);

            Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(5f, 1.5f, -2f), Vector3.forward, 4f), "коробка выше стены — стена цела");
            Assert.AreNotEqual(signatureWithHole, wall.BuiltSignature, "порядок — в подписи: переставил — пересборка");
        }


        // Коробка в интерьере здания: здание — её предок, перекрытие прорезано; перегородка после коробки — цела.
        [Test]
        public void CutBoxInInterior_CutsBuildingSlab_ButNotPartitionBelowIt()
        {
            var setup = CreateBuildingWithCutBox(LiveBuilderCutTargets.Everything, isPartitionFirst: false);

            Assert.IsTrue(IsSlabOpen(setup.Building), "перекрытие здания-предка прорезано");
            Assert.IsFalse(IsPartitionOpen(setup.Building), "перегородка ниже коробки — цела");
        }


        // ── Что режет ────────────────────────────────────────────────────

        [Test]
        public void SlabsOnlyCutBox_CutsSlab_ButNotPartition()
        {
            var setup = CreateBuildingWithCutBox(LiveBuilderCutTargets.SlabsOnly, isPartitionFirst: true);

            Assert.IsTrue(IsSlabOpen(setup.Building), "перекрытие прорезано");
            Assert.IsFalse(IsPartitionOpen(setup.Building), "перегородка цела");
        }


        [Test]
        public void WallsOnlyCutBox_CutsPartition_ButNotSlab()
        {
            var setup = CreateBuildingWithCutBox(LiveBuilderCutTargets.WallsOnly, isPartitionFirst: true);

            Assert.IsFalse(IsSlabOpen(setup.Building), "перекрытие цело");
            Assert.IsTrue(IsPartitionOpen(setup.Building), "перегородка прорезана");
        }


        [Test]
        public void SlabsOnlyCutter_CutsOnlyFloorParts()
        {
            var building = testScene.CreateBuilding(2, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var firstFloor = building.FirstFloorWorldHeight;

            // Поперёк стены x = 0 и над краем перекрытия второго этажа: глубина по X.
            var cutter = BoxModelFactory.CreateCutter(null, new Vector3(Origin.x, firstFloor + 2f, Origin.z + 4f), Quaternion.LookRotation(Vector3.right),
                new Vector2(2f, 4f), 3f);
            cutter.CutTargets = LiveBuilderCutTargets.SlabsOnly;
            SplineBuildingRebuilder.Rebuild(building);

            Assert.IsTrue(LiveBuilderTestScene.IsOpen(new Vector3(Origin.x + 0.8f, firstFloor + 4.5f, Origin.z + 4f), Vector3.down, 1f), "перекрытие прорезано");
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(new Vector3(Origin.x - 2f, firstFloor + 3f, Origin.z + 4f), Vector3.right, 3f), "стена здания цела");
        }


        // Здание 8 × 8 в два этажа внутри объекта, в интерьере — вырезающая коробка поперёк перекрытия второго этажа
        // и перегородка x = 4 первого этажа, которую коробка задевает сверху. Порядок в интерьере — перегородка первой или коробка.
        private (SplineBuilding Building, BoxModelPart Box, SplineWall Partition) CreateBuildingWithCutBox(LiveBuilderCutTargets targets, bool isPartitionFirst)
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = testScene.CreateBuilding(model, 2, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var floor = building.FirstFloorWorldHeight - Origin.y;
            var interior = BoxModelFactory.InteriorOf(building);

            var partition = testScene.CreateWall(model, new Vector3(4f, floor, 1f), new Vector3(4f, floor, 7f));
            var box = BoxModelFactory.CreatePart(model, new Vector3(3f, floor + 2f, 3f), new Vector3(5f, floor + 6f, 5f), BoxModelPartMode.Cut);
            box.CutTargets = targets;

            var first = isPartitionFirst ? partition.transform : box.transform;
            var second = isPartitionFirst ? box.transform : partition.transform;
            first.SetParent(interior, true);
            second.SetParent(interior, true);

            BoxModelRebuilder.Rebuild(model);
            SplineBuildingRebuilder.Rebuild(building);
            SplineWallRebuilder.Rebuild(partition);
            return (building, box, partition);
        }

        // Сквозь перекрытие второго этажа сбоку от перегородки.
        private static bool IsSlabOpen(SplineBuilding building)
        {
            var floor = building.FirstFloorWorldHeight;
            return LiveBuilderTestScene.IsOpen(new Vector3(Origin.x + 3.5f, floor + 4.5f, Origin.z + 3.5f), Vector3.down, 1f);
        }

        // Сквозь перегородку x = 4 на высоте 2,5 над полом — там, где её задевает коробка.
        private static bool IsPartitionOpen(SplineBuilding building)
        {
            var floor = building.FirstFloorWorldHeight;
            return LiveBuilderTestScene.IsOpen(new Vector3(Origin.x + 2f, floor + 2.5f, Origin.z + 4f), Vector3.right, 4f);
        }


        // ── Где режет вырез ──────────────────────────────────────────────

        // Стены A вдоль X и B вдоль Z крестом; вырез на перекрестье задевает обе.
        [Test]
        public void CutterScope_OwnGroupInsideWall_EverythingOrAtRootCutsNeighbour()
        {
            var wallA = testScene.CreateWall(Vector3.zero, new Vector3(10f, 0f, 0f));
            var wallB = testScene.CreateWall(new Vector3(5f, 0f, -5f), new Vector3(5f, 0f, 5f));
            var cutter = BoxModelFactory.CreateCutter(wallA.transform, Origin + new Vector3(5f, 0.5f, 0f), Quaternion.identity, new Vector2(1.5f, 2f), 2f);
            RebuildWalls(wallA, wallB);

            Assert.IsTrue(IsWallAOpen(), "«авто» в стене A — режет A");
            Assert.IsFalse(IsWallBOpen(), "и не режет соседнюю B");

            cutter.Scope = GeometryCutterScope.Everything;
            RebuildWalls(wallA, wallB);

            Assert.IsTrue(IsWallAOpen() && IsWallBOpen(), "«всё вокруг» — режет обе");

            cutter.Scope = GeometryCutterScope.Automatic;
            cutter.transform.SetParent(null, true);
            RebuildWalls(wallA, wallB);

            Assert.IsTrue(IsWallAOpen() && IsWallBOpen(), "«авто» в корне — режет обе");
        }

        private static void RebuildWalls(params SplineWall[] walls)
        {
            foreach (var wall in walls)
                SplineWallRebuilder.Rebuild(wall);
        }

        private static bool IsWallAOpen()
        {
            return LiveBuilderTestScene.IsOpen(Origin + new Vector3(5.5f, 1.5f, -2f), Vector3.forward, 4f);
        }

        private static bool IsWallBOpen()
        {
            return LiveBuilderTestScene.IsOpen(Origin + new Vector3(3f, 1.5f, 0.6f), Vector3.right, 4f);
        }
    }
}
