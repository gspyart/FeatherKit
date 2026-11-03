using FeatherKit.EditorTools;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Куда ложится материал, брошенный на постройку (<see cref="LiveBuilderMaterialSlots"/>): бок и верх коробки — в свою сторону,
    /// с «вся коробка» — в общий; пол дыры — в низ вырезающей коробки; стена — в стену, а у стены с типом — нельзя; здание — в то, что под лучом.
    /// Палитра уровня красит выделенную коробку её же данными, а не рендерером.
    /// </summary>
    public class LiveBuilderMaterialSlotTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private Material testMaterial;


        [SetUp]
        public void OpenScene()
        {
            testScene = new LiveBuilderTestScene();
            testMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Material Slot Test" };
        }


        [TearDown]
        public void CloseScene()
        {
            testScene.Dispose();
            Object.DestroyImmediate(testMaterial);
        }


        // ── Коробки ──────────────────────────────────────────────────────

        [Test]
        public void BoxSide_GoesToThatSide()
        {
            var part = CreateBlock(out var model);

            Assert.IsTrue(LiveBuilderMaterialSlots.TryFindUnder(RayFrom(new Vector3(5f, 1f, 1f), Vector3.left), false, out var slot));
            Assert.IsTrue(slot.TryAssign(testMaterial));
            BoxModelRebuilder.Rebuild(model);

            Assert.AreEqual(testMaterial, part.RightMaterial, "бок +X");
            Assert.IsNull(part.Material, "общий не тронут");
            CollectionAssert.Contains(model.GetComponent<MeshRenderer>().sharedMaterials, testMaterial, "в меше — новый материал");
        }


        [Test]
        public void BoxTop_GoesToTop()
        {
            var part = CreateBlock(out _);

            Assert.IsTrue(LiveBuilderMaterialSlots.TryFindUnder(RayFrom(new Vector3(1f, 5f, 1f), Vector3.down), false, out var slot));
            slot.TryAssign(testMaterial);

            Assert.AreEqual(testMaterial, part.TopMaterial);
        }


        [Test]
        public void WholeBox_SetsCommonAndClearsSides()
        {
            var part = CreateBlock(out _);
            part.TopMaterial = testMaterial;

            Assert.IsTrue(LiveBuilderMaterialSlots.TryFindUnder(RayFrom(new Vector3(5f, 1f, 1f), Vector3.left), true, out var slot));
            slot.TryAssign(testMaterial);

            Assert.AreEqual(testMaterial, part.Material);
            Assert.IsNull(part.TopMaterial, "свои у сторон сняты");
        }


        [Test]
        public void HoleFloor_GoesToBottomOfCuttingBox()
        {
            CreateBlock(out var model);
            var hole = BoxModelFactory.CreatePart(model, new Vector3(0.5f, 1f, 0.5f), new Vector3(1.5f, 3f, 1.5f), BoxModelPartMode.Cut);
            BoxModelRebuilder.Rebuild(model);

            Assert.IsTrue(LiveBuilderMaterialSlots.TryFindUnder(RayFrom(new Vector3(1f, 5f, 1f), Vector3.down), false, out var slot));
            slot.TryAssign(testMaterial);

            Assert.AreEqual(testMaterial, hole.BottomMaterial, "пол дыры — низ вырезающей коробки");
        }


        // ── Стены и здания ───────────────────────────────────────────────

        [Test]
        public void Wall_GoesToWall_TypedWallRefuses()
        {
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(6f, 0f, 0f));

            Assert.IsTrue(LiveBuilderMaterialSlots.TryFindUnder(RayFrom(new Vector3(3f, 1f, -3f), Vector3.forward), false, out var slot));
            Assert.IsTrue(slot.TryAssign(testMaterial));
            Assert.AreEqual(testMaterial, wall.Material);

            var wallType = ScriptableObject.CreateInstance<SplineWallType>();
            wall.WallType = wallType;
            LiveBuilderMaterialSlots.TryFindUnder(RayFrom(new Vector3(3f, 1f, -3f), Vector3.forward), false, out var typedSlot);

            Assert.IsNotNull(typedSlot.Problem, "у стены с типом материал задаёт тип");
            Assert.IsFalse(typedSlot.TryAssign(null));
            wall.WallType = null;
            Object.DestroyImmediate(wallType);
        }


        [Test]
        public void Building_OuterWallAndFloor()
        {
            var building = testScene.CreateBuilding(1, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var height = building.FirstFloorWorldHeight - Origin.y + 1f;

            Assert.IsTrue(LiveBuilderMaterialSlots.TryFindUnder(RayFrom(new Vector3(4f, height, -3f), Vector3.forward), false, out var wallSlot));
            wallSlot.TryAssign(testMaterial);
            Assert.AreEqual(testMaterial, building.OuterWallMaterial, "снаружи — наружные стены");

            Assert.IsTrue(LiveBuilderMaterialSlots.TryFindUnder(RayFrom(new Vector3(4f, height, 4f), Vector3.down), false, out var floorSlot));
            Assert.AreEqual("пол здания", floorSlot.Description);
        }


        // ── Палитра ──────────────────────────────────────────────────────

        [Test]
        public void Palette_PaintsSelectedBoxData()
        {
            var part = CreateBlock(out _);
            var painter = ObjectMaterialPainters.Find(part.gameObject);

            Assert.IsNotNull(painter, "коробку красит Live Builder, а не рендерер");
            Assert.IsTrue(painter.TryPaint(part.gameObject, testMaterial));
            Assert.AreEqual(testMaterial, part.Material);
        }


        [Test]
        public void Palette_FaceMode_PaintsOnlySelectedFace()
        {
            var part = CreateBlock(out var model);
            var defaultMaterial = model.DefaultMaterial;

            // В режиме «Грани» в Unity выделен объект из коробок, а грань помнит Live Builder.
            BoxModelFaceSelection.Select(new BoxModelPartFace(part, 2), false);
            ObjectMaterialPainters.Find(model.gameObject).TryPaint(model.gameObject, testMaterial);
            BoxModelFaceSelection.Clear();
            UnityEditor.Selection.activeGameObject = null;

            Assert.AreEqual(testMaterial, part.TopMaterial, "верх — покрашен");
            Assert.AreEqual(defaultMaterial, model.DefaultMaterial, "материал объекта не тронут");
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Брусок 2 × 2 × 2 м от Origin, уже собран.
        private static BoxModelPart CreateBlock(out BoxModel model)
        {
            model = BoxModelFactory.CreateModel(Origin);
            var part = BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 2f, 2f), BoxModelPartMode.Add);
            BoxModelRebuilder.Rebuild(model);
            Physics.SyncTransforms();
            return part;
        }

        private static Ray RayFrom(Vector3 fromOrigin, Vector3 direction)
        {
            Physics.SyncTransforms();
            return new Ray(Origin + fromOrigin, direction);
        }
    }
}
