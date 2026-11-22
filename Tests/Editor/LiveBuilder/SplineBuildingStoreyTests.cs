using System.Linq;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Этажи уровня и здания в иерархии (<see cref="SplineBuildingStoreys"/>): этажи «Storey N» и «Roof» на своей высоте и с номерами уровня,
    /// сверка этажей (<see cref="SplineBuildingStoreySync"/>) — высоты содержимого, пропавший и вернувшийся этаж, крыша;
    /// сверка уровня (<see cref="SplineBuildingLevelSync"/>) — высота уровня, этаж уровня, «свободное» здание; повтор «И этажи выше» в этаже.
    /// </summary>
    public class SplineBuildingStoreyTests
    {
        private const float LevelStoreyHeight = 4f;

        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;


        [SetUp]
        public void OpenScene()
        {
            testScene = new LiveBuilderTestScene();
            var storeys = LiveBuilderLevelStoreysInScene.FindOrCreate(LevelStoreyHeight, Origin.y, "Test Level Storeys");
            storeys.StoreyHeight = LevelStoreyHeight;
            storeys.GroundLevel = Origin.y;
        }


        [TearDown]
        public void CloseScene()
        {
            testScene.Dispose();
        }


        // ── Этажи на месте ───────────────────────────────────────────────

        // Здание на втором этаже уровня: этажи «Storey 2»…«Storey 4» на полах своих этажей, крыша — на верху; части в осях здания.
        [Test]
        public void Storeys_StandOnFloors_NamedByLevel_PartsKeepBuildingAxes()
        {
            var building = CreateBuilding(3, LevelStoreyHeight);
            SplineBuildingStoreySync.Sync(building);

            Assert.IsTrue(building.LevelPlacement.IsOnLevelStorey, "стоит на этаже уровня");
            Assert.AreEqual(1, building.LevelPlacement.LevelStorey, "на втором");

            for (var storey = 0; storey < 3; storey++)
            {
                var container = SplineBuildingStoreys.FindContainer(building, storey);
                Assert.IsNotNull(container, "этаж " + storey);
                Assert.AreEqual("Storey " + (storey + 2), container.name, "имя по нумерации уровня");
                Assert.AreEqual(building.FirstFloorWorldHeight + storey * building.StoreyHeight, container.transform.position.y, 0.001f, "на полу этажа");
            }

            var roof = SplineBuildingStoreys.FindRoof(building);
            Assert.IsNotNull(roof, "есть «Roof»");
            Assert.AreEqual(SplineBuildingStoreys.RoofName, roof.name);
            Assert.AreEqual(building.FirstFloorWorldHeight + 3 * building.StoreyHeight, roof.transform.position.y, 0.001f, "на верху крыши");

            foreach (var part in building.GetComponentsInChildren<SplineBuildingPart>())
            {
                Assert.IsNotNull(part.transform.parent.GetComponent<SplineBuildingStorey>(), part.name + " — в этаже");
                Assert.IsTrue(IsSameMatrix(part.transform.localToWorldMatrix, building.transform.localToWorldMatrix), part.name + " — в осях здания");
            }

            Assert.AreEqual("Slab", SplineBuildingParts.Find(building, SplineBuildingPartKind.Slab, 1).name, "перекрытие — «Slab»");
        }


        [Test]
        public void BasementBuilding_NamesStoreysWithoutZero()
        {
            var building = CreateBuilding(2, -LevelStoreyHeight);
            SplineBuildingStoreySync.Sync(building);

            Assert.AreEqual("Storey -1", SplineBuildingStoreys.FindContainer(building, 0).name, "подвал");
            Assert.AreEqual("Storey 1", SplineBuildingStoreys.FindContainer(building, 1).name, "над ним — первый, нулевого нет");
        }


        // ── Высота этажа ─────────────────────────────────────────────────

        // Своя высота 4 → 5: этажи и интерьер уехали, «до потолка» и «весь этаж» подогнались, своя высота осталась.
        [Test]
        public void StoreyHeightChange_MovesStoreysAndFitsContent()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = CreateBuilding(model, 3, 0f);
            building.LevelPlacement.UsesLevelStoreyHeight = false;
            SplineBuildingStoreySync.Sync(building);

            var storey = SplineBuildingStoreys.FindContainer(building, 1).transform;
            var floor = storey.position;
            var partition = CreateWallIn(storey, SplineWallHeightInBuilding.ToCeiling);
            var fullWall = CreateWallIn(storey, SplineWallHeightInBuilding.FullStorey);
            var ownWall = CreateWallIn(storey, SplineWallHeightInBuilding.Custom);
            var stairs = BoxModelFactory.CreateStairs(model, Vector3.zero, BoxModelStairsDirection.PositiveZ, 1.2f, building.StoreyHeight, 0.3f, true);
            var fullBox = BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(1f, building.StoreyHeight, 1f), BoxModelPartMode.Add);
            var lowBox = BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(1f, 1f, 1f), BoxModelPartMode.Add);
            var room = BoxModelFactory.CreateRoom(model, Vector3.zero, new Vector3(3f, building.ClearHeight, 3f), true);

            foreach (var node in new Component[] { stairs, fullBox, lowBox, room })
            {
                node.transform.SetParent(storey, false);
                node.transform.localPosition = new Vector3(1f, 0f, 1f);
            }

            building.StoreyHeight = 5f;
            SplineBuildingStoreySync.Sync(building);

            Assert.AreEqual(floor.y + 1f, storey.position.y, 0.001f, "этаж 2 поднялся на разницу этажа 1");
            Assert.AreEqual(storey.position.y, stairs.transform.position.y, 0.001f, "лестница — с этажом");
            Assert.AreEqual(building.ClearHeight, partition.Height, 0.001f, "перегородка до потолка");
            Assert.AreEqual(5f, fullWall.Height, 0.001f, "стена на весь этаж");
            Assert.AreEqual(2f, ownWall.Height, 0.001f, "своя высота стены осталась");
            Assert.AreEqual(5f, stairs.Height, 0.001f, "лестница во весь этаж");
            Assert.AreEqual(5f, fullBox.Size.y, 0.001f, "коробка во весь этаж");
            Assert.AreEqual(1f, lowBox.Size.y, 0.001f, "низкая коробка — своя высота");
            Assert.AreEqual(building.ClearHeight, room.Size.y, 0.001f, "комната до потолка");
            Assert.IsFalse(building.LevelPlacement.UsesLevelStoreyHeight, "высота здания — своя");
        }

        private static SplineWall CreateWallIn(Transform storey, SplineWallHeightInBuilding heightInBuilding)
        {
            var wall = BoxModelFactory.CreateWall(null, storey.position, storey.rotation);
            wall.transform.SetParent(storey, true);
            wall.Height = 2f;
            wall.HeightInBuilding = heightInBuilding;
            return wall;
        }


        // ── Пропавший этаж ───────────────────────────────────────────────

        // 4 → 2: этаж 4 с коробкой выключен, пустой этаж 3 убран; 2 → 4 — этаж 4 включён, этаж 3 снова есть.
        [Test]
        public void FewerStoreys_HideFilledStorey_RemoveEmpty_AndBack()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = CreateBuilding(model, 4, 0f);
            SplineBuildingRebuilder.Rebuild(building);
            SplineBuildingStoreySync.Sync(building);

            var top = SplineBuildingStoreys.FindContainer(building, 3);
            var box = BoxModelFactory.CreatePart(model, Vector3.zero, Vector3.one, BoxModelPartMode.Add);
            box.transform.SetParent(top.transform, false);

            building.StoreyCount = 2;
            SplineBuildingStoreySync.Sync(building);
            SplineBuildingRebuilder.Rebuild(building);

            Assert.IsFalse(top.gameObject.activeSelf, "этаж 4 с коробкой выключен");
            Assert.IsTrue(top.IsHiddenByMissingStorey, "выключила сверка, а не человек");
            Assert.IsNull(SplineBuildingStoreys.FindContainer(building, 2), "пустой этаж 3 убран");
            Assert.IsNull(top.GetComponentInChildren<SplineBuildingPart>(true), "части пропавшего этажа убраны");

            building.StoreyCount = 4;
            SplineBuildingStoreySync.Sync(building);
            SplineBuildingRebuilder.Rebuild(building);

            Assert.IsTrue(top.gameObject.activeSelf, "этаж 4 вернулся");
            Assert.IsNotNull(SplineBuildingStoreys.FindContainer(building, 2), "этаж 3 снова есть");
            Assert.IsTrue(SplineBuildingParts.AreBuilt(building), "части всех этажей собраны");
        }


        // ── Крыша ────────────────────────────────────────────────────────

        [Test]
        public void Roof_FollowsRoofTop()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = CreateBuilding(model, 2, 0f);
            SplineBuildingStoreySync.Sync(building);

            var hut = BoxModelFactory.CreatePart(model, Vector3.zero, Vector3.one, BoxModelPartMode.Add);
            hut.transform.position = new Vector3(Origin.x + 2f, building.FirstFloorWorldHeight + 2f * building.StoreyHeight, Origin.z + 2f);
            SplineBuildingInteriorPlacement.PutOnRoof(hut.transform, building, "Test");
            Assert.AreEqual(SplineBuildingStoreys.FindRoof(building).transform, hut.transform.parent, "будка — в «Roof»");

            var heightBefore = hut.transform.position.y;
            building.StoreyCount = 3;
            SplineBuildingStoreySync.Sync(building);

            Assert.AreEqual(heightBefore + building.StoreyHeight, hut.transform.position.y, 0.001f, "добавили этаж — поднялась с крышей");
        }


        // ── Уровень ──────────────────────────────────────────────────────

        // Высота этажа уровня 4 → 5: здание с высотой уровня перестроилось и встало на новый пол своего этажа; здание со своей высотой не тронуто.
        [Test]
        public void LevelStoreyHeightChange_UpdatesLevelBuilding_KeepsOwnOne()
        {
            var levelBuilding = CreateBuilding(2, LevelStoreyHeight);
            var ownBuilding = CreateBuilding(2, 0.3f, new Vector3(30f, 0f, 0f));
            ownBuilding.StoreyHeight = 3.5f;
            SplineBuildingLevelSync.SyncAll();

            Assert.IsFalse(ownBuilding.LevelPlacement.IsOnLevelStorey, "не на полу этажа — свободное");
            Assert.IsFalse(ownBuilding.LevelPlacement.UsesLevelStoreyHeight, "поменяли высоту у здания — своя");

            var ownPosition = ownBuilding.transform.position;
            LiveBuilderStoreys.StoreyHeight = 5f;
            SplineBuildingLevelSync.SyncAll();

            Assert.AreEqual(5f, levelBuilding.StoreyHeight, 0.001f, "высота уровня");
            Assert.AreEqual(Origin.y + 5f, levelBuilding.transform.position.y, 0.001f, "низ — на новом полу второго этажа");
            Assert.AreEqual(3.5f, ownBuilding.StoreyHeight, 0.001f, "своя высота осталась");
            Assert.AreEqual(ownPosition, ownBuilding.transform.position, "свободное не двигалось");
        }


        // Сдвинул здание руками на пол третьего этажа — стоит на нём; между полами — свободное.
        [Test]
        public void MovedByHand_TakesNewStoreyOrBecomesFree()
        {
            var building = CreateBuilding(2, 0f);
            building.transform.position += Vector3.up * (2f * LevelStoreyHeight);
            SplineBuildingLevelSync.Sync(building);

            Assert.IsTrue(building.LevelPlacement.IsOnLevelStorey, "на полу этажа — на этаже");
            Assert.AreEqual(2, building.LevelPlacement.LevelStorey, "третий этаж уровня");

            building.transform.position += Vector3.up * 1.3f;
            SplineBuildingLevelSync.Sync(building);

            Assert.IsFalse(building.LevelPlacement.IsOnLevelStorey, "между полами — свободное");
        }


        // ── Повтор по этажам ─────────────────────────────────────────────

        // Балкон «И этажи выше» в первом этаже: повтор до верха — на всех этажах; число Y больше этажей — копии выше крыши не встают.
        [Test]
        public void BalconyOnAllStoreys_InFirstStorey_RepeatsOnEveryStorey()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = CreateBuilding(model, 3, 0f);
            var balcony = BoxModelFactory.CreatePart(model, new Vector3(2f, building.FloorTopHeight - 0.2f, -1.2f), new Vector3(4f, building.FloorTopHeight, 0f),
                BoxModelPartMode.Add);
            SplineBuildingInteriorPlacement.PutOnStorey(balcony.transform, building, true, false, "Test");

            Assert.AreEqual(SplineBuildingStoreys.FindContainer(building, 0).transform, balcony.transform.parent, "в первом этаже");
            var array = balcony.GetComponent<LiveBuilderArray>();
            Assert.IsNotNull(array, "повтор вместо привязки");
            Assert.IsTrue(array.CountYToBuildingTop, "до верха здания");

            var copies = LiveBuilderArrayCopyPoses.CopiesOf(balcony.transform);
            Assert.AreEqual(3, copies.Count, "на всех трёх этажах");
            Assert.AreEqual(2f * building.StoreyHeight, copies.Max(copy => copy.y), 0.001f, "шаг — этаж здания");

            array.CountYToBuildingTop = false;
            array.CountY = 10;
            Assert.AreEqual(3, LiveBuilderArrayCopyPoses.CopiesOf(balcony.transform).Count, "выше верхнего этажа копий нет");

            array.CountYToBuildingTop = true;
            BoxModelRebuilder.Rebuild(model);
            Physics.SyncTransforms();
            var topSlab = new Vector3(Origin.x + 3f, building.FirstFloorWorldHeight + 2f * building.StoreyHeight + 1f, Origin.z - 0.6f);
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(topSlab, Vector3.down, 1.5f), "плита балкона на третьем этаже");
        }


        // Окно на стене здания с «И этажи выше», как ставит «Проём»: в своём этаже с повтором, режет стену на всех этажах, место на линии держит.
        [Test]
        public void WindowOnAllStoreys_LandsInStorey_CutsEveryStorey()
        {
            var building = CreateBuilding(3, 0f);
            var paths = SplineWallPath.FromContainer(building.GetComponent<SplineContainer>(), building.CurveStep);
            var firstFloor = building.transform.InverseTransformPoint(new Vector3(0f, building.FirstFloorWorldHeight, 0f)).y;
            LiveBuilderOpeningPlacer.FitCutterToWall(building, paths[0], 3f, 1.5f, firstFloor + 1f, out var bottom, out var rotation, out var depth);
            var window = BoxModelFactory.CreateCutter(building.transform, building.transform.TransformPoint(bottom), building.transform.rotation * rotation,
                new Vector2(1.5f, 1.5f), depth);
            var allStoreysBefore = LiveBuilderBuildingContext.IsOnAllStoreys;
            LiveBuilderBuildingContext.IsOnAllStoreys = true;

            try
            {
                LiveBuilderCutterGesture.AttachToOwner(window, window.transform.forward);
            }
            finally
            {
                LiveBuilderBuildingContext.IsOnAllStoreys = allStoreysBefore;
            }

            Assert.AreEqual(SplineBuildingStoreys.FindContainer(building, 0).transform, window.transform.parent, "окно — в первом этаже");
            Assert.IsTrue(window.GetComponent<LiveBuilderArray>().CountYToBuildingTop, "повтор до верха");

            var position = window.transform.position;
            GeometryCutterLineSync.SyncAll();
            Assert.That(Vector3.Distance(position, window.transform.position), Is.LessThan(0.001f), "место на линии то же");

            SplineBuildingRebuilder.Rebuild(building);
            var center = GeometryCutterOutline.ShapeToWorld(window).MultiplyPoint3x4(new Vector3(0f, window.Height / 2f, 0f));

            for (var storey = 0; storey < 3; storey++)
            {
                var point = center + Vector3.up * (storey * building.StoreyHeight);
                Assert.IsTrue(LiveBuilderTestScene.IsOpen(new Vector3(point.x, point.y, Origin.z - 2f), Vector3.forward, 2.4f), "дыра на этаже " + (storey + 1));
            }
        }


        // ── Помощники ────────────────────────────────────────────────────

        private SplineBuilding CreateBuilding(int storeyCount, float bottomAboveGround, Vector3 shift = default)
        {
            return CreateBuilding(null, storeyCount, bottomAboveGround, shift);
        }

        // Здание фабрикой, низ — на высоте над полом первого этажа уровня; этажи по 4 м, как у уровня.
        private SplineBuilding CreateBuilding(BoxModel model, int storeyCount, float bottomAboveGround, Vector3 shift = default)
        {
            var corner = shift;
            var building = testScene.CreateBuilding(model, storeyCount, corner, corner + new Vector3(8f, 0f, 0f), corner + new Vector3(8f, 0f, 8f),
                corner + new Vector3(0f, 0f, 8f));
            building.transform.position += Vector3.up * bottomAboveGround;
            SplineBuildingLevelSync.PlaceOnLevel(building);
            return building;
        }

        private static bool IsSameMatrix(Matrix4x4 first, Matrix4x4 second)
        {
            for (var index = 0; index < 16; index++)
                if (Mathf.Abs(first[index] - second[index]) > 0.0001f)
                    return false;

            return true;
        }
    }
}
