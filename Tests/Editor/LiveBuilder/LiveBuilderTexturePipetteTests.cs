using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Пипетка текстуры (<see cref="LiveBuilderTexturePipette"/>) лучами, без окна сцены: взять у коробки и положить на коробку и на стену здания —
    /// материал и подгонка, одним Ctrl+Z; у стены с типом материал не ложится (подсказка), а подгонка — да.
    /// </summary>
    public class LiveBuilderTexturePipetteTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private LiveBuilderTextureTestKit kit;


        [SetUp]
        public void OpenScene()
        {
            testScene = new LiveBuilderTestScene();
            kit = new LiveBuilderTextureTestKit();
            LiveBuilderTexturePipette.Clear();
        }


        [TearDown]
        public void CloseScene()
        {
            LiveBuilderTexturePipette.Clear();
            kit.Dispose();
            testScene.Dispose();
        }


        [Test]
        public void Pick_ThenApply_CopiesMaterialAndFit_BoxToBox()
        {
            var brick = kit.CreateMaterial("Brick", Vector2.one);
            var source = CreateBlock(Origin);
            source.TopMaterial = brick;
            source.TextureFits.Set(BoxModelSideSlot.Top, new LiveBuilderTextureFit(new Vector2(0.5f, 0f), Vector2.one, 1));
            BoxModelRebuilder.Rebuild(source.GetComponentInParent<BoxModel>());
            var target = CreateBlock(Origin + new Vector3(0f, 0f, 10f));

            Assert.IsTrue(LiveBuilderTexturePipette.TryPick(DownOnto(Origin + new Vector3(1f, 1f, 1f))), "взяли с верха коробки");
            Assert.AreSame(brick, LiveBuilderTexturePipette.PickedMaterial);

            Undo.IncrementCurrentGroup();
            Assert.IsTrue(LiveBuilderTexturePipette.TryApply(DownOnto(Origin + new Vector3(1f, 1f, 11f))), "положили на верх другой");
            Assert.AreSame(brick, target.TopMaterial, "материал");
            var fit = target.TextureFits.Get(BoxModelSideSlot.Top);
            Assert.IsNotNull(fit, "подгонка");
            Assert.AreEqual(new Vector2(0.5f, 0f), fit.OffsetMeters);
            Assert.AreEqual(1, fit.QuarterTurns);

            Undo.PerformUndo();
            Assert.IsNull(target.TopMaterial, "Ctrl+Z убрал материал");
            Assert.IsTrue(target.TextureFits.IsEmpty, "и подгонку — тем же шагом");
        }


        [Test]
        public void Pick_ThenApply_CopiesMaterialAndFit_BoxToBuildingWall()
        {
            var brick = kit.CreateMaterial("Brick", new Vector2(3f, 3f));
            var source = CreateBlock(Origin);
            source.Material = brick;
            source.TextureFits.Set(BoxModelSideSlot.All, new LiveBuilderTextureFit(new Vector2(1.5f, 0f), Vector2.one, 0));
            BoxModelRebuilder.Rebuild(source.GetComponentInParent<BoxModel>());
            var building = testScene.CreateBuilding(1, new Vector3(0f, 0f, 20f), new Vector3(6f, 0f, 20f), new Vector3(6f, 0f, 26f), new Vector3(0f, 0f, 26f));
            Physics.SyncTransforms();

            Assert.IsTrue(LiveBuilderTexturePipette.TryPick(DownOnto(Origin + new Vector3(1f, 1f, 1f))));
            Assert.IsTrue(LiveBuilderTexturePipette.TryApply(new Ray(Origin + new Vector3(3f, 1.5f, 15f), Vector3.forward)), "положили на стену здания");

            Assert.AreSame(brick, building.OuterWallMaterial, "материал наружных стен");
            Assert.AreEqual(new Vector2(1.5f, 0f), building.TextureFits.Get(SplineBuildingTextureSurface.OuterWalls)?.OffsetMeters, "подгонка наружных стен");
        }


        [Test]
        public void Apply_OnWallWithType_ShowsProblem()
        {
            var brick = kit.CreateMaterial("Brick", Vector2.one);
            var typeMaterial = kit.CreateMaterial("Type", Vector2.one);
            var source = CreateBlock(Origin);
            source.Material = brick;
            source.TextureFits.Set(BoxModelSideSlot.All, new LiveBuilderTextureFit(new Vector2(0.25f, 0f), Vector2.one, 2));
            BoxModelRebuilder.Rebuild(source.GetComponentInParent<BoxModel>());

            var wallType = ScriptableObject.CreateInstance<SplineWallType>();
            wallType.Material = typeMaterial;
            var wall = testScene.CreateWall(new Vector3(0f, 0f, 20f), new Vector3(6f, 0f, 20f));
            wall.WallType = wallType;
            SplineWallRebuilder.Rebuild(wall);
            Physics.SyncTransforms();
            var ray = new Ray(Origin + new Vector3(3f, 1.5f, 15f), Vector3.forward);

            Assert.IsTrue(LiveBuilderMaterialSlots.TryFindUnder(ray, false, out var slot));
            Assert.IsNotNull(slot.Problem, "у стены с типом — подсказка, почему без материала");

            Assert.IsTrue(LiveBuilderTexturePipette.TryPick(DownOnto(Origin + new Vector3(1f, 1f, 1f))));
            Assert.IsTrue(LiveBuilderTexturePipette.TryApply(ray), "подгонка всё равно легла");
            Assert.AreSame(typeMaterial, wall.Material, "материал — от типа, как был");
            Assert.AreEqual(2, wall.TextureFits.Get(SplineWallTextureSurface.Sides)?.QuarterTurns, "подгонка сторон");

            Object.DestroyImmediate(wallType);
        }


        // Коробка 2 × 1 × 2 в своём объекте; собрана.
        private static BoxModelPart CreateBlock(Vector3 worldPosition)
        {
            var model = BoxModelFactory.CreateModel(worldPosition);
            var part = BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 1f, 2f), BoxModelPartMode.Add);
            BoxModelRebuilder.Rebuild(model);
            return part;
        }

        private static Ray DownOnto(Vector3 point)
        {
            return new Ray(point + Vector3.up * 5f, Vector3.down);
        }
    }
}
