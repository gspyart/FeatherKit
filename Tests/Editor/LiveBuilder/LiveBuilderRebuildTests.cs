using System.Collections.Generic;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Пересборка и файлы мешей: окно на одном этаже пересобирает только этот этаж; экземпляр префаба под чужим вырезом получает свой меш
    /// и возвращается к мешу префаба, когда выреза нет; копия не перезаписывает общий с оригиналом файл; хеш линии не зависит от языка системы;
    /// уборка находит брошенные файлы мешей и не трогает нужные — и те, на которые ссылается только ScriptableObject.
    /// </summary>
    public class LiveBuilderRebuildTests
    {
        private const string TempFolder = "Assets/LiveBuilderTestTemp";

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


        // ── Части здания ─────────────────────────────────────────────────

        [Test]
        public void CutterOnOneStorey_RebuildsOnlyThatStorey()
        {
            var building = testScene.CreateBuilding(3, Vector3.zero, new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 8f), new Vector3(0f, 0f, 8f));
            var signaturesBefore = PartSignaturesOf(building);

            var height = building.FirstFloorWorldHeight + 1f;
            BoxModelFactory.CreateCutter(building.transform, new Vector3(Origin.x + 4f, height, Origin.z + 0.15f), Quaternion.identity, new Vector2(2f, 1.5f), 0.5f);
            SplineBuildingRebuilder.RebuildChangedBuildings();

            var changed = new List<string>();

            foreach (var part in building.GetComponentsInChildren<SplineBuildingPart>())
                if (part.BuiltSignature != signaturesBefore[part])
                    changed.Add(part.Kind + " " + part.Storey);

            CollectionAssert.AreEquivalent(new[] { "Walls 0" }, changed, "пересобрались: " + string.Join(", ", changed));
        }

        private static Dictionary<SplineBuildingPart, string> PartSignaturesOf(SplineBuilding building)
        {
            var signatures = new Dictionary<SplineBuildingPart, string>();

            foreach (var part in building.GetComponentsInChildren<SplineBuildingPart>())
                signatures[part] = part.BuiltSignature;

            return signatures;
        }


        // ── Экземпляры префабов ──────────────────────────────────────────

        [Test]
        public void PrefabInstanceUnderOutsideCutter_GetsOwnMeshAndReturns()
        {
            AssetDatabase.CreateFolder("Assets", "LiveBuilderTestTemp");
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(10f, 0f, 0f));
            BoxModelFactory.CreateCutter(wall.transform, Origin + new Vector3(2f, 0.5f, 0f), Quaternion.identity, new Vector2(1f, 1f), 0.5f);
            SplineWallRebuilder.Rebuild(wall);

            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(wall.gameObject, TempFolder + "/Test Wall.prefab", InteractionMode.AutomatedAction);
            var instance = ((GameObject)PrefabUtility.InstantiatePrefab(prefab)).GetComponent<SplineWall>();
            instance.transform.position = Origin + new Vector3(0f, 0f, 5f);
            BoxModelPrefabInstances.ForgetAllChecks();

            Assert.IsTrue(BoxModelPrefabInstances.UsesPrefabGeometry(instance, out _), "свой вырез префаба — меш префаба");

            var outsideCutter = BoxModelFactory.CreateCutter(null, Origin + new Vector3(6f, 0.5f, 5f), Quaternion.identity, new Vector2(1.5f, 2f), 0.6f);
            Assert.IsFalse(BoxModelPrefabInstances.UsesPrefabGeometry(instance, out _), "чужой вырез — свой меш");
            Assert.IsTrue(BoxModelPrefabInstances.UsesPrefabGeometry(wall, out _), "другой экземпляр — по-прежнему меш префаба");

            Object.DestroyImmediate(outsideCutter.gameObject);
            Assert.IsTrue(BoxModelPrefabInstances.UsesPrefabGeometry(instance, out var isRechecked), "вырез убрали — снова меш префаба");
            Assert.IsTrue(isRechecked, "и сверено заново — снимутся лишние правки меша");
        }


        // ── Файлы мешей ──────────────────────────────────────────────────

        // Копия (Ctrl+D) делит файл меша с оригиналом; пересборка копии напрямую, как после рисования, оригинал не трогает.
        [Test]
        public void CopyRebuiltDirectly_GetsOwnMeshFile()
        {
            var original = testScene.CreateWall(Vector3.zero, new Vector3(10f, 0f, 0f));
            var originalMesh = original.GetComponent<MeshFilter>().sharedMesh;
            var originalBounds = originalMesh.bounds;

            var copy = Object.Instantiate(original.gameObject).GetComponent<SplineWall>();
            Assert.AreSame(originalMesh, copy.GetComponent<MeshFilter>().sharedMesh, "копия начинает с общего файла");

            copy.Height = 5f;
            SplineWallRebuilder.Rebuild(copy);

            Assert.AreNotSame(originalMesh, copy.GetComponent<MeshFilter>().sharedMesh, "у копии — свой файл");
            Assert.AreSame(originalMesh, original.GetComponent<MeshFilter>().sharedMesh, "у оригинала — прежний");
            Assert.AreEqual(originalBounds, originalMesh.bounds, "меш оригинала не перезаписан");
        }


        // Хеш линии одинаков при любом языке системы и на любом запуске: сцена не становится «изменённой» сама.
        [Test]
        public void LineHash_SameUnderAnyCulture()
        {
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(10.25f, 0f, 0.5f));
            var container = wall.GetComponent<SplineContainer>();
            var previousCulture = System.Globalization.CultureInfo.CurrentCulture;

            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ru-RU");
                var russian = LiveBuilderSyncEdits.LineHashOf(container);
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
                Assert.AreEqual(russian, LiveBuilderSyncEdits.LineHashOf(container));
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = previousCulture;
            }
        }


        [Test]
        public void MeshCleanup_FindsAbandonedFileOnly()
        {
            var kept = testScene.CreateWall(Vector3.zero, new Vector3(10f, 0f, 0f));
            var removed = testScene.CreateWall(new Vector3(0f, 0f, 5f), new Vector3(10f, 0f, 5f));
            var keptPath = AssetDatabase.GetAssetPath(kept.GetComponent<MeshFilter>().sharedMesh);
            var removedPath = AssetDatabase.GetAssetPath(removed.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(removed.gameObject);

            try
            {
                var unused = BoxModelMeshCleanup.FindUnusedMeshFiles();
                CollectionAssert.Contains(unused, removedPath, "файл удалённой стены — лишний");
                CollectionAssert.DoesNotContain(unused, keptPath, "файл живой стены — нужен");
            }
            finally
            {
                AssetDatabase.DeleteAsset(removedPath);
            }
        }


        // Меш, на который ссылается только ScriptableObject проекта (не сцена и не префаб), — нужный.
        [Test]
        public void MeshCleanup_KeepsFileReferencedByScriptableObject()
        {
            AssetDatabase.CreateFolder("Assets", "LiveBuilderTestTemp");
            var wall = testScene.CreateWall(Vector3.zero, new Vector3(10f, 0f, 0f));
            var mesh = wall.GetComponent<MeshFilter>().sharedMesh;
            var meshPath = AssetDatabase.GetAssetPath(mesh);
            Object.DestroyImmediate(wall.gameObject);

            try
            {
                CollectionAssert.Contains(BoxModelMeshCleanup.FindUnusedMeshFiles(), meshPath, "без ссылок — лишний");

                var holder = ScriptableObject.CreateInstance<LiveBuilderTestMeshHolder>();
                holder.Mesh = mesh;
                AssetDatabase.CreateAsset(holder, TempFolder + "/Mesh Holder.asset");

                CollectionAssert.DoesNotContain(BoxModelMeshCleanup.FindUnusedMeshFiles(), meshPath, "на него смотрит ScriptableObject — нужен");
            }
            finally
            {
                AssetDatabase.DeleteAsset(meshPath);
            }
        }


        // Автоуборка после сохранения сцены смотрит только в её папку: лишний файл в другой папке не находится.
        [Test]
        public void MeshCleanupInFolder_LooksOnlyThere()
        {
            AssetDatabase.CreateFolder("Assets", "LiveBuilderTestTemp");
            var removed = testScene.CreateWall(Vector3.zero, new Vector3(10f, 0f, 0f));
            var removedPath = AssetDatabase.GetAssetPath(removed.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(removed.gameObject);

            try
            {
                Assert.AreEqual("Assets/Levels/Street_BoxModels", BoxModelMeshStorage.FolderNextTo("Assets/Levels/Street.unity"));
                CollectionAssert.Contains(BoxModelMeshCleanup.FindUnusedMeshFiles(BoxModelMeshStorage.FallbackFolder), removedPath, "в своей папке — найден");
                CollectionAssert.DoesNotContain(BoxModelMeshCleanup.FindUnusedMeshFiles(TempFolder), removedPath, "в чужой папке — не ищется");
                CollectionAssert.IsEmpty(BoxModelMeshCleanup.FindUnusedMeshFiles("Assets/No Such Folder"), "папки нет — пусто");
            }
            finally
            {
                AssetDatabase.DeleteAsset(removedPath);
            }
        }
    }
}
