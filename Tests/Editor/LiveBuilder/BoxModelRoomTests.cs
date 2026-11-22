using System.Linq;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Комната — одно поведение (<see cref="BoxModelRoomShell"/>): пустота плюс оболочка, стены, пол и потолок включаются по отдельности.
    /// В здании стены на его стенах не строятся; соседние комнаты делят одну стену; в доме из коробок видна только пустота;
    /// материалы изнутри — комнаты; стена, нарисованная в комнате, встаёт в «Kept From Cuts» и цела.
    /// </summary>
    public class BoxModelRoomTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private Object[] selectionBefore;


        [SetUp]
        public void OpenScene()
        {
            selectionBefore = Selection.objects;
            Selection.objects = new Object[0];
            testScene = new LiveBuilderTestScene();
        }


        [TearDown]
        public void CloseScene()
        {
            foreach (var tool in LiveBuilderToolPalette.Tools)
                tool.Cancel();

            testScene.Dispose();
            Selection.objects = selectionBefore;
        }


        // ── Переключатели ────────────────────────────────────────────────

        // Комната 4 × 3 × 4 в пустоте: включена одна сторона — есть только она.
        [Test]
        public void Switches_EachBuildsOnlyItsOwnSide()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var room = BoxModelFactory.CreateRoom(model, Vector3.zero, new Vector3(4f, 3f, 4f), true);

            foreach (var (hasWalls, hasFloor, hasCeiling) in new[] { (true, false, false), (false, true, false), (false, false, true), (false, false, false) })
            {
                room.HasWalls = hasWalls;
                room.HasFloor = hasFloor;
                room.HasCeiling = hasCeiling;
                Rebuild(model);

                var what = "стены " + hasWalls + ", пол " + hasFloor + ", потолок " + hasCeiling;
                Assert.AreEqual(!hasWalls, IsOpenFromCenter(Vector3.right), what + ": стена");
                Assert.AreEqual(!hasFloor, IsOpenFromCenter(Vector3.down), what + ": пол");
                Assert.AreEqual(!hasCeiling, IsOpenFromCenter(Vector3.up), what + ": потолок");
            }
        }

        // Луч из середины комнаты 4 × 3 × 4 на 3 м — дальше её оболочки.
        private static bool IsOpenFromCenter(Vector3 direction)
        {
            return LiveBuilderTestScene.IsOpen(Origin + new Vector3(2f, 1.5f, 2f), direction, 3f);
        }


        // ── Здание ───────────────────────────────────────────────────────

        // Здание 8 × 8, стены 0,3 внутрь: комната «Коробкой» в режиме Room от угла здания — в его этаж, стены только две свободные, пол и потолок — здания.
        [Test]
        public void RoomMode_InBuilding_GoesIntoIt_WithoutSidesOnItsWalls()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var building = testScene.CreateBuilding(model, 2, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var floor = building.FirstFloorWorldHeight;
            var wallSideBefore = LiveBuilderRoomSettings.WallSide;
            var gridBefore = EditorSnapSettings.gridSize;
            var modeBefore = LiveBuilderBoxModeSettings.Mode;
            LiveBuilderRoomSettings.WallSide = LiveBuilderRoomWallSide.Outward;
            LiveBuilderBoxModeSettings.Mode = LiveBuilderBoxMode.Room;
            EditorSnapSettings.gridSize = Vector3.one;
            Physics.SyncTransforms();

            try
            {
                // Угол у стены здания прилипает к ней: 0,4 → по сетке 0 → к стене 0,3.
                var driver = new LiveBuilderToolDriver(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>());
                driver.Drag(DownRay(0.4f, 0.4f, floor + 1.5f), DownRay(4f, 4f, floor + 1.5f));
            }
            finally
            {
                LiveBuilderRoomSettings.WallSide = wallSideBefore;
                LiveBuilderBoxModeSettings.Mode = modeBefore;
                EditorSnapSettings.gridSize = gridBefore;
            }

            var room = InTestScene<BoxModelRoom>().Single();
            Assert.AreEqual(SplineBuildingStoreys.FindContainer(building, 0).transform, room.transform.parent, "в первом этаже здания");
            Assert.IsTrue(room.HasWalls && !room.HasFloor && !room.HasCeiling, "стены свои, пол и потолок — здания");
            Assert.AreEqual(building.ClearHeight, room.Size.y, 0.001f, "до потолка этажа");

            Rebuild(model);
            SplineBuildingRebuilder.Rebuild(building);

            var shell = BoxModelRebuilder.GetSolverBoxes(model).Where(box => box.Owner == room && box.Mode == BoxModelPartMode.Add).ToList();
            Assert.AreEqual(2, shell.Count, "стены на стенах здания не строятся — остались две свободные");

            var center = new Vector3(Origin.x + 2f, floor + 1.5f, Origin.z + 2f);
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(center, Vector3.right, 3f), "свободная стена есть");
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(center, Vector3.down, 2f), "пол здания цел");
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(center, Vector3.up, 3f), "потолок здания цел");
        }


        // ── Соседи ───────────────────────────────────────────────────────

        // Комнаты 4 × 3 × 4 вплотную через стену 0,3: оболочки сливаются — между ними одна стена, а не две.
        [Test]
        public void NeighbourRooms_ShareOneWall()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            CreateRoomWithShell(model, Vector3.zero);
            CreateRoomWithShell(model, new Vector3(4.3f, 0f, 0f));
            Rebuild(model);
            Physics.SyncTransforms();

            Assert.IsTrue(Physics.Raycast(Origin + new Vector3(2f, 1.5f, 2f), Vector3.right, out var fromFirst, 4f), "из первой — в стену");
            Assert.IsTrue(Physics.Raycast(Origin + new Vector3(6.3f, 1.5f, 2f), Vector3.left, out var fromSecond, 4f), "из второй — в стену");
            Assert.AreEqual(0.3f, fromSecond.point.x - fromFirst.point.x, 0.001f, "стена между ними — одной толщины");

            var surfacesInsideWall = BoxModelRebuilder.GetSurfaces(model).Count(surface => BoxFaces.Axis(surface.Face) == 0 && surface.Plane > 4000 && surface.Plane < 4300);
            Assert.AreEqual(0, surfacesInsideWall, "внутри стены своих граней нет");
        }

        private static BoxModelRoom CreateRoomWithShell(BoxModel model, Vector3 localMin)
        {
            var room = BoxModelFactory.CreateRoom(model, localMin, new Vector3(4f, 3f, 4f), true);
            room.WallThickness = 0.3f;
            room.SlabThickness = 0.2f;
            room.HasFloor = true;
            room.HasCeiling = true;
            return room;
        }


        // ── Дом из коробок ───────────────────────────────────────────────

        // Дом 10 × 4 × 10, комната со всей оболочкой целиком внутри него: выглядит так же, как одна пустота без оболочки.
        [Test]
        public void InBoxHouse_OnlyVoidIsSeen()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreatePart(model, Vector3.zero, new Vector3(10f, 4f, 10f), BoxModelPartMode.Add);
            var room = CreateRoomWithShell(model, new Vector3(2f, 0.5f, 2f));
            Rebuild(model);
            var withShell = LiveBuilderLookDigest.Describe(model.transform);

            room.HasWalls = false;
            room.HasFloor = false;
            room.HasCeiling = false;
            Rebuild(model);

            CollectionAssert.IsEmpty(LiveBuilderLookDigest.Compare(LiveBuilderLookDigest.Describe(model.transform), withShell), "оболочка слилась с домом");
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(4f, 2f, 4f), Vector3.right, 6f), "стены пустоты на месте");
        }


        // ── Материалы ────────────────────────────────────────────────────

        // Пол, стены и потолок изнутри — свои материалы комнаты; покраска по грани изнутри попадает в них же.
        [Test]
        public void Materials_FloorWallsCeiling_SeenFromInside()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var room = CreateRoomWithShell(model, Vector3.zero);
            room.FloorMaterial = new Material(model.DefaultMaterial) { name = "Room Floor" };
            room.WallMaterial = new Material(model.DefaultMaterial) { name = "Room Walls" };
            room.CeilingMaterial = new Material(model.DefaultMaterial) { name = "Room Ceiling" };
            Rebuild(model);
            Physics.SyncTransforms();

            var center = Origin + new Vector3(2f, 1.5f, 2f);
            Assert.AreEqual(room.FloorMaterial, MaterialHitFrom(center, Vector3.down), "пол");
            Assert.AreEqual(room.WallMaterial, MaterialHitFrom(center, Vector3.back), "стена");
            Assert.AreEqual(room.CeilingMaterial, MaterialHitFrom(center, Vector3.up), "потолок");
            Assert.AreEqual(model.DefaultMaterial, MaterialHitFrom(Origin + new Vector3(2f, 1.5f, -2f), Vector3.forward), "снаружи — материал объекта");

            Assert.AreEqual("room floor", SlotUnder(center, Vector3.down), "покраска пола изнутри");
            Assert.AreEqual("room walls", SlotUnder(center, Vector3.left), "покраска стены изнутри");
            Assert.AreEqual("room ceiling", SlotUnder(center, Vector3.up), "покраска потолка изнутри");
        }

        // Материал части меша, в которую попал луч.
        private static Material MaterialHitFrom(Vector3 from, Vector3 direction)
        {
            Assert.IsTrue(Physics.Raycast(from, direction, out var hit, 5f), "луч не попал");
            var mesh = ((MeshCollider)hit.collider).sharedMesh;
            var index = hit.triangleIndex * 3;

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var descriptor = mesh.GetSubMesh(submesh);

                if (index >= descriptor.indexStart && index < descriptor.indexStart + descriptor.indexCount)
                    return hit.collider.GetComponent<MeshRenderer>().sharedMaterials[submesh];
            }

            return null;
        }

        private static string SlotUnder(Vector3 from, Vector3 direction)
        {
            Assert.IsTrue(LiveBuilderMaterialSlots.TryFindUnder(new Ray(from, direction), false, out var slot), "под лучом нет места материала");
            return slot.Description;
        }


        // ── Стена в комнате ──────────────────────────────────────────────

        // Комната 8 × 3 × 6 в выделенном объекте: стена поперёк неё встаёт во вложенный объект — пустота комнаты её не режет.
        [Test]
        public void WallTool_InsideRoom_GoesIntoKeptFromCuts_AndStaysWhole()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            BoxModelFactory.CreateRoom(model, Vector3.zero, new Vector3(8f, 3f, 6f), true);
            Rebuild(model);
            Physics.SyncTransforms();
            Selection.activeGameObject = model.gameObject;

            var wallTool = LiveBuilderToolPalette.Get<LiveBuilderWallTool>();
            var driver = new LiveBuilderToolDriver(wallTool);
            driver.Move(InsideRoomRay(4f, 1f));
            StringAssert.Contains(LiveBuilderKeptFromCuts.GroupName, wallTool.Hint, "подсказка говорит заранее");

            driver.Click(InsideRoomRay(4f, 1f));
            driver.Click(InsideRoomRay(4f, 5f));
            driver.Press(KeyCode.Return);

            var wall = InTestScene<SplineWall>().Single();
            var kept = wall.transform.parent;
            Assert.AreEqual(LiveBuilderKeptFromCuts.GroupName, kept.name, "стена — во вложенном объекте");
            Assert.IsNotNull(kept.GetComponent<BoxModel>(), "это объект из коробок");
            Assert.AreEqual(model.transform, kept.parent, "того же родителя");
            Assert.AreEqual(2, wall.GetComponent<SplineContainer>().Spline.Count, "две точки");

            Rebuild(model);
            SplineWallRebuilder.Rebuild(wall);
            Assert.IsFalse(LiveBuilderTestScene.IsOpen(Origin + new Vector3(2f, 1.5f, 3f), Vector3.right, 4f), "стена цела");
        }

        // Луч сверху вниз изнутри комнаты: под потолком, в метрах по X и Z от начала сцены.
        private static Ray InsideRoomRay(float x, float z)
        {
            return new Ray(Origin + new Vector3(x, 2.5f, z), Vector3.down);
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static void Rebuild(BoxModel model)
        {
            BoxModelRebuilder.ForgetCachedResults();
            BoxModelRebuilder.Rebuild(model);
        }


        // Луч сверху вниз из точки на высоте мира, в метрах по X и Z от начала сцены.
        private static Ray DownRay(float x, float z, float worldHeight)
        {
            return new Ray(new Vector3(Origin.x + x, worldHeight, Origin.z + z), Vector3.down);
        }


        private static T[] InTestScene<T>() where T : Component
        {
            var scene = SceneManager.GetActiveScene();
            return Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(component => component.gameObject.scene == scene).ToArray();
        }
    }
}
