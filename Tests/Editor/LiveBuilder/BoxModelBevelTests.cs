using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Фаска рёбер объекта из коробок: плоский срез и скругление ложатся по выпуклому ребру и не трогают середину грани,
    /// вогнутое ребро остаётся острым, выключенная фаска возвращает острые рёбра. Высоту поверхности меряет луч сверху по коллайдеру.
    /// </summary>
    public class BoxModelBevelTests
    {
        private const float Tolerance = 0.01f;

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


        // ── Выпуклое ребро ───────────────────────────────────────────────

        [Test]
        public void FlatBevel_CutsEdgeAt45Degrees()
        {
            CreateBlock(0.2f, 1);

            Assert.AreEqual(1f, HeightAt(1f, 1.5f), Tolerance, "середина верха");
            Assert.AreEqual(0.85f, HeightAt(1.95f, 1.5f), Tolerance, "у ребра: срез 0,2 под 45°");
        }


        [Test]
        public void RoundBevel_FollowsQuarterCircle()
        {
            CreateBlock(0.2f, 8);

            // Окружность с центром (1,8; 0,8) и радиусом 0,2 над x = 1,95 — на высоте 0,932.
            Assert.AreEqual(0.932f, HeightAt(1.95f, 1.5f), Tolerance);
        }


        [Test]
        public void BevelOff_RestoresSharpEdge()
        {
            var model = CreateBlock(0.2f, 1);
            model.BevelSize = 0f;
            BoxModelRebuilder.Rebuild(model);

            Assert.AreEqual(1f, HeightAt(1.95f, 1.5f), Tolerance);
        }


        // ── Вогнутое ребро ───────────────────────────────────────────────

        [Test]
        public void ConcaveEdge_StaysSharp()
        {
            // Ступень: низ 2 × 1 × 3, на его левой половине — верх 1 × 2 × 3. Где верх низа встречает бок верха — вогнутое ребро.
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 1f, 3f), BoxModelPartMode.Add);
            BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(1f, 2f, 3f), BoxModelPartMode.Add);
            model.BevelSize = 0.2f;
            BoxModelRebuilder.Rebuild(model);

            Assert.AreEqual(1f, HeightAt(1.05f, 1.5f), Tolerance, "у вогнутого ребра верх низа целый");
            Assert.AreEqual(1.85f, HeightAt(0.95f, 1.5f), Tolerance, "выпуклое ребро верха срезано");
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Брусок 2 × 1 × 3 м от Origin с фаской.
        private static BoxModel CreateBlock(float bevelSize, int bevelSegments)
        {
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 1f, 3f), BoxModelPartMode.Add);
            model.BevelSize = bevelSize;
            model.BevelSegments = bevelSegments;
            BoxModelRebuilder.Rebuild(model);
            return model;
        }

        // Высота поверхности над точкой (x, z) от Origin: луч сверху вниз.
        private static float HeightAt(float x, float z)
        {
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(Origin + new Vector3(x, 10f, z), Vector3.down, out var hit, 20f), "луч не попал в объект");
            return hit.point.y - Origin.y;
        }
    }
}
