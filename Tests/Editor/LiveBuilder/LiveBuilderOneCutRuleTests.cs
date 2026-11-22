using System.Collections.Generic;
using System.Linq;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Одно правило выреза (<see cref="LiveBuilderCutRules"/>) у новых объектов: сначала все добавления, потом все вырезы, порядок в списке не важен;
    /// вложенный объект — граница; свой кусок вырез не режет; «Own object» и «Everything it touches» через здания, группы и префаб;
    /// «Cuts» — все три значения; объект версии 0 режется по-старому; кеш солвера совпадает с эталоном нового правила.
    /// </summary>
    public class LiveBuilderOneCutRuleTests
    {
        private const string TempFolder = "Assets/LiveBuilderOneCutRuleTests";
        private const int RandomSeed = 20261121;

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

            if (AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.DeleteAsset(TempFolder);
        }


        // ── Порядок не важен ─────────────────────────────────────────────

        // Зал 10 × 4 × 10, вырез внутри, колонна посреди выреза — колонна стоит в списке после выреза или перед ним.
        [Test]
        public void AddBox_BeforeOrAfterCut_IsCutTheSame()
        {
            var (model, cut, _) = CreateHallWithColumn(hall => hall.transform);
            Rebuild(model);
            var surfacesWithCutFirst = SurfaceKeysOf(model);

            Assert.IsTrue(IsColumnPlaceOpen(), "колонна после выреза — тоже вырезана");

            cut.transform.SetAsLastSibling();
            Rebuild(model);

            CollectionAssert.AreEquivalent(surfacesWithCutFirst, SurfaceKeysOf(model), "вырез в конце списка — те же поверхности");
            Assert.IsTrue(IsColumnPlaceOpen(), "и колонна вырезана так же");
        }


        [Test]
        public void ColumnInNestedObject_IsNotCut()
        {
            var (model, _, _) = CreateHallWithColumn(hall => BoxModelFactory.CreateGroup(hall.transform).transform);
            Rebuild(model);

            Assert.IsFalse(IsColumnPlaceOpen(), "вложенный объект — граница: колонна цела");
        }

        // Зал: коробка 10 × 4 × 10, вырез на всю ширину по X и Z от 0,5 до 3,5 м; колонна 1 × 3 × 1 посередине — в columnParent.
        private static (BoxModel Model, BoxModelPart Cut, BoxModelPart Column) CreateHallWithColumn(System.Func<BoxModel, Transform> columnParent)
        {
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(10f, 4f, 10f), BoxModelPartMode.Add);
            var cut = BoxModelFactory.CreatePart(model, new Vector3(1f, 0.5f, 1f), new Vector3(9f, 3.5f, 9f), BoxModelPartMode.Cut);
            var column = BoxModelFactory.CreatePart(model, new Vector3(4.5f, 0.5f, 4.5f), new Vector3(5.5f, 3.5f, 5.5f), BoxModelPartMode.Add);
            column.transform.SetParent(columnParent(model), true);

            foreach (var nested in model.GetComponentsInChildren<BoxModel>())
                Rebuild(nested);

            return (model, cut, column);
        }

        // Луч поперёк зала на высоте 2 м через место колонны.
        private static bool IsColumnPlaceOpen()
        {
            return LiveBuilderTestScene.IsOpen(Origin + new Vector3(2f, 2f, 5f), Vector3.right, 6f);
        }

        private static List<string> SurfaceKeysOf(BoxModel model)
        {
            var keys = new List<string>();

            foreach (var surface in BoxModelRebuilder.GetSurfaces(model))
                keys.Add(surface.Face + " " + surface.Plane + " " + surface.Area);

            return keys;
        }


        // ── Свой кусок ───────────────────────────────────────────────────

        // Лестница на 3 м: вырез над ней начинается на 2,1 м, а ступени идут до верха — верхние целы.
        [Test]
        public void StairsCut_KeepsItsOwnTopSteps()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreateStairs(model, Vector3.zero, BoxModelStairsDirection.PositiveX, 1.2f, 3f, 0.3f, true);
            Rebuild(model);

            var boxes = BoxModelRebuilder.GetSolverBoxes(model);
            var shapes = new List<BoxModelPartShape>();
            long stepsVolume = 0;

            foreach (var box in boxes)
            {
                shapes.Add(box.Shape);

                if (box.Mode == BoxModelPartMode.Add)
                    stepsVolume += VolumeOf(box.Box);
            }

            var cells = BoxModelSolver.Solve(shapes, BoxModelCutRule.CutsAfterAllAdds, LiveBuilderCutRules.SolverFilterOf(model, boxes));
            var cellsWithoutFilter = BoxModelSolver.Solve(shapes, BoxModelCutRule.CutsAfterAllAdds);

            Assert.IsTrue(boxes.Any(box => box.Mode == BoxModelPartMode.Cut), "у лестницы есть вырез над ней");
            Assert.AreEqual(stepsVolume, VolumeOf(cells), "все ступени целиком");
            Assert.Less(VolumeOf(cellsWithoutFilter), stepsVolume, "без правила «свой кусок» вырез съел бы верхние ступени");
        }


        // ── Комната ──────────────────────────────────────────────────────

        // Комната 8 × 3 × 6 со стенами: стены вокруг целы, внутри пусто.
        [Test]
        public void RoomShell_StaysAroundItsVoid()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreateRoom(model, Vector3.zero, new Vector3(8f, 3f, 6f), true);
            Rebuild(model);

            Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(4f, 1.5f, -2f), Vector3.forward, 2.5f), "стена комнаты цела");
            Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(4f, 1.5f, 1f), Vector3.forward, 4f), "внутри — пусто");
        }


        // ── Где режет ────────────────────────────────────────────────────

        // Здание 8 × 8 в объекте; вырез идёт из здания наружу сквозь блок объекта рядом со зданием, в здании — коробка интерьера.
        [Test]
        public void CutBoxInBuilding_CutsOnlyInsideIt_InObjectRootCutsAround()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = testScene.CreateBuilding(model, 2, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var floor = building.FirstFloorWorldHeight - Origin.y;
            var interior = SplineBuildingStoreys.ContainerOf(building, 0).transform;

            BoxModelFactory.CreatePart(model, new Vector3(10f, floor, 2f), new Vector3(12f, floor + 3f, 6f), BoxModelPartMode.Add);
            var interiorBox = BoxModelFactory.CreatePart(model, new Vector3(2f, floor, 3.5f), new Vector3(6f, floor + 3f, 4.5f), BoxModelPartMode.Add);
            var cut = BoxModelFactory.CreatePart(model, new Vector3(5f, floor + 0.5f, 3f), new Vector3(12.5f, floor + 2.5f, 5f), BoxModelPartMode.Cut);
            interiorBox.transform.SetParent(interior, true);
            cut.transform.SetParent(interior, true);
            Rebuild(model);

            Assert.IsTrue(IsInteriorBoxOpen(floor), "вырез в здании режет коробку его интерьера");
            Assert.IsFalse(IsOutsideBlockOpen(floor), "и не режет блок объекта снаружи здания");

            cut.transform.SetParent(model.transform, true);
            Rebuild(model);

            Assert.IsTrue(IsInteriorBoxOpen(floor) && IsOutsideBlockOpen(floor), "вырез прямо в объекте режет и то, и другое");
        }

        private static bool IsInteriorBoxOpen(float floor)
        {
            return LiveBuilderTestScene.IsOpen(Origin + new Vector3(5.5f, floor + 1.5f, 2f), Vector3.forward, 3f);
        }

        private static bool IsOutsideBlockOpen(float floor)
        {
            return LiveBuilderTestScene.IsOpen(Origin + new Vector3(13f, floor + 1.5f, 4f), Vector3.left, 3.5f);
        }


        // Дверь-префаб: рама — объект из коробок, рядом вырез «Everything it touches». Свою раму вырез не режет, стену, куда поставили, — режет.
        [Test]
        public void EverythingCutter_InPrefab_SkipsOwnFrame_CutsWall()
        {
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(10f, 0f, 0f));
            var door = new GameObject("Door");
            door.transform.position = Origin + new Vector3(5f, 0f, 0f);
            var frame = BoxModelFactory.CreateGroup(door.transform);
            var cutter = BoxModelFactory.CreateCutter(door.transform, door.transform.position, Quaternion.identity, new Vector2(1f, 2f), 1f);

            Assert.AreEqual(GeometryCutterScope.EverythingItTouches, cutter.Scope, "без постройки над ним — «Everything it touches» сам");
            Assert.IsTrue(LiveBuilderCutRules.CanCutterCut(cutter, frame), "пока не префаб — режет и раму");

            AssetDatabase.CreateFolder("Assets", TempFolder.Substring("Assets/".Length));
            PrefabUtility.SaveAsPrefabAssetAndConnect(door, TempFolder + "/Door.prefab", InteractionMode.AutomatedAction);

            Assert.IsFalse(LiveBuilderCutRules.CanCutterCut(cutter, frame), "в префабе свою раму не режет");
            Assert.IsTrue(LiveBuilderCutRules.CanCutterCut(cutter, wall), "стену снаружи префаба режет");
        }


        // ── Что режет ────────────────────────────────────────────────────

        // Вырезающая коробка поперёк стены по линии и блока объекта: блок — коробка своего объекта, его режут все три значения.
        [Test]
        public void CutsValues_WallByValue_OwnBoxesAlways()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var wall = testScene.CreateWall(model, Vector3.zero, new Vector3(10f, 0f, 0f));
            BoxModelFactory.CreatePart(model, new Vector3(0f, 0f, 4f), new Vector3(6f, 3f, 6f), BoxModelPartMode.Add);
            var cut = BoxModelFactory.CreatePart(model, new Vector3(2f, 0.5f, -1f), new Vector3(3f, 2.5f, 7f), BoxModelPartMode.Cut);

            foreach (var (targets, isWallCut) in new[]
                     {
                         (LiveBuilderCutTargets.Everything, true), (LiveBuilderCutTargets.SlabsOnly, false), (LiveBuilderCutTargets.WallsOnly, true)
                     })
            {
                cut.CutTargets = targets;
                Rebuild(model);
                SplineWallRebuilder.Rebuild(wall);

                Assert.AreEqual(isWallCut, LiveBuilderTestScene.IsOpen(Origin + new Vector3(2.5f, 1.5f, -2f), Vector3.forward, 3f), targets + ": стена");
                Assert.IsTrue(LiveBuilderTestScene.IsOpen(Origin + new Vector3(2.5f, 1.5f, 3f), Vector3.forward, 4f), targets + ": блок объекта");
            }
        }


        // ── Версия данных ────────────────────────────────────────────────

        // Тот же зал с колонной после выреза и перегородка в комнате: у версии 0 — по-старому, у новой — одно правило.
        [Test]
        public void LegacyObject_KeepsOldRule_NewObjectUsesOneRule()
        {
            var (model, _, _) = CreateHallWithColumn(hall => hall.transform);
            var roomModel = BoxModelFactory.CreateModel(Origin + new Vector3(20f, 0f, 0f));
            var partition = testScene.CreateWall(roomModel, new Vector3(21f, 0f, 3f), new Vector3(27f, 0f, 3f));
            BoxModelFactory.CreateRoom(roomModel, Vector3.zero, new Vector3(8f, 3f, 6f), true);

            model.DataVersion = BoxModel.LegacyDataVersion;
            roomModel.DataVersion = BoxModel.LegacyDataVersion;
            RebuildAll(model, roomModel, partition);

            Assert.IsFalse(IsColumnPlaceOpen(), "версия 0: колонна после выреза цела");
            Assert.IsFalse(IsPartitionOpen(), "версия 0: пустота комнаты перегородку не режет");

            model.DataVersion = BoxModel.CurrentDataVersion;
            roomModel.DataVersion = BoxModel.CurrentDataVersion;
            RebuildAll(model, roomModel, partition);

            Assert.IsTrue(IsColumnPlaceOpen(), "новая версия: колонна вырезана");
            Assert.IsTrue(IsPartitionOpen(), "новая версия: перегородка в комнате вырезана");
        }

        private static void RebuildAll(BoxModel model, BoxModel roomModel, SplineWall partition)
        {
            Rebuild(model);
            Rebuild(roomModel);
            SplineWallRebuilder.Rebuild(partition);
        }

        private static bool IsPartitionOpen()
        {
            return LiveBuilderTestScene.IsOpen(Origin + new Vector3(24f, 1.5f, 1.5f), Vector3.forward, 3f);
        }


        // ── Кеш солвера ──────────────────────────────────────────────────

        // Случайные коробки с метками «кусок» и «постройка»; фильтр — как у правила: не свой кусок и постройка выреза над коробкой.
        [Test]
        public void SolverCache_MatchesReference_WithCutFilter()
        {
            var random = new System.Random(RandomSeed);
            var parts = new List<BoxModelPartShape>();
            var pieces = new List<int>();
            var constructions = new List<int>();
            var cache = new BoxModelSolverCache();

            for (var index = 0; index < 40; index++)
                AddRandomPart(random, parts, pieces, constructions);

            BoxModelCutFilter canCut = (cutIndex, addIndex) =>
                pieces[cutIndex] != pieces[addIndex] && (constructions[cutIndex] == 0 || constructions[cutIndex] == constructions[addIndex]);

            for (var edit = 0; edit < 200; edit++)
            {
                var replaced = random.Next(parts.Count);
                parts.RemoveAt(replaced);
                pieces.RemoveAt(replaced);
                constructions.RemoveAt(replaced);
                AddRandomPart(random, parts, pieces, constructions);

                var expected = BoxModelSolver.Solve(parts, BoxModelCutRule.CutsAfterAllAdds, canCut);
                var actual = cache.Solve(parts, BoxModelCutRule.CutsAfterAllAdds, canCut);
                AssertSameCells(expected, actual, "правка " + edit);
            }
        }

        // Кусок — один из пяти, постройка — 0 (сам объект) или 1, 2 (здания в нём).
        private static void AddRandomPart(System.Random random, List<BoxModelPartShape> parts, List<int> pieces, List<int> constructions)
        {
            var min = new Vector3Int(random.Next(20), random.Next(10), random.Next(20)) * 100;
            var size = new Vector3Int(random.Next(1, 8), random.Next(1, 6), random.Next(1, 8)) * 100;
            var mode = random.Next(3) == 0 ? BoxModelPartMode.Cut : BoxModelPartMode.Add;
            var place = random.Next(parts.Count + 1);
            parts.Insert(place, new BoxModelPartShape(new MillimeterBox(min, min + size), mode));
            pieces.Insert(place, random.Next(5));
            constructions.Insert(place, random.Next(3));
        }

        private static void AssertSameCells(List<BoxModelCell> expected, List<BoxModelCell> actual, string where)
        {
            Assert.AreEqual(expected.Count, actual.Count, where + ": число кусков");

            for (var index = 0; index < expected.Count; index++)
            {
                Assert.AreEqual(expected[index].Box.Min, actual[index].Box.Min, where + ": кусок " + index);
                Assert.AreEqual(expected[index].Box.Max, actual[index].Box.Max, where + ": кусок " + index);
                CollectionAssert.AreEqual(expected[index].FaceSources, actual[index].FaceSources, where + ": грани куска " + index);
            }
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static void Rebuild(BoxModel model)
        {
            BoxModelRebuilder.ForgetCachedResults();
            BoxModelRebuilder.Rebuild(model);
        }


        private static long VolumeOf(MillimeterBox box)
        {
            var size = box.Max - box.Min;
            return (long)size.x * size.y * size.z;
        }

        private static long VolumeOf(List<BoxModelCell> cells)
        {
            long volume = 0;

            foreach (var cell in cells)
                volume += VolumeOf(cell.Box);

            return volume;
        }
    }
}
