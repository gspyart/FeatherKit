using System.Collections.Generic;
using System.IO;
using System.Linq;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Эталонный уровень (<see cref="LiveBuilderReferenceLevel"/>): в нём всё из ТЗ, два построения подряд дают те же меши,
    /// и меши совпадают с эталоном в файле (<see cref="LiveBuilderMeshDigest"/>), а вид — с эталоном вида (<see cref="LiveBuilderLookDigest"/>).
    /// Эталоны переписывают WriteBaseline и WriteLookBaseline — только запуском по имени.
    /// Уровень строится один раз на все проверки: сборка долгая.
    /// </summary>
    public class LiveBuilderReferenceLevelTests
    {
        // Эталон записан на новых понятиях (этап 2) с формами этапа 6; дальше меняется порядок вершин, но не форма — сверка по форме.
        private const bool IsExactComparison = false;

        private const string BaselineFile = "Tests/Editor/LiveBuilder/Baselines/ReferenceLevel.txt";
        private const string LookBaselineFile = "Tests/Editor/LiveBuilder/Baselines/ReferenceLevelLook.txt";
        private const string WriteBaselinesArgument = "-writeLiveBuilderBaselines";

        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private LiveBuilderReferenceLevelTargets targets;
        private List<string> firstBuildLines;
        private List<string> lines;


        [OneTimeSetUp]
        public void BuildTwice()
        {
            using (new LiveBuilderTestScene())
                firstBuildLines = LiveBuilderMeshDigest.DescribeLevel(LiveBuilderReferenceLevel.Build(Origin).Root);

            testScene = new LiveBuilderTestScene();
            targets = LiveBuilderReferenceLevel.Build(Origin);
            lines = LiveBuilderMeshDigest.DescribeLevel(targets.Root);
        }


        [OneTimeTearDown]
        public void CloseScene()
        {
            testScene.Dispose();
        }


        // ── Состав ───────────────────────────────────────────────────────

        [Test]
        public void Build_HasAllPieces()
        {
            var building = targets.Building;
            Assert.AreEqual(9, building.StoreyCount, "9 этажей");
            Assert.AreEqual(3f, building.StoreyHeight, 0.001f, "по 3 м");
            Assert.IsTrue(building.HasParapet && Mathf.Approximately(building.ParapetHeight, 1f), "парапет 1 м");
            Assert.IsTrue(SplineBuildingStoreys.HasStoreyContainers(building), "этажи — контейнеры");
            Assert.IsTrue(SplineBuildingParts.AreBuilt(building), "части здания собраны");

            var firstStorey = SplineBuildingStoreys.FindContainer(building, 0);
            Assert.IsNotNull(firstStorey, "Storey 1");

            var windows = firstStorey.GetComponentsInChildren<GeometryCutter>().Where(cutter => cutter.transform.parent == firstStorey.transform).ToList();
            Assert.AreEqual(2, windows.Count, "окно на каждой длинной стене");
            Assert.IsTrue(windows.All(window => LiveBuilderArrayCopyPoses.CutterCopiesOf(window).Count == 8 * 9), "8 окон на стену на всех этажах");

            var stairs = targets.Root.GetComponentsInChildren<BoxModelStairs>().Single();
            Assert.IsTrue(stairs.IsSwitchback, "лестница с площадкой");
            Assert.AreEqual(firstStorey.transform, stairs.transform.parent, "лестница — в Storey 1");
            Assert.AreEqual(8, CopiesOf(stairs), "лестница на 8 этажах");

            var shaft = targets.Root.GetComponentsInChildren<BoxModel>().Single(model => model.name == "Elevator Shaft");
            Assert.AreEqual(9, CopiesOf(shaft), "шахта на всех этажах");
            var opening = targets.Root.GetComponentsInChildren<GeometryCutter>().Single(cutter => cutter.name == "Shaft Opening");
            Assert.IsTrue(opening.Scope == GeometryCutterScope.EverythingItTouches && opening.CutTargets == LiveBuilderCutTargets.SlabsOnly, "дыра шахты в перекрытиях");
            Assert.AreEqual(8, LiveBuilderArrayCopyPoses.CutterCopiesOf(opening).Count, "в перекрытиях над 8 этажами");

            var partitions = building.GetComponentsInChildren<SplineWall>();
            Assert.AreEqual(4, partitions.Length, "4 перегородки");
            Assert.IsTrue(partitions.All(wall => wall.transform.parent == firstStorey.transform), "перегородки — в Storey 1");
            Assert.IsTrue(partitions.All(wall => wall.GetComponentsInChildren<GeometryCutter>().Length == 1), "в каждой — дверь");

            var balconies = firstStorey.transform.Find("Balconies");
            Assert.IsNotNull(balconies, "балконы в Storey 1");
            Assert.AreEqual(16, balconies.GetComponentsInChildren<BoxModelPart>().Length, "4 балкона по 4 коробки");
            Assert.AreEqual(9, CopiesOf(balconies.GetComponentInChildren<BoxModelPart>()), "балконы на всех этажах");

            Assert.AreEqual(300, BoxModelRebuilder.GetSolverBoxes(targets.Yard).Count, "двор — 300 коробок");
            Assert.Greater(targets.Yard.BevelSize, 0f, "с фаской");
            Assert.IsTrue(targets.Yard.GetComponentsInChildren<BoxModelPart>().Any(part => BoxModelRamps.IsRamp(part, out _)), "пандус");
            Assert.AreEqual(4, targets.Yard.GetComponentsInChildren<LiveBuilderColumn>().Length, "4 колонны Live Builder");
            Assert.IsTrue(targets.Yard.GetComponentsInChildren<GeometryCutter>()
                .Any(cutter => Mathf.Approximately(cutter.TopCornerRadius, cutter.Width / 2f) && cutter.GetComponentInParent<SplineWall>() != null), "арка в стенке");

            var canopy = targets.Root.GetComponentsInChildren<SplineBuilding>().Single(candidate => candidate != building);
            Assert.AreEqual(1, canopy.StoreyCount, "навес в один этаж");
            Assert.IsNotNull(SplineBuildingGableRoof.FindRoofObject(SplineBuildingStoreys.RoofOf(canopy).transform), "двускатная крыша в Roof");

            // Объект без своих коробок (навес — только здание) меша не строит.
            foreach (var filter in targets.Root.GetComponentsInChildren<MeshFilter>().Where(filter => !IsEmptyGroup(filter)))
                Assert.Greater(filter.sharedMesh != null ? filter.sharedMesh.vertexCount : 0, 0, filter.name + " собран");
        }

        private static bool IsEmptyGroup(MeshFilter filter)
        {
            return filter.TryGetComponent<BoxModel>(out var model) && BoxModelRebuilder.GetSolverBoxes(model).Count == 0;
        }

        private static int CopiesOf(Component repeated)
        {
            return LiveBuilderArrayCopyPoses.CopiesOf(repeated.transform).Count;
        }


        // ── Меши ─────────────────────────────────────────────────────────

        [Test]
        public void BuildTwiceInARow_GivesSameExactDigests()
        {
            var differences = LiveBuilderMeshDigest.Compare(LiveBuilderMeshDigest.Parse(firstBuildLines), LiveBuilderMeshDigest.Parse(lines), true);
            Assert.IsEmpty(differences, string.Join("\n", differences));
        }


        [Test]
        public void Meshes_MatchBaseline()
        {
            var path = BaselinePath();
            Assert.IsTrue(File.Exists(path), "эталона нет — запусти WriteBaseline по имени");

            var baseline = LiveBuilderMeshDigest.Parse(File.ReadAllLines(path));
            var differences = LiveBuilderMeshDigest.Compare(baseline, LiveBuilderMeshDigest.Parse(lines), IsExactComparison);
            Assert.IsEmpty(differences, string.Join("\n", differences));
        }


        [Test, Explicit("Rewrites the mesh baseline of the reference level: run it by name after an intended geometry change.")]
        public void WriteBaseline()
        {
            SkipUnlessWriteRequested();
            var path = BaselinePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, new[] { LiveBuilderMeshDigest.Header }.Concat(lines));
            AssetDatabase.Refresh();
        }

        internal static string BaselinePath()
        {
            return PackageFilePath(BaselineFile);
        }


        // ── Вид ──────────────────────────────────────────────────────────

        // Отпечаток «по виду» — тот же уровень другими словами: правка устройства уровня вид не меняет.
        [Test]
        public void Meshes_LookSameAsBaseline()
        {
            var path = PackageFilePath(LookBaselineFile);
            Assert.IsTrue(File.Exists(path), "эталона вида нет — запусти WriteLookBaseline по имени");

            var differences = LiveBuilderLookDigest.Compare(File.ReadAllLines(path), LiveBuilderLookDigest.Describe(targets.Root));
            Assert.IsEmpty(differences, string.Join("\n", differences));
        }


        [Test, Explicit("Rewrites the look baseline of the reference level: run it by name after an intended change of how the level looks.")]
        public void WriteLookBaseline()
        {
            SkipUnlessWriteRequested();
            var path = PackageFilePath(LookBaselineFile);
            File.WriteAllLines(path, new[] { LiveBuilderLookDigest.Header }.Concat(LiveBuilderLookDigest.Describe(targets.Root)));
            AssetDatabase.Refresh();
        }


        // Unity в фоновом режиме гоняет и [Explicit] в полном прогоне — без флага запуска эталон не пишется, иначе любой прогон затёр бы «как было».
        private static void SkipUnlessWriteRequested()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), WriteBaselinesArgument) < 0)
                Assert.Ignore("Baseline is written only with the " + WriteBaselinesArgument + " command line argument.");
        }


        private static string PackageFilePath(string fileInPackage)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(LiveBuilderReferenceLevelTests).Assembly);
            return Path.Combine(package.resolvedPath, fileInPackage);
        }
    }
}
