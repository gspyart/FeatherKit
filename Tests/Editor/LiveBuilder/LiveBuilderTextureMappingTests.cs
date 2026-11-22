using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Развёртка этапа 5 (<see cref="LiveBuilderTextureMapping"/>): формула; World — рисунок сходится на стыке соседних объектов,
    /// экземпляры префаба в разных местах делят меш; Object — перенос не меняет развёртку; фасад с панелью 3 × 3 м ложится
    /// по сетке 3 м на всех этажах, балкон рядом продолжает рисунок. Старые меши без изменений — <see cref="LiveBuilderReferenceLevelTests"/>.
    /// </summary>
    public class LiveBuilderTextureMappingTests
    {
        private const string TempFolder = "Assets/LiveBuilderTextureTestTemp";
        private const float Tolerance = 0.002f;

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
            testScene.Dispose();

            if (AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.DeleteAsset(TempFolder);
        }


        // ── Формула ──────────────────────────────────────────────────────

        [Test]
        public void Map_Defaults_EqualPlanarMeters()
        {
            var planar = new Vector2(12.345f, -6.789f);
            Assert.AreEqual(planar, LiveBuilderTextureMapping.Map(planar, Vector2.zero, null, Vector2.one), "без подгонки — те же метры, байт в байт");
            Assert.AreEqual(planar, LiveBuilderTextureMapping.Map(planar, Vector2.zero, new LiveBuilderTextureFit(), Vector2.one));
        }


        [Test]
        public void Map_TurnScaleOffsetAndTile()
        {
            var planar = new Vector2(2f, 1f);
            AssertClose(new Vector2(-1f, 2f), LiveBuilderTextureMapping.Map(planar, Vector2.zero, new LiveBuilderTextureFit(Vector2.zero, Vector2.one, 1), Vector2.one), "90°");
            AssertClose(new Vector2(1f, 0.25f), LiveBuilderTextureMapping.Map(planar, Vector2.zero, new LiveBuilderTextureFit(Vector2.zero, new Vector2(2f, 4f), 0), Vector2.one), "масштаб");
            AssertClose(new Vector2(0.5f, 1f), LiveBuilderTextureMapping.Map(planar, Vector2.zero, new LiveBuilderTextureFit(new Vector2(1.5f, 0f), Vector2.one, 0), Vector2.one), "сдвиг");
            AssertClose(new Vector2(2f / 3f, 1f / 3f), LiveBuilderTextureMapping.Map(planar, Vector2.zero, null, new Vector2(3f, 3f)), "плитка 3 × 3");
        }


        // Мировой сдвиг — по модулю плитки: UV не уходит далеко, а точка рисунка та же.
        [Test]
        public void Map_WorldShift_WrapsByTile()
        {
            var uv = LiveBuilderTextureMapping.Map(new Vector2(1f, 0f), new Vector2(5001f, 3f), null, new Vector2(3f, 3f));
            Assert.Less(Mathf.Abs(uv.x), 2f, "UV рядом с нулём");
            Assert.IsTrue(LiveBuilderTextureTestKit.IsSameTilePoint(uv.x, (5001f + 1f) / 3f), "та же точка рисунка");
            Assert.IsTrue(LiveBuilderTextureTestKit.IsSameTilePoint(uv.y, 0f));
        }


        // ── World и Object ───────────────────────────────────────────────

        [Test]
        public void World_TwoObjectsSideBySide_UvContinuousAtSeam()
        {
            foreach (var tile in new[] { Vector2.one, new Vector2(3f, 3f) })
            {
                var material = kit.CreateMaterial("Seam " + tile.x, tile);
                var left = CreateBlock(Origin, new Vector3(2.3f, 3f, 1f), material);
                var right = CreateBlock(Origin + new Vector3(2.3f, 0f, 0f), new Vector3(2.3f, 3f, 1f), material);

                foreach (var height in new[] { 0f, 3f })
                {
                    var seam = Origin + new Vector3(2.3f, height, 0f);
                    Assert.IsTrue(LiveBuilderTextureTestKit.TryGetUvAt(left.GetComponent<MeshFilter>(), seam, Vector3.back, out var leftUv), "угол левого");
                    Assert.IsTrue(LiveBuilderTextureTestKit.TryGetUvAt(right.GetComponent<MeshFilter>(), seam, Vector3.back, out var rightUv), "угол правого");
                    Assert.IsTrue(LiveBuilderTextureTestKit.IsSameTilePoint(leftUv.x, rightUv.x, Tolerance), "U на стыке: " + leftUv.x + " и " + rightUv.x);
                    Assert.IsTrue(LiveBuilderTextureTestKit.IsSameTilePoint(leftUv.y, rightUv.y, Tolerance), "V на стыке");
                }

                Object.DestroyImmediate(left.gameObject);
                Object.DestroyImmediate(right.gameObject);
            }
        }


        [Test]
        public void Object_MovedObject_SameUv()
        {
            var model = CreateBlock(Origin, new Vector3(2.3f, 3f, 1f), null);
            model.TextureAlignment = LiveBuilderTextureAlignment.Object;
            BoxModelRebuilder.Rebuild(model);
            var before = model.GetComponent<MeshFilter>().sharedMesh.uv;

            model.transform.position += new Vector3(0.37f, 1.1f, -2.71f);
            BoxModelRebuilder.Rebuild(model);

            CollectionAssert.AreEqual(before, model.GetComponent<MeshFilter>().sharedMesh.uv, "Object — текстура едет с объектом");
        }


        [Test]
        public void World_MovedObject_PatternStaysInWorld()
        {
            var model = CreateBlock(Origin, new Vector3(2f, 3f, 1f), null);
            var corner = Origin + new Vector3(1f, 0f, 0f);
            Assert.IsFalse(LiveBuilderTextureTestKit.TryGetUvAt(model.GetComponent<MeshFilter>(), corner, Vector3.back, out _), "в этой точке вершины пока нет");

            model.transform.position += new Vector3(1.37f, 0f, 0f);
            BoxModelRebuilder.Rebuild(model);
            var filter = model.GetComponent<MeshFilter>();
            Assert.IsTrue(LiveBuilderTextureTestKit.TryGetUvAt(filter, Origin + new Vector3(1.37f, 0f, 0f), Vector3.back, out var uv));
            Assert.IsTrue(LiveBuilderTextureTestKit.IsSameTilePoint(uv.x, Origin.x + 1.37f, Tolerance), "U — по миру: " + uv.x);
        }


        // «Мир» в префабе — его корень: экземпляры в разных местах — один меш.
        [Test]
        public void World_PrefabInstancesAtDifferentPlaces_ShareMesh()
        {
            AssetDatabase.CreateFolder("Assets", "LiveBuilderTextureTestTemp");
            var model = CreateBlock(Origin + new Vector3(0.37f, 0f, 0.71f), new Vector3(2.3f, 3f, 1f), null);
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(model.gameObject, TempFolder + "/Texture Block.prefab", InteractionMode.AutomatedAction);
            var other = ((GameObject)PrefabUtility.InstantiatePrefab(prefab)).GetComponent<BoxModel>();
            other.transform.position = Origin + new Vector3(5.53f, 1.21f, 2.29f);

            BoxModelPrefabInstances.ForgetAllChecks();
            BoxModelRebuilder.RebuildChangedModels();

            Assert.IsTrue(BoxModelPrefabInstances.UsesPrefabGeometry(model, out _), "первый экземпляр — меш префаба");
            Assert.IsTrue(BoxModelPrefabInstances.UsesPrefabGeometry(other, out _), "второй, в другом месте, — тот же меш");
            Assert.AreSame(model.GetComponent<MeshFilter>().sharedMesh, other.GetComponent<MeshFilter>().sharedMesh);
        }


        // ── Фасад ────────────────────────────────────────────────────────

        // Секция 24 × 12 м, 9 этажей по 3 м на сетке 3 м, панель 3 × 3: у наружных стен V × 3 — высота в мире, U × 3 — место вдоль фасада;
        // на высоте каждого перекрытия V — целое. Балкон-коробка у фасада продолжает тот же рисунок.
        [Test]
        public void Facade_Panel3x3_AlignedOnAllStoreys()
        {
            var panel = kit.CreateMaterial("Panel", new Vector2(3f, 3f));
            var corner = Origin + new Vector3(1f, 0f, 1f);
            var building = testScene.CreateBuilding(9, Vector3.zero, new Vector3(24f, 0f, 0f), new Vector3(24f, 0f, 12f), new Vector3(0f, 0f, 12f));
            building.transform.position = corner;
            building.StoreyHeight = 3f;
            building.OuterWallMaterial = panel;
            SplineBuildingRebuilder.Rebuild(building);

            var heights = new System.Collections.Generic.HashSet<int>();

            foreach (var part in building.GetComponentsInChildren<SplineBuildingPart>())
            {
                if (part.Kind != SplineBuildingPartKind.Walls)
                    continue;

                var filter = part.GetComponent<MeshFilter>();
                var mesh = filter.sharedMesh;
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var uvs = mesh.uv;

                foreach (var index in mesh.GetTriangles(SplineBuildingMeshBuilder.OuterWallSubmesh))
                {
                    var normal = filter.transform.TransformDirection(normals[index]);

                    if (Mathf.Abs(normal.y) > 0.1f)
                        continue;

                    var position = filter.transform.TransformPoint(vertices[index]);
                    var along = Mathf.Abs(normal.z) > 0.5f ? position.x : position.z;
                    var uv = uvs[index];
                    Assert.IsTrue(LiveBuilderTextureTestKit.IsSameTilePoint(uv.y, position.y / 3f, Tolerance), "V — высота в мире: " + position);
                    Assert.IsTrue(LiveBuilderTextureTestKit.IsSameTilePoint(uv.x, along / 3f, Tolerance)
                                  || LiveBuilderTextureTestKit.IsSameTilePoint(uv.x, -along / 3f, Tolerance), "U — по сетке фасада: " + position);

                    if (Mathf.Abs(position.y / 3f - Mathf.Round(position.y / 3f)) < 0.001f)
                    {
                        Assert.IsTrue(LiveBuilderTextureTestKit.IsSameTilePoint(uv.y, 0f, Tolerance), "на перекрытии V целое: " + position);
                        heights.Add(Mathf.RoundToInt(position.y / 3f));
                    }
                }
            }

            for (var storey = 0; storey < 9; storey++)
                Assert.IsTrue(heights.Contains(storey), "шов панели на перекрытии этажа " + (storey + 1));

            var balcony = CreateBlock(corner + new Vector3(3f, 3f, -1.5f), new Vector3(6f, 1.2f, 1.5f), panel);
            var balconyFilter = balcony.GetComponent<MeshFilter>();

            foreach (var point in new[] { new Vector3(3f, 3f, -1.5f), new Vector3(9f, 4.2f, -1.5f) })
            {
                var world = corner + point;
                Assert.IsTrue(LiveBuilderTextureTestKit.TryGetUvAt(balconyFilter, world, Vector3.back, out var uv), "угол балкона");
                Assert.IsTrue(LiveBuilderTextureTestKit.IsSameTilePoint(uv.x, world.x / 3f, Tolerance), "балкон: U по той же сетке");
                Assert.IsTrue(LiveBuilderTextureTestKit.IsSameTilePoint(uv.y, world.y / 3f, Tolerance), "балкон: V по той же сетке");
            }
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Объект из коробок (World, как из инструмента) с одной коробкой size от его опоры; собран.
        private static BoxModel CreateBlock(Vector3 worldPosition, Vector3 size, Material material)
        {
            var model = BoxModelFactory.CreateModel(worldPosition);

            if (material != null)
                model.DefaultMaterial = material;

            BoxModelFactory.CreatePart(model, Vector3.zero, size, BoxModelPartMode.Add);
            BoxModelRebuilder.Rebuild(model);
            return model;
        }

        private static void AssertClose(Vector2 expected, Vector2 actual, string message)
        {
            Assert.Less((expected - actual).magnitude, 0.0001f, message + ": ждали " + expected + ", вышло " + actual);
        }
    }
}
