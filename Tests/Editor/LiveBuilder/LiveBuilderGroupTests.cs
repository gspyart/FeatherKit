using System.Reflection;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Группы — объект из коробок как граница вырезов: вырезающая коробка снаружи не режет стену во вложенной группе, вырез «авто» в здании —
    /// стену в группе его интерьера («всё вокруг» — режет). Пустая группа без файла меша и без лишних пересборок.
    /// Команды: «Новая группа» в здании — в его «Interior» с привязкой к этажу; «В группу» — места в мире те же, Ctrl+Z возвращает родителей.
    /// </summary>
    public class LiveBuilderGroupTests
    {
        private static readonly Vector3 Origin = LiveBuilderTestScene.Origin;

        private LiveBuilderTestScene testScene;
        private Object[] previousSelection;


        [SetUp]
        public void OpenScene()
        {
            previousSelection = Selection.objects;
            testScene = new LiveBuilderTestScene();
        }


        [TearDown]
        public void CloseScene()
        {
            testScene.Dispose();
            Selection.objects = previousSelection;
        }


        // ── Вырезающая коробка ───────────────────────────────────────────

        [Test]
        public void OuterCutBox_SkipsWallInNestedGroup_CutsWallInOwnModel()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var group = BoxModelFactory.CreateGroup(model.transform);
            var wall = testScene.CreateWall(group, Vector3.zero, new Vector3(10f, 0f, 0f));
            BoxModelFactory.CreatePart(model, new Vector3(4f, 0.5f, -1f), new Vector3(6f, 2.5f, 1f), BoxModelPartMode.Cut);
            BoxModelRebuilder.Rebuild(model);
            SplineWallRebuilder.Rebuild(wall);

            Assert.IsFalse(IsWallOpen(), "стена во вложенной группе — коробка снаружи её не режет");

            wall.transform.SetParent(model.transform, true);
            wall.transform.SetSiblingIndex(0);
            BoxModelRebuilder.ForgetCachedResults();
            SplineWallRebuilder.Rebuild(wall);

            Assert.IsTrue(IsWallOpen(), "та же стена прямо в объекте — прорезана");
        }

        private static bool IsWallOpen()
        {
            return LiveBuilderTestScene.IsOpen(Origin + new Vector3(5f, 1.5f, -2f), Vector3.forward, 4f);
        }


        // ── Вырез ────────────────────────────────────────────────────────

        // Здание 8 × 8, в интерьере — группа с перегородкой z = 4; вырез «авто» лежит в самом здании и задевает перегородку.
        [Test]
        public void AutomaticCutterInBuilding_SkipsWallInNestedGroup_EverythingCutsIt()
        {
            var building = testScene.CreateBuilding(2, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var floor = building.FirstFloorWorldHeight - Origin.y;
            var interior = BoxModelFactory.InteriorOf(building);
            var group = BoxModelFactory.CreateGroup(interior);
            var wall = testScene.CreateWall(group, new Vector3(2f, floor, 4f), new Vector3(6f, floor, 4f));
            var cutter = BoxModelFactory.CreateCutter(building.transform, new Vector3(Origin.x + 4f, building.FirstFloorWorldHeight + 0.5f, Origin.z + 4f),
                Quaternion.identity, new Vector2(1f, 2f), 1f);
            SplineWallRebuilder.Rebuild(wall);

            Assert.IsFalse(IsPartitionOpen(building), "«авто» в здании — во вложенную группу не заходит");

            wall.transform.SetParent(interior, true);
            SplineWallRebuilder.Rebuild(wall);

            Assert.IsTrue(IsPartitionOpen(building), "перегородка прямо в интерьере — прорезана");

            wall.transform.SetParent(group.transform, true);
            cutter.Scope = GeometryCutterScope.Everything;
            SplineWallRebuilder.Rebuild(wall);

            Assert.IsTrue(IsPartitionOpen(building), "«всё вокруг» режет и сквозь группу");
        }

        private static bool IsPartitionOpen(SplineBuilding building)
        {
            return LiveBuilderTestScene.IsOpen(new Vector3(Origin.x + 4f, building.FirstFloorWorldHeight + 1.5f, Origin.z + 3f), Vector3.forward, 2f);
        }


        // ── Пустая группа ────────────────────────────────────────────────

        [Test]
        public void EmptyGroup_HasNoMeshFile_AndStaysUpToDate()
        {
            var group = BoxModelFactory.CreateGroup(null);
            group.transform.position = Origin;
            BoxModelRebuilder.Rebuild(group);

            var filter = group.GetComponent<MeshFilter>();
            var meshCollider = group.GetComponent<MeshCollider>();
            Assert.IsNull(filter.sharedMesh, "меша нет — и файла нет");
            Assert.IsTrue(meshCollider == null || meshCollider.sharedMesh == null, "коллайдер без меша");
            Assert.IsTrue(IsMeshUpToDate(group), "пустая группа готова — второй проход её не пересобирает");

            BoxModelFactory.CreatePart(group, Vector3.zero, Vector3.one, BoxModelPartMode.Add);
            BoxModelRebuilder.ForgetCachedResults();

            Assert.IsFalse(IsMeshUpToDate(group), "появилась коробка — пора собрать");

            BoxModelRebuilder.Rebuild(group);

            Assert.IsTrue(BoxModelMeshStorage.IsBoxModelMeshFile(filter.sharedMesh), "с коробкой — свой файл меша");
            Assert.IsTrue(IsMeshUpToDate(group), "и он готов");
        }

        // Та же сверка, что у очереди пересборки, — без прохода по открытым сценам.
        private static bool IsMeshUpToDate(BoxModel model)
        {
            var method = typeof(BoxModelRebuilder).GetMethod("IsMeshUpToDate", BindingFlags.NonPublic | BindingFlags.Static);
            return (bool)method.Invoke(null, new object[] { model, model.GetComponent<MeshFilter>().sharedMesh });
        }


        // ── Команды ──────────────────────────────────────────────────────

        [Test]
        public void NewGroupOnBuilding_LandsInInteriorWithStoreyLink()
        {
            var building = testScene.CreateBuilding(3, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var group = LiveBuilderGroups.CreateGroupInside(building.gameObject);

            Assert.AreEqual(BoxModelFactory.InteriorOf(building), group.transform.parent, "в «Interior» здания");

            var link = group.GetComponent<SplineBuildingStoreyLink>();
            Assert.IsTrue(link != null && link.IsValid && link.Building == building, "привязана к этажам здания");
            Assert.That(link.Storey, Is.InRange(0, building.StoreyCount - 1), "на одном из его этажей");
            Assert.AreEqual(building.FirstFloorWorldHeight + link.Storey * building.StoreyHeight, group.transform.position.y, 0.001f, "на полу своего этажа");
        }


        [Test]
        public void GroupSelection_KeepsWorldPositions_UndoRestoresParents()
        {
            var model = BoxModelFactory.CreateModel(Origin);
            var first = BoxModelFactory.CreatePart(model, new Vector3(1f, 0f, 1f), new Vector3(2f, 1f, 2f), BoxModelPartMode.Add);
            var second = BoxModelFactory.CreatePart(model, new Vector3(3f, 0.5f, 1f), new Vector3(4f, 1.5f, 3f), BoxModelPartMode.Add);
            var firstPosition = first.transform.position;
            var secondPosition = second.transform.position;

            var group = LiveBuilderGroups.GroupObjects(new[] { first.gameObject, second.gameObject });

            Assert.IsNotNull(group, "завернулись");
            Assert.AreEqual(model.transform, group.transform.parent, "группа — в их объекте");
            Assert.AreEqual(group.transform, first.transform.parent, "первая коробка — в группе");
            Assert.AreEqual(group.transform, second.transform.parent, "вторая — тоже");
            Assert.That(Vector3.Distance(firstPosition, first.transform.position), Is.LessThan(0.0005f), "места в мире те же");
            Assert.That(Vector3.Distance(secondPosition, second.transform.position), Is.LessThan(0.0005f), "места в мире те же");

            Undo.PerformUndo();

            Assert.AreEqual(model.transform, first.transform.parent, "Ctrl+Z — коробки снова в объекте");
            Assert.AreEqual(model.transform, second.transform.parent, "обе");
            Assert.IsTrue(group == null, "группы больше нет");
        }
    }
}
