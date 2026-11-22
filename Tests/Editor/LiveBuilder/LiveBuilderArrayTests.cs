using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Повтор (<see cref="LiveBuilderArray"/>): копии по осям и вложенные повторы, предел копий, разбор пропусков, «до верха здания»;
    /// копии — часть результата: коробки, вырезы, стены и вложенные объекты строятся в каждой копии, а сдвиг коробки пересчитывает только её копии.
    /// </summary>
    public class LiveBuilderArrayTests
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


        // ── Число и шаг ──────────────────────────────────────────────────

        [Test]
        public void CountAndStep_PlaceCopiesAlongAxes()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var box = BoxModelFactory.CreatePart(model, Vector3.zero, Vector3.one, BoxModelPartMode.Add);
            var array = AddArray(box.gameObject, new Vector3Int(3, 2, 1), new Vector3(2f, 5f, 0f));

            var mins = CollectMins(model);

            Assert.AreEqual(6, mins.Count, "3 по X × 2 по Y");

            foreach (var expected in new[] { new Vector3(0f, 0f, 0f), new Vector3(4f, 0f, 0f), new Vector3(2f, 5f, 0f), new Vector3(4f, 5f, 0f) })
                Assert.IsTrue(mins.Any(min => Vector3.Distance(min, expected) < 0.001f), "копия в " + expected);

            array.SkipY = "1";
            Assert.AreEqual(3, CollectMins(model).Count, "Skip Y 1 убирает ряд оригинала");
            Assert.IsFalse(CollectMins(model).Any(min => min.y < 0.001f), "на месте оригинала копии нет");
        }


        [Test]
        public void NestedArrays_Multiply()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var row = CreateContainer(model.transform, "Row");
            AddArray(row.gameObject, new Vector3Int(1, 4, 1), new Vector3(0f, 3f, 0f));

            var box = BoxModelFactory.CreatePart(model, Vector3.zero, Vector3.one, BoxModelPartMode.Add);
            box.transform.SetParent(row, true);
            AddArray(box.gameObject, new Vector3Int(3, 1, 1), new Vector3(2f, 0f, 0f));

            Assert.AreEqual(12, CollectMins(model).Count, "4 ряда × 3 копии");
            Assert.AreEqual(12, LiveBuilderArrayCopyPoses.CopiesOf(box.transform).Count, "копии узла — те же 12");
        }


        [Test]
        public void OverLimit_BuildsLimitAndWarns()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var box = BoxModelFactory.CreatePart(model, Vector3.zero, Vector3.one, BoxModelPartMode.Add);
            var array = AddArray(box.gameObject, new Vector3Int(100, 1, 100), new Vector3(2f, 0f, 2f));

            LogAssert.Expect(LogType.Warning, new Regex("only " + LiveBuilderArray.MaxCopies + " are built"));
            Assert.AreEqual(LiveBuilderArray.MaxCopies, LiveBuilderArrayCopyPoses.CopiesOf(box.transform).Count, "копий — не больше предела");
            Assert.IsTrue(LiveBuilderArrayCopyPoses.WarningsOf(array).Any(warning => warning.StartsWith("Too many copies")), "у повтора — предупреждение");

            LogAssert.Expect(LogType.Warning, new Regex("boxes counting repeats"));
            Assert.AreEqual(LiveBuilderArray.MaxCopies, CollectMins(model).Count, "сборщик строит не больше предела");
        }


        // ── Пропуски ─────────────────────────────────────────────────────

        [Test]
        public void SkipList_ReadsNumbersRangesAndLast()
        {
            Assert.IsTrue(LiveBuilderArraySkipList.TryParse("1, 5", 9, out var numbers, out _));
            CollectionAssert.AreEquivalent(new[] { 1, 5 }, numbers);

            Assert.IsTrue(LiveBuilderArraySkipList.TryParse("2-4", 9, out var range, out _));
            CollectionAssert.AreEquivalent(new[] { 2, 3, 4 }, range);

            Assert.IsTrue(LiveBuilderArraySkipList.TryParse("last", 7, out var last, out _));
            CollectionAssert.AreEquivalent(new[] { 7 }, last);

            Assert.IsTrue(LiveBuilderArraySkipList.TryParse("1, last", 3, out var both, out _));
            CollectionAssert.AreEquivalent(new[] { 1, 3 }, both);

            Assert.IsFalse(LiveBuilderArraySkipList.TryParse("2, x", 9, out _, out var error), "мусор — ошибка");
            StringAssert.Contains("'x'", error);
        }


        [Test]
        public void CountYToBuildingTop_FollowsStoreysAndSkipsLast()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = testScene.CreateBuilding(model, 4, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var box = BoxModelFactory.CreatePart(model, new Vector3(2f, building.FloorTopHeight, 2f), new Vector3(3f, building.FloorTopHeight + 1f, 3f),
                BoxModelPartMode.Add);
            box.transform.SetParent(building.transform, true);

            var array = box.gameObject.AddComponent<LiveBuilderArray>();
            array.CountYToBuildingTop = true;

            var mins = CollectMins(model);
            Assert.AreEqual(4, mins.Count, "на каждом этаже");
            Assert.IsTrue(mins.Any(min => Mathf.Abs(min.y - (building.FloorTopHeight + 3f * building.StoreyHeight)) < 0.001f), "шаг — высота этажа здания");

            array.SkipY = "last";
            Assert.AreEqual(3, CollectMins(model).Count, "без верхнего этажа");

            building.StoreyCount = 6;
            Assert.AreEqual(5, CollectMins(model).Count, "добавил этажи — добавились копии");
        }


        // ── Копии вырезов ────────────────────────────────────────────────

        [Test]
        public void CutterCopies_CutWall()
        {
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(12f, 0f, 0f));
            var window = CreateWindow(wall, 1f, 1.5f, 1.5f);
            AddArray(window.gameObject, new Vector3Int(3, 1, 1), new Vector3(4f, 0f, 0f));
            SplineWallRebuilder.Rebuild(wall);

            for (var copy = 0; copy < 3; copy++)
            {
                var center = CenterOf(window) + window.transform.right * (4f * copy);
                Assert.IsTrue(IsOpenThrough(center), "дыра в копии " + (copy + 1));
                Assert.IsFalse(IsOpenThrough(center + window.transform.right * 2f), "простенок после копии " + (copy + 1));
            }
        }


        [Test]
        public void CutterCopies_CutBuildingOnEveryStorey()
        {
            var building = testScene.CreateBuilding(3, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var paths = SplineWallPath.FromContainer(building.GetComponent<SplineContainer>(), building.CurveStep);
            var firstFloor = building.transform.InverseTransformPoint(new Vector3(0f, building.FirstFloorWorldHeight, 0f)).y;
            LiveBuilderOpeningPlacer.FitCutterToWall(building, paths[0], 1.5f, 1.5f, firstFloor + 1f, out var bottom, out var rotation, out var depth);

            var window = BoxModelFactory.CreateCutter(building.transform, building.transform.TransformPoint(bottom), building.transform.rotation * rotation,
                new Vector2(1.5f, 1.5f), depth);
            window.FollowsWallLine = true;
            var array = AddArray(window.gameObject, new Vector3Int(2, 1, 1), new Vector3(3f, 0f, 0f));
            array.CountYToBuildingTop = true;
            array.StepYIsStoreyHeight = true;
            SplineBuildingRebuilder.Rebuild(building);

            for (var storey = 0; storey < 3; storey++)
                for (var copy = 0; copy < 2; copy++)
                {
                    var center = CenterOf(window) + window.transform.right * (3f * copy) + Vector3.up * (storey * building.StoreyHeight);
                    Assert.IsTrue(IsOpenThrough(center, 2.4f), "дыра: этаж " + (storey + 1) + ", копия " + (copy + 1));
                }

            Assert.IsFalse(IsOpenThrough(CenterOf(window) + window.transform.right * 1.5f, 2.4f), "простенок между копиями");
        }


        // Вырез-коробка объекта под повтором: копии режут и сам объект клетками, и стену в нём — одни и те же копии.
        [Test]
        public void CutBoxCopies_CutModelAndWallAlike()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(model, new Vector3(0f, 0f, 3f), new Vector3(10f, 3f, 4f), BoxModelPartMode.Add);
            var wall = testScene.CreateWall(model, Vector3.zero, new Vector3(10f, 0f, 0f));

            var row = CreateContainer(model.transform, "Openings");
            AddArray(row.gameObject, new Vector3Int(3, 1, 1), new Vector3(3f, 0f, 0f));
            var cutter = BoxModelFactory.CreateCutterBox(model, new Vector3(1f, 0.5f, -1f), new Vector3(2f, 2f, 5f));
            cutter.transform.SetParent(row, true);

            BoxModelRebuilder.Rebuild(model);
            SplineWallRebuilder.Rebuild(wall);

            for (var copy = 0; copy < 3; copy++)
            {
                var x = 1.5f + 3f * copy;
                Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(x, 1.25f, -2f), Vector3.forward, 7f), "стена и коробка прорезаны в копии " + (copy + 1));
            }

            Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(3f, 1.25f, -2f), Vector3.forward, 7f), "между копиями — целые");
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(3f, 1.25f, 2.5f), Vector3.forward, 2f), "коробка между копиями цела");
        }


        // Повтор над вырезами копирует их одинаково — и клетками солвера, и вычитанием.
        [Test]
        public void RepeatAroundCutters_CopiesAlikeInSolverAndSubtraction()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(10f, 3f, 1f), BoxModelPartMode.Add);
            var repeat = new GameObject("Repeat").transform;
            repeat.SetParent(model.transform, false);
            var array = repeat.gameObject.AddComponent<LiveBuilderArray>();
            array.CountX = 3;
            array.StepX = 3f;
            array.CountY = 1;

            var preciseCutter = BoxModelFactory.CreateCutterBox(model, new Vector3(1f, 2f, -1f), new Vector3(2f, 2.8f, 2f));
            preciseCutter.transform.SetParent(repeat, true);

            var turnedCutter = BoxModelFactory.CreateCutter(repeat, Origin + new Vector3(1.5f, 0.5f, 0.5f), Quaternion.Euler(0f, 30f, 0f), Vector2.one, 3f);
            BoxModelRebuilder.Rebuild(model);

            Assert.AreEqual(3, GeometryCutterCopy.AllOf(turnedCutter).Count, "копии повернутого выреза — по повтору над ним");

            for (var copy = 0; copy < 3; copy++)
            {
                var x = 1.5f + 3f * copy;
                Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(x, 2.4f, -2f), Vector3.forward, 4f), "клетки солвера, копия " + (copy + 1));
                Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(x, 1f, -2f), Vector3.forward, 4f), "вычитание, копия " + (copy + 1));
            }

            Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(3f, 1f, -2f), Vector3.forward, 4f), "между копиями — цело");
        }


        // ── Копии стен и объектов ────────────────────────────────────────

        [Test]
        public void WallCopies_BuiltInOneMesh()
        {
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(10f, 0f, 0f));
            var row = CreateContainer(null, "Walls");
            row.position = Origin;
            wall.transform.SetParent(row, true);
            AddArray(row.gameObject, new Vector3Int(1, 1, 3), new Vector3(0f, 0f, 5f));
            SplineWallRebuilder.Rebuild(wall);

            for (var copy = 0; copy < 3; copy++)
                Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(5f, 1.5f, 5f * copy - 2f), Vector3.forward, 4f), "стена в копии " + (copy + 1));

            Assert.That(wall.GetComponent<MeshFilter>().sharedMesh.bounds.size.z, Is.EqualTo(10.3f).Within(0.01f), "все копии — в одном меше стены");
        }


        [Test]
        public void BuildingCopies_BuiltInItsParts()
        {
            var building = testScene.CreateBuilding(2, Vector3.zero, new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 4f), new Vector3(0f, 0f, 4f));
            var row = CreateContainer(null, "Buildings");
            row.position = Origin;
            building.transform.SetParent(row, true);
            AddArray(row.gameObject, new Vector3Int(2, 1, 1), new Vector3(8f, 0f, 0f));
            SplineBuildingRebuilder.Rebuild(building);

            foreach (var x in new[] { 2f, 10f })
                for (var storey = 0; storey < 2; storey++)
                    Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(x, 2f + storey * 4f, -2f), Vector3.forward, 2.4f),
                        "стена здания в копии на " + x + " м, этаж " + (storey + 1));

            Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(6f, 2f, -2f), Vector3.forward, 2.4f), "между копиями пусто");
        }


        [Test]
        public void DoorInWallUnderArray_CopiedWithWall()
        {
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(4f, 0f, 0f));
            var row = CreateContainer(null, "Rooms");
            row.position = Origin;
            wall.transform.SetParent(row, true);
            var door = CreateWindow(wall, 1.5f, 1f, 2f);
            AddArray(row.gameObject, new Vector3Int(2, 1, 1), new Vector3(6f, 0f, 0f));
            SplineWallRebuilder.Rebuild(wall);

            foreach (var x in new[] { 0f, 6f })
            {
                Assert.IsTrue(IsOpenThrough(CenterOf(door) + new Vector3(x, 0f, 0f)), "дверь в копии со сдвигом " + x);
                Assert.IsFalse(IsOpenThrough(CenterOf(door) + new Vector3(x + 1f, 0f, 0f)), "стена рядом с дверью, сдвиг " + x);
            }
        }


        // Шахта: вложенный объект под повтором — все копии одним мешем, между этажами нет крышек.
        [Test]
        public void NestedModelCopies_OneMeshWithoutGaps()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var shaft = BoxModelFactory.CreateGroup(model.transform);
            BoxModelFactory.CreatePart(shaft, Vector3.zero, new Vector3(2f, 4f, 2f), BoxModelPartMode.Add);
            AddArray(shaft.gameObject, new Vector3Int(1, 3, 1), new Vector3(0f, 4f, 0f));
            BoxModelRebuilder.Rebuild(shaft);

            var mesh = shaft.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.bounds.size.y, Is.EqualTo(12f).Within(0.001f), "три этажа шахты — в одном меше");

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            for (var index = 0; index < triangles.Length; index += 3)
            {
                var heights = new[] { vertices[triangles[index]].y, vertices[triangles[index + 1]].y, vertices[triangles[index + 2]].y };
                var isInnerCap = heights.All(height => Mathf.Abs(height - heights[0]) < 0.001f) && heights[0] > 0.01f && heights[0] < 11.99f;
                Assert.IsFalse(isInnerCap, "крышка внутри шахты на высоте " + heights[0]);
            }
        }


        // ── Кеш солвера ──────────────────────────────────────────────────

        [Test]
        public void MovingOneArrayedBox_RecomputesOnlyItsCopies()
        {
            var model = BoxModelFactory.CreateModel(Origin);

            for (var index = 0; index < 5; index++)
                BoxModelFactory.CreatePart(model, new Vector3(index * 3f, 0f, 0f), new Vector3(index * 3f + 1f, 1f, 1f), BoxModelPartMode.Add);

            var arrayed = BoxModelFactory.CreatePart(model, new Vector3(0f, 0f, 5f), new Vector3(1f, 1f, 6f), BoxModelPartMode.Add);
            AddArray(arrayed.gameObject, new Vector3Int(4, 1, 1), new Vector3(3f, 0f, 0f));

            var cache = new BoxModelSolverCache();
            Solve(cache, model);
            Assert.AreEqual(9, cache.RecomputedBoxCount, "первый расчёт — все коробки");

            arrayed.transform.localPosition += new Vector3(0f, 0.5f, 0f);
            Solve(cache, model);
            Assert.AreEqual(4, cache.RecomputedBoxCount, "сдвиг — только четыре копии этой коробки");
        }

        private static void Solve(BoxModelSolverCache cache, BoxModel model)
        {
            var boxes = BoxModelShapeCollector.Collect(model, new List<BoxModelRampShape>());
            cache.Solve(boxes.ConvertAll(box => box.Shape), LiveBuilderCutRules.RuleOf(model), LiveBuilderCutRules.SolverFilterOf(model, boxes));
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Повтор со своими числами; шаг Y — свой, а не высота этажа.
        private static LiveBuilderArray AddArray(GameObject target, Vector3Int counts, Vector3 steps)
        {
            var array = target.AddComponent<LiveBuilderArray>();
            array.CountX = counts.x;
            array.CountY = counts.y;
            array.CountZ = counts.z;
            array.StepX = steps.x;
            array.StepY = steps.y;
            array.StepZ = steps.z;
            array.StepYIsStoreyHeight = false;
            return array;
        }

        private static Transform CreateContainer(Transform parent, string name)
        {
            var container = new GameObject(name).transform;
            container.SetParent(parent, false);
            return container;
        }

        // Углы коробок расчёта объекта в его осях, м.
        private static List<Vector3> CollectMins(BoxModel model)
        {
            return BoxModelShapeCollector.Collect(model, new List<BoxModelRampShape>()).ConvertAll(box => box.Box.MinInMeters);
        }

        // Окно, как ставит «Проём»: вдоль стены от alongFrom, низ на 0,5 м над линией, насквозь.
        private static GeometryCutter CreateWindow(SplineWall wall, float alongFrom, float width, float height)
        {
            var paths = SplineWallPath.FromWall(wall);
            LiveBuilderOpeningPlacer.FitCutterToWall(wall, paths[0], alongFrom, width, 0.5f, out var bottom, out var rotation, out var depth);

            var window = BoxModelFactory.CreateCutter(wall.transform, wall.transform.TransformPoint(bottom), wall.transform.rotation * rotation, new Vector2(width, height), depth);
            window.FollowsWallLine = true;
            return window;
        }

        // Середина дыры выреза в мире.
        private static Vector3 CenterOf(GeometryCutter cutter)
        {
            return GeometryCutterOutline.ShapeToWorld(cutter).MultiplyPoint3x4(new Vector3(0f, cutter.Height / 2f, 0f));
        }

        // Луч поперёк стены вдоль Z мира сквозь точку: стена — в плоскости z = 0 от начала координат сцены.
        private static bool IsOpenThrough(Vector3 point, float distance = 4f)
        {
            return LiveBuilderTestScene.IsOpen(new Vector3(point.x, point.y, Origin.z - 2f), Vector3.forward, distance);
        }
    }
}
