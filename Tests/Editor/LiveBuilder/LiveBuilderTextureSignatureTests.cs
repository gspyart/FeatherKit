using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Текстура в подписях (<see cref="LiveBuilderTextureSignature"/>): смена подгонки заливает меш заново без решателя; перенос World-объекта
    /// на целую плитку не пересобирает, на полплитки — только чистовая сборка; правка плитки пересобирает только постройки с этим материалом.
    /// </summary>
    public class LiveBuilderTextureSignatureTests
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
            LiveBuilderDraftBuilds.FinishAll();
            kit.Dispose();
            testScene.Dispose();
        }


        [Test]
        public void FitChange_RefillsWithoutSolver()
        {
            var (model, part) = CreateBlock(Origin, null);
            var before = model.GetComponent<MeshFilter>().sharedMesh.uv;

            LiveBuilderRebuildProfiler.BeginRecording();
            part.TextureFits.Set(BoxModelSideSlot.All, new LiveBuilderTextureFit(new Vector2(0.25f, 0f), Vector2.one, 0));
            LiveBuilderRebuildQueue.RequestCheck(part.gameObject);
            LiveBuilderRebuildQueue.RunFinalPassNow();
            var recording = LiveBuilderRebuildProfiler.EndRecording();

            Assert.AreEqual(1, recording.RebuiltConstructionCount, "объект залит заново");
            Assert.AreEqual(0, recording.CallsOf(LiveBuilderRebuildStage.Solver), "решатель не нужен");
            CollectionAssert.AreNotEqual(before, model.GetComponent<MeshFilter>().sharedMesh.uv, "развёртка другая");
        }


        [Test]
        public void MoveByWholeTile_RebuildsNothing()
        {
            var (model, _) = CreateBlock(Origin + new Vector3(0.25f, 0f, 0f), null);

            LiveBuilderRebuildProfiler.BeginRecording();
            model.transform.position += new Vector3(1f, 2f, -3f);
            LiveBuilderRebuildQueue.RequestCheck(model.gameObject);
            LiveBuilderRebuildQueue.RunFinalPassNow();
            var recording = LiveBuilderRebuildProfiler.EndRecording();

            Assert.AreEqual(0, recording.RebuiltConstructionCount, "рисунок тот же — пересборки нет");
        }


        [Test]
        public void MoveByHalfTile_DraftKeepsMesh_FinalRebuildsOnce()
        {
            var panel = kit.CreateMaterial("Panel", new Vector2(3f, 3f));
            var (model, _) = CreateBlock(Origin, panel);
            LiveBuilderRebuildRecording draft;

            using (LiveBuilderGestureTracker.BeginExplicitGesture())
            {
                LiveBuilderRebuildProfiler.BeginRecording();
                model.transform.position += new Vector3(1.5f, 0f, 0f);
                LiveBuilderRebuildQueue.RequestCheck(model.gameObject);
                LiveBuilderRebuildQueue.RunPassNow(LiveBuilderBuildQuality.Draft);
                draft = LiveBuilderRebuildProfiler.EndRecording();
            }

            LiveBuilderRebuildProfiler.BeginRecording();
            LiveBuilderRebuildQueue.RunFinalPassNow();
            var final = LiveBuilderRebuildProfiler.EndRecording();

            Assert.AreEqual(0, draft.RebuiltConstructionCount, "в жесте текстура едет с объектом");
            Assert.AreEqual(1, final.RebuiltConstructionCount, "после жеста — ложится на мир");

            LiveBuilderRebuildProfiler.BeginRecording();
            LiveBuilderRebuildQueue.RunFinalPassNow();
            Assert.AreEqual(0, LiveBuilderRebuildProfiler.EndRecording().RebuiltConstructionCount, "дальше — стабильно");
        }


        [Test]
        public void TileChange_RebuildsOnlyConstructionsWithThatMaterial()
        {
            var panel = kit.CreateMaterial("Panel", Vector2.one);
            var (withPanel, _) = CreateBlock(Origin, panel);
            var (plain, _) = CreateBlock(Origin + new Vector3(10f, 0f, 0f), null);
            var plainSignature = plain.BuiltSignature;
            var panelSignature = withPanel.BuiltSignature;

            LiveBuilderRebuildProfiler.BeginRecording();
            LiveBuilderProjectSettings.instance.SetMaterialTileSize(panel, new Vector2(3f, 3f));
            LiveBuilderRebuildQueue.RunFinalPassNow();
            var recording = LiveBuilderRebuildProfiler.EndRecording();

            Assert.AreEqual(1, recording.RebuiltConstructionCount, "пересобран только объект с панелью");
            Assert.AreNotEqual(panelSignature, withPanel.BuiltSignature);
            Assert.AreEqual(plainSignature, plain.BuiltSignature, "объект без панели не тронут");
        }


        // Object и всё по умолчанию — подпись та же, что до этапа 5: старые постройки не пересобираются.
        [Test]
        public void ObjectWithoutFits_SignatureHasNoTexturePart()
        {
            var (model, _) = CreateBlock(Origin, null);
            model.TextureAlignment = LiveBuilderTextureAlignment.Object;
            BoxModelRebuilder.Rebuild(model);

            StringAssert.DoesNotContain("|w", model.BuiltSignature, "мирового ключа нет");
            Assert.AreEqual("", BoxModelSignature.WorldKeyOf(model, BoxModelRebuilder.GetSolverBoxes(model)));
        }


        // Объект из коробок с одной коробкой 2,3 × 3 × 1, собран начисто.
        private static (BoxModel Model, BoxModelPart Part) CreateBlock(Vector3 worldPosition, Material material)
        {
            var model = BoxModelFactory.CreateModel(worldPosition);

            if (material != null)
                model.DefaultMaterial = material;

            var part = BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2.3f, 3f, 1f), BoxModelPartMode.Add);
            BoxModelRebuilder.Rebuild(model);
            LiveBuilderRebuildQueue.RunFinalPassNow();
            return (model, part);
        }
    }
}
