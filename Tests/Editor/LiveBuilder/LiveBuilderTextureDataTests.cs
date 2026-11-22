using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Данные текстуры этапа 5: старые сцены читаются как «Object, без подгонок»; подгонки переживают запись; новые постройки — World;
    /// материала нет в таблице плиток — 1 × 1 м.
    /// </summary>
    public class LiveBuilderTextureDataTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private LiveBuilderTextureTestKit kit;


        [SetUp]
        public void OpenScene()
        {
            testScene = new LiveBuilderTestScene();
            kit = new LiveBuilderTextureTestKit();
        }


        [TearDown]
        public void CloseScene()
        {
            kit.Dispose();
            testScene?.Dispose();
        }


        // ── Старые данные ────────────────────────────────────────────────

        // Сцены-образцы записаны до этапа 5: новых полей в них нет.
        [Test]
        public void OldScenes_ReadAsObjectWithoutFits()
        {
            // Образец открывается вместо безымянной сцены — временная здесь не нужна.
            testScene.Dispose();
            testScene = null;
            var constructionCount = 0;

            foreach (var name in LiveBuilderUpgradeFixtures.Names)
            {
                var scene = LiveBuilderUpgradeFixtureTests.OpenFixture(name, out var replacedUntitledScene);

                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var model in root.GetComponentsInChildren<BoxModel>(true))
                        {
                            Assert.AreEqual(LiveBuilderTextureAlignment.Object, model.TextureAlignment, name + " / " + model.name);
                            constructionCount++;
                        }

                        foreach (var wall in root.GetComponentsInChildren<SplineWall>(true))
                        {
                            Assert.AreEqual(LiveBuilderTextureAlignment.Object, wall.TextureAlignment, name + " / " + wall.name);
                            Assert.IsTrue(wall.TextureFits.IsEmpty, name + " / " + wall.name + ": без подгонок");
                        }

                        foreach (var building in root.GetComponentsInChildren<SplineBuilding>(true))
                        {
                            Assert.AreEqual(LiveBuilderTextureAlignment.Object, building.TextureAlignment, name + " / " + building.name);
                            Assert.IsTrue(building.TextureFits.IsEmpty, name + " / " + building.name + ": без подгонок");
                        }

                        foreach (var part in root.GetComponentsInChildren<BoxModelPart>(true))
                            Assert.IsTrue(part.TextureFits.IsEmpty, name + " / " + part.name + ": без подгонок");

                        foreach (var cutter in root.GetComponentsInChildren<GeometryCutter>(true))
                            Assert.IsTrue(cutter.TextureFits.IsEmpty, name + " / " + cutter.name + ": без подгонок");

                        foreach (var stairs in root.GetComponentsInChildren<BoxModelStairs>(true))
                            Assert.IsTrue(stairs.TextureFit.IsDefault, name + " / " + stairs.name + ": как есть");
                    }
                }
                finally
                {
                    LiveBuilderUpgradeFixtureTests.CloseFixture(scene, replacedUntitledScene);
                }
            }

            Assert.Greater(constructionCount, 0, "в образцах есть постройки");
        }


        // Список подгонок — обобщённый класс: Unity должна его писать и читать, как обычное поле.
        [Test]
        public void TextureFits_SurviveSerialization()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var part = BoxModelFactory.CreatePart(model, Vector3.zero, Vector3.one, BoxModelPartMode.Add);
            part.TextureFits.Set(BoxModelSideSlot.Top, new LiveBuilderTextureFit(new Vector2(1.5f, 0f), new Vector2(2f, 1f), 1));

            var building = testScene.CreateBuilding(1, Vector3.zero, new Vector3(6f, 0f, 0f), new Vector3(6f, 0f, 6f), new Vector3(0f, 0f, 6f));
            building.TextureFits.Set(SplineBuildingTextureSurface.Roof, new LiveBuilderTextureFit(Vector2.zero, Vector2.one, 3));

            var partCopy = new GameObject("Copy").AddComponent<BoxModelPart>();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(part), partCopy);
            var top = partCopy.TextureFits.Get(BoxModelSideSlot.Top);
            Assert.IsNotNull(top, "подгонка верха записана");
            Assert.AreEqual(new Vector2(1.5f, 0f), top.OffsetMeters);
            Assert.AreEqual(new Vector2(2f, 1f), top.Scale);
            Assert.AreEqual(1, top.QuarterTurns);
            Assert.IsNull(partCopy.TextureFits.Get(BoxModelSideSlot.Bottom), "у низа своей нет");

            var serialized = new SerializedObject(building);
            Assert.IsNotNull(serialized.FindProperty("textureFits.fits.Array.data[0].place"), "список подгонок виден в инспекторе");
            Assert.AreEqual(3, building.TextureFits.Get(SplineBuildingTextureSurface.Roof).QuarterTurns);
        }


        // Подгонка «как есть» в список не попадает: подпись постройки та же.
        [Test]
        public void DefaultFit_IsNotStored()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var part = BoxModelFactory.CreatePart(model, Vector3.zero, Vector3.one, BoxModelPartMode.Add);
            part.TextureFits.Set(BoxModelSideSlot.Top, new LiveBuilderTextureFit());

            Assert.IsTrue(part.TextureFits.IsEmpty);
        }


        // ── Новые постройки ──────────────────────────────────────────────

        [Test]
        public void NewConstructions_AreWorld()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(4f, 0f, 0f));
            var building = testScene.CreateBuilding(1, Vector3.zero, new Vector3(6f, 0f, 0f), new Vector3(6f, 0f, 6f), new Vector3(0f, 0f, 6f));

            Assert.AreEqual(LiveBuilderTextureAlignment.World, model.TextureAlignment, "объект из коробок");
            Assert.AreEqual(LiveBuilderTextureAlignment.World, wall.TextureAlignment, "стена");
            Assert.AreEqual(LiveBuilderTextureAlignment.World, building.TextureAlignment, "здание");
            Assert.AreEqual(LiveBuilderTextureAlignment.World, new GameObject("Added").AddComponent<BoxModel>().TextureAlignment, "добавленный руками — тоже");
        }


        // ── Плитка материала ─────────────────────────────────────────────

        [Test]
        public void MaterialNotInTable_IsOneByOne()
        {
            var plain = kit.CreateMaterial("Plain", Vector2.one);
            var panel = kit.CreateMaterial("Panel", new Vector2(3f, 3f));

            Assert.AreEqual(Vector2.one, LiveBuilderMaterialTiling.SizeOf(plain), "нет в таблице — 1 × 1");
            Assert.AreEqual(Vector2.one, LiveBuilderMaterialTiling.SizeOf(null), "пусто — 1 × 1");
            Assert.AreEqual(new Vector2(3f, 3f), LiveBuilderMaterialTiling.SizeOf(panel), "панель — 3 × 3");

            LiveBuilderProjectSettings.instance.SetMaterialTileSize(panel, Vector2.one);
            Assert.AreEqual(Vector2.one, LiveBuilderMaterialTiling.SizeOf(panel), "убрали из таблицы — снова 1 × 1");
        }
    }
}
