using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Пересборка только задетого, на эталонном уровне (<see cref="LiveBuilderReferenceLevel"/>): новый этаж пересобирает только верх здания,
    /// лестница — только плиты, сквозь которые идёт её вырез, окно на всех этажах — только стены; после жеста с окном меши — эталон.
    /// Включение инструмента и выделение не пересобирают ничего.
    /// </summary>
    public class LiveBuilderPartialRebuildTests
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
            LiveBuilderDraftBuilds.FinishAll();
            testScene.Dispose();
        }


        // ── Части здания ─────────────────────────────────────────────────

        [Test]
        public void StoreysNineToTen_RebuildsOnlyTopParts()
        {
            var building = LiveBuilderReferenceLevel.Build(Origin).Building;
            var before = MeshStatesOf(building);

            building.StoreyCount = 10;
            LiveBuilderRebuildQueue.RunFinalPassNow();

            var rebuilt = RebuiltPartsOf(building, before);
            CollectionAssert.AreEquivalent(new[] { "Walls 8", "Walls 9", "Slab 9", "Roof 0" }, rebuilt, "пересобрались: " + string.Join(", ", rebuilt));
        }


        [Test]
        public void StairsMoved_RebuildsOnlySlabsItsOpeningGoesThrough()
        {
            var targets = LiveBuilderReferenceLevel.Build(Origin);
            var building = targets.Building;
            var stairs = targets.Root.GetComponentsInChildren<BoxModelStairs>().Single();
            var before = MeshStatesOf(building);

            stairs.transform.position += building.transform.right * 0.2f;
            LiveBuilderRebuildQueue.RunFinalPassNow();

            var rebuilt = RebuiltPartsOf(building, before);
            var expected = Enumerable.Range(1, 8).Select(storey => "Slab " + storey).ToList();
            CollectionAssert.AreEquivalent(expected, rebuilt, "пересобрались: " + string.Join(", ", rebuilt));
        }


        [Test]
        public void WindowOnAllStoreysMoved_RebuildsOnlyWalls()
        {
            var targets = LiveBuilderReferenceLevel.Build(Origin);
            var building = targets.Building;
            var before = MeshStatesOf(building);

            targets.Window.transform.position += building.transform.right * 0.5f;
            LiveBuilderRebuildQueue.RunFinalPassNow();

            var rebuilt = RebuiltPartsOf(building, before);
            var expected = Enumerable.Range(0, 9).Select(storey => "Walls " + storey).ToList();
            CollectionAssert.AreEquivalent(expected, rebuilt, "пересобрались: " + string.Join(", ", rebuilt));
        }


        // Шаги жеста режут заготовку только окном — черновик; доводка после жеста даёт ровно эталонные меши.
        [Test]
        public void WindowGesture_ThenFinalPass_MatchesBaseline()
        {
            var targets = LiveBuilderReferenceLevel.Build(Origin);
            var window = targets.Window.transform;
            var step = targets.Building.transform.right * 0.5f;

            using (LiveBuilderGestureTracker.BeginExplicitGesture())
            {
                foreach (var offset in new[] { step, step, -step, -step })
                {
                    window.position += offset;
                    LiveBuilderRebuildQueue.RequestCheck(window.gameObject);
                    LiveBuilderRebuildQueue.RunPassNow(LiveBuilderBuildQuality.Draft);
                }
            }

            Assert.IsTrue(LiveBuilderDraftBuilds.HasPending, "черновики ждут доводки");
            LiveBuilderRebuildQueue.RunFinalPassNow();

            var baseline = LiveBuilderMeshDigest.Parse(File.ReadAllLines(LiveBuilderReferenceLevelTests.BaselinePath()));
            var differences = LiveBuilderMeshDigest.Compare(baseline, LiveBuilderMeshDigest.Parse(LiveBuilderMeshDigest.DescribeLevel(targets.Root)), true);
            Assert.IsEmpty(differences, string.Join("\n", differences));
        }


        // ── Инструмент и выделение ───────────────────────────────────────

        [UnityTest]
        public IEnumerator ToolActivationAndSelection_RebuildNothing()
        {
            var selected = new List<Object>();

            for (var index = 0; index < 5; index++)
            {
                var model = BoxModelFactory.CreateModel(Origin + new Vector3(index * 4f, 0f, 0f));
                BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(2f, 1f, 2f), BoxModelPartMode.Add);
                BoxModelRebuilder.Rebuild(model);
                selected.Add(model.gameObject);
                selected.Add(testScene.CreateWall(new Vector3(index * 4f, 0f, 6f), new Vector3(index * 4f + 3f, 0f, 6f)).gameObject);
            }

            // Создание — правка: очередь должна отработать её до начала замера.
            yield return WaitSeconds(0.5);
            LiveBuilderDraftBuilds.FinishEverythingPending();
            BoxModelMeshStorage.SavePendingNow();

            var previousSelection = Selection.objects;
            var wasToolActive = LiveBuilderActivation.IsToolActive;
            var wasWindowOpen = EditorWindow.HasOpenInstances<LiveBuilderWindow>();
            LiveBuilderRebuildRecording recording;
            LiveBuilderRebuildProfiler.BeginRecording();

            try
            {
                LiveBuilderActivation.SetToolActive(true);
                yield return WaitSeconds(0.2);
                Selection.objects = selected.ToArray();
                yield return WaitSeconds(0.2);
                LiveBuilderWindow.Open();
                yield return WaitSeconds(0.5);
            }
            finally
            {
                recording = LiveBuilderRebuildProfiler.EndRecording();
                Selection.objects = previousSelection;
                LiveBuilderActivation.SetToolActive(wasToolActive);

                if (!wasWindowOpen && EditorWindow.HasOpenInstances<LiveBuilderWindow>())
                    EditorWindow.GetWindow<LiveBuilderWindow>().Close();
            }

            Assert.AreEqual(0, recording.PassCount, "проходов очереди нет");
            Assert.AreEqual(0, recording.RebuiltConstructionCount, "пересборок нет");
            Assert.AreEqual(0, recording.SavedFileCount, "записей на диск нет");
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Меш каждой части и сколько раз его помечали изменённым: пересборка пишет меш и помечает его.
        private static Dictionary<SplineBuildingPart, (Mesh Mesh, int DirtyCount)> MeshStatesOf(SplineBuilding building)
        {
            var states = new Dictionary<SplineBuildingPart, (Mesh, int)>();

            foreach (var part in building.GetComponentsInChildren<SplineBuildingPart>())
            {
                var mesh = part.GetComponent<MeshFilter>().sharedMesh;
                states[part] = (mesh, EditorUtility.GetDirtyCount(mesh));
            }

            return states;
        }

        private static List<string> RebuiltPartsOf(SplineBuilding building, Dictionary<SplineBuildingPart, (Mesh Mesh, int DirtyCount)> before)
        {
            var rebuilt = new List<string>();

            foreach (var part in building.GetComponentsInChildren<SplineBuildingPart>())
            {
                var mesh = part.GetComponent<MeshFilter>().sharedMesh;

                if (!before.TryGetValue(part, out var state) || state.Mesh != mesh || state.DirtyCount != EditorUtility.GetDirtyCount(mesh))
                    rebuilt.Add(part.Kind + " " + part.Storey);
            }

            return rebuilt;
        }

        private static IEnumerator WaitSeconds(double seconds)
        {
            var until = EditorApplication.timeSinceStartup + seconds;

            while (EditorApplication.timeSinceStartup < until)
                yield return null;
        }
    }
}
