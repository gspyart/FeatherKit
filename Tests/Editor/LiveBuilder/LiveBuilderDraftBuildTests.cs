using System.Collections;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Черновик и доводка (<see cref="LiveBuilderDraftBuilds"/>): пока идёт жест — без фаски, коллайдера и записи на диск;
    /// доводка возвращает фаску и коллайдер; файл пишется через 2 с после конца жеста; правка вне жеста — сразу начисто.
    /// Доводят черновики: чистовой проход (и черновик выключенного объекта), сохранение сцены и файла меша, вход в Play; залипшая мышь жест не держит.
    /// </summary>
    public class LiveBuilderDraftBuildTests
    {
        private const string SavedScenePath = "Assets/LiveBuilderDraftBuildTests.unity";

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
            LiveBuilderDraftBuilds.FinishAll();
            testScene.Dispose();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SavedScenePath) != null)
                AssetDatabase.DeleteAsset(SavedScenePath);

            var savedSceneMeshes = BoxModelMeshStorage.FolderNextTo(SavedScenePath);

            if (AssetDatabase.IsValidFolder(savedSceneMeshes))
                AssetDatabase.DeleteAsset(savedSceneMeshes);
        }


        // ── Черновик и доводка ───────────────────────────────────────────

        [Test]
        public void DraftStep_HasNoBevelAndCollider_FinishAllBringsThemBack()
        {
            var (model, part) = CreateBeveledBlock();
            var mesh = model.GetComponent<MeshFilter>().sharedMesh;
            var finalVertexCount = mesh.vertexCount;
            var finalSignature = model.BuiltSignature;

            LiveBuilderRebuildProfiler.BeginRecording();
            DragOneStep(part);
            var draftPass = LiveBuilderRebuildProfiler.EndRecording();

            Assert.IsTrue(LiveBuilderDraftBuilds.HasPending, "черновик ждёт доводки");
            Assert.AreEqual(1, draftPass.RebuiltConstructionCount, "объект пересобран начерно");
            Assert.AreEqual(0, draftPass.CallsOf(LiveBuilderRebuildStage.Bevel), "без фаски");
            Assert.AreEqual(0, draftPass.CallsOf(LiveBuilderRebuildStage.Collider), "без коллайдера");
            Assert.Less(mesh.vertexCount, finalVertexCount, "рёбра острые");
            Assert.AreEqual(finalSignature, model.BuiltSignature, "подпись в сцене — от чистовой сборки");

            LiveBuilderRebuildProfiler.BeginRecording();
            LiveBuilderDraftBuilds.FinishAll();
            var finish = LiveBuilderRebuildProfiler.EndRecording();

            Assert.IsFalse(LiveBuilderDraftBuilds.HasPending, "черновиков не осталось");
            Assert.AreEqual(1, finish.CallsOf(LiveBuilderRebuildStage.Bevel), "фаска вернулась");
            Assert.AreEqual(1, finish.CallsOf(LiveBuilderRebuildStage.Collider), "коллайдер собран");
            Assert.AreEqual(finalVertexCount, mesh.vertexCount, "тот же брусок с фаской, только сдвинут");
            Assert.AreNotEqual(finalSignature, model.BuiltSignature, "подпись — новая");
        }


        [Test]
        public void SecondDraftStep_WithoutEdits_RebuildsNothing()
        {
            var (_, part) = CreateBeveledBlock();
            DragOneStep(part);
            LiveBuilderRebuildRecording secondPass;

            using (LiveBuilderGestureTracker.BeginExplicitGesture())
            {
                LiveBuilderRebuildProfiler.BeginRecording();
                LiveBuilderRebuildQueue.RunPassNow(LiveBuilderBuildQuality.Draft);
                secondPass = LiveBuilderRebuildProfiler.EndRecording();
            }

            Assert.AreEqual(0, secondPass.RebuiltConstructionCount, "черновик тот же — не пересобирается");
        }


        // Проход не находит выключенный объект — его черновик доводится напрямую, иначе очередь гоняла бы чистовой проход вечно.
        [Test]
        public void FinalPass_FinishesDraftOfDisabledObject()
        {
            var (model, part) = CreateBeveledBlock();
            var mesh = model.GetComponent<MeshFilter>().sharedMesh;
            var finalVertexCount = mesh.vertexCount;
            DragOneStep(part);
            model.gameObject.SetActive(false);

            LiveBuilderRebuildQueue.RunFinalPassNow();

            Assert.IsFalse(LiveBuilderDraftBuilds.HasPending, "черновиков не осталось");
            Assert.AreEqual(finalVertexCount, mesh.vertexCount, "фаска вернулась");
        }


        // Правка вне жеста (поле ввели числом, галка) — сразу чистовой проход, без черновика: иначе фаска мигнула бы.
        [UnityTest]
        public IEnumerator EditOutsideGesture_GoesStraightToFinalPass()
        {
            var (_, part) = CreateBeveledBlock();
            yield return WaitSeconds(0.5);

            LiveBuilderRebuildProfiler.BeginRecording();
            part.transform.position += Vector3.right;
            LiveBuilderRebuildQueue.RequestCheck(part.gameObject);
            yield return WaitSeconds(0.5);
            var recording = LiveBuilderRebuildProfiler.EndRecording();

            Assert.AreEqual(1, recording.RebuiltConstructionCount, "объект пересобран один раз");
            Assert.AreEqual(1, recording.CallsOf(LiveBuilderRebuildStage.Bevel), "и сразу начисто");
            Assert.IsFalse(LiveBuilderDraftBuilds.HasPending, "черновиков нет");
        }


        // Мышь держат на контроле без единой правки дольше сторожа — жест кончился: чужой контрол мог не отпустить захват.
        [UnityTest]
        public IEnumerator HeldHotControlWithoutEdits_StopsCountingAsGesture()
        {
            GUIUtility.hotControl = 4242;

            try
            {
                Assert.IsTrue(LiveBuilderGestureTracker.IsGestureActive, "мышь на контроле — жест");
                yield return WaitSeconds(LiveBuilderGestureTracker.StuckMouseHoldSeconds + 0.5);
                Assert.IsFalse(LiveBuilderGestureTracker.IsGestureActive, "без правок дольше сторожа — уже не жест");

                LiveBuilderGestureTracker.NoteEdit();
                Assert.IsTrue(LiveBuilderGestureTracker.IsGestureActive, "пришла правка — жест снова живой");
            }
            finally
            {
                GUIUtility.hotControl = 0;
            }
        }


        // ── Диск ─────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator DiskWrite_WaitsForGestureEnd_ThenTwoSeconds()
        {
            var (model, part) = CreateBeveledBlock();
            var mesh = model.GetComponent<MeshFilter>().sharedMesh;
            BoxModelMeshStorage.SavePendingNow();
            Assert.IsFalse(EditorUtility.IsDirty(mesh), "до жеста файл записан");

            using (LiveBuilderGestureTracker.BeginExplicitGesture())
            {
                part.transform.position += Vector3.right;
                LiveBuilderRebuildQueue.RunPassNow(LiveBuilderBuildQuality.Draft);

                yield return WaitSeconds(BoxModelMeshStorage.SaveDelaySeconds + 0.5);
                Assert.IsTrue(EditorUtility.IsDirty(mesh), "пока тянешь — на диск не пишется");
            }

            yield return WaitSeconds(1.0);
            Assert.IsFalse(LiveBuilderDraftBuilds.HasPending, "через 0,3 с после жеста черновик доведён");
            Assert.IsTrue(EditorUtility.IsDirty(mesh), "запись — не сразу после доводки");

            yield return WaitSeconds(BoxModelMeshStorage.SaveDelaySeconds + 0.5);
            Assert.IsFalse(EditorUtility.IsDirty(mesh), "через 2 с тишины файл записан");
        }


        // ── Сохранение сцены ─────────────────────────────────────────────

        [Test]
        public void SceneSave_FinishesDrafts()
        {
            var (model, part) = CreateBeveledBlock();
            var signatureBeforeDrag = model.BuiltSignature;
            DragOneStep(part);

            LiveBuilderRebuildProfiler.BeginRecording();
            Assert.IsTrue(EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), SavedScenePath), "сцена сохранена");
            var save = LiveBuilderRebuildProfiler.EndRecording();

            Assert.IsFalse(LiveBuilderDraftBuilds.HasPending, "черновик доведён до записи");
            Assert.AreEqual(1, save.CallsOf(LiveBuilderRebuildStage.Bevel), "с фаской");
            Assert.AreNotEqual(signatureBeforeDrag, model.BuiltSignature, "подпись — от доведённого меша");
        }


        // Save Project или чужой SaveAssets пишет файл меша — черновик сначала доводится: на диск идёт только чистовое.
        [Test]
        public void MeshFileSave_FinishesDraftsFirst()
        {
            var (model, part) = CreateBeveledBlock();
            var mesh = model.GetComponent<MeshFilter>().sharedMesh;
            var finalVertexCount = mesh.vertexCount;
            DragOneStep(part);

            AssetDatabase.SaveAssetIfDirty(mesh);

            Assert.IsFalse(LiveBuilderDraftBuilds.HasPending, "черновик доведён до записи");
            Assert.AreEqual(finalVertexCount, mesh.vertexCount, "записан меш с фаской");
            Assert.IsFalse(EditorUtility.IsDirty(mesh), "и он уже на диске");
        }


        // ── Play ─────────────────────────────────────────────────────────

        // Вход в Play доводит черновики. Сцена проверки и числа живут в памяти редактора, а перезагрузка скриптов их стёрла бы:
        // на время проверки Play входит без неё, какие бы ни были настройки проекта.
        [UnityTest]
        public IEnumerator EnterPlayMode_FinishesDrafts()
        {
            var wereOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            var previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = previousOptions | EnterPlayModeOptions.DisableDomainReload;

            try
            {
                var (model, part) = CreateBeveledBlock();
                var mesh = model.GetComponent<MeshFilter>().sharedMesh;
                var finalVertexCount = mesh.vertexCount;
                DragOneStep(part);
                Assert.IsTrue(LiveBuilderDraftBuilds.HasPending, "черновик ждёт доводки");

                yield return new EnterPlayMode();
                var hasPendingInPlay = LiveBuilderDraftBuilds.HasPending;
                var vertexCountInPlay = mesh.vertexCount;
                yield return new ExitPlayMode();

                Assert.IsFalse(hasPendingInPlay, "в Play черновиков нет");
                Assert.AreEqual(finalVertexCount, vertexCountInPlay, "в Play — меш с фаской");
            }
            finally
            {
                EditorSettings.enterPlayModeOptionsEnabled = wereOptionsEnabled;
                EditorSettings.enterPlayModeOptions = previousOptions;
            }
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Брусок 2 × 1 × 3 м с фаской и коллайдером, собранный начисто.
        private static (BoxModel model, BoxModelPart part) CreateBeveledBlock()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var part = BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 1f, 3f), BoxModelPartMode.Add);
            model.BevelSize = 0.1f;
            model.BevelSegments = 1;
            BoxModelRebuilder.Rebuild(model);
            return (model, part);
        }

        // Шаг жеста: коробку тянут на метр, очередь собирает черновик.
        private static void DragOneStep(BoxModelPart part)
        {
            using (LiveBuilderGestureTracker.BeginExplicitGesture())
            {
                part.transform.position += Vector3.right;
                LiveBuilderRebuildQueue.RunPassNow(LiveBuilderBuildQuality.Draft);
            }
        }

        private static IEnumerator WaitSeconds(double seconds)
        {
            var until = EditorApplication.timeSinceStartup + seconds;

            while (EditorApplication.timeSinceStartup < until)
                yield return null;
        }
    }
}
