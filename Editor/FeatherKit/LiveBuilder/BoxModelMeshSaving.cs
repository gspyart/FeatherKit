using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Пишет файлы мешей Live Builder (<see cref="BoxModelMeshStorage"/>) на диск вместе со сценой и префабом —
    /// иначе сцена сохранена, а файл меша остался старым. Объекты из коробок, стены по линии и части зданий; чужие меши не трогает.
    /// Меши сцены, которые ещё лежат в общей папке несохранённых сцен, при сохранении переезжают копией в папку сцены:
    /// иначе копия постройки в новой несохранённой сцене считала бы общий файл своим и перезаписала его.
    /// Развёртка под запекание — при этой записи и перед запеканием света: пересборка её стирает, а запекание могло начаться до сохранения.
    /// </summary>
    [InitializeOnLoad]
    public static class BoxModelMeshSaving
    {
        static BoxModelMeshSaving()
        {
            EditorSceneManager.sceneSaving += OnSceneSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;
            Lightmapping.bakeStarted += OnBakeStarted;
        }


        // Меши объектов, стен и зданий под root — на диск. Зовут сохранение сцены, префаба и пересборка префаба.
        public static void SaveMeshFiles(GameObject root)
        {
            foreach (var mesh in MeshFilesUnder(root))
            {
                BoxModelMeshStorage.AddLightmapUVs(mesh);
                AssetDatabase.SaveAssetIfDirty(mesh);
            }
        }


        // ── События редактора ────────────────────────────────────────────

        private static void OnSceneSaving(Scene scene, string path)
        {
            MoveFallbackMeshesNextTo(scene, path);

            foreach (var root in scene.GetRootGameObjects())
                SaveMeshFiles(root);
        }

        // Меши построек сцены из общей папки — копией в папку сцены, постройки смотрят на копии. Меш префаба у его экземпляра не трогаем.
        // Старый файл остаётся: на него может смотреть другая несохранённая сцена или префаб; ненужный уберёт уборка файлов мешей.
        private static void MoveFallbackMeshesNextTo(Scene scene, string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath) || !scenePath.StartsWith("Assets/"))
                return;

            var folder = BoxModelMeshStorage.FolderNextTo(scenePath);
            var copies = new Dictionary<Mesh, Mesh>();

            foreach (var root in scene.GetRootGameObjects())
            foreach (var filter in FiltersUnder(root))
            {
                var mesh = filter.sharedMesh;

                if (!BoxModelMeshStorage.IsFallbackMeshFile(mesh) || IsPrefabSourceMesh(filter, mesh))
                    continue;

                if (!copies.ContainsKey(mesh))
                    foreach (var (original, copy) in BoxModelMeshStorage.CopyMeshFileTo(mesh, folder))
                        copies[original] = copy;

                if (!copies.TryGetValue(mesh, out var meshCopy))
                    continue;

                filter.sharedMesh = meshCopy;
                LiveBuilderSyncEdits.RecordPrefabInstance(filter);

                if (filter.TryGetComponent<MeshCollider>(out var meshCollider) && meshCollider.sharedMesh != null
                    && copies.TryGetValue(meshCollider.sharedMesh, out var colliderCopy))
                {
                    meshCollider.sharedMesh = colliderCopy;
                    LiveBuilderSyncEdits.RecordPrefabInstance(meshCollider);
                }
            }
        }

        private static bool IsPrefabSourceMesh(MeshFilter filter, Mesh mesh)
        {
            var sourceFilter = PrefabUtility.IsPartOfPrefabInstance(filter) ? PrefabUtility.GetCorrespondingObjectFromSource(filter) : null;
            return sourceFilter != null && sourceFilter.sharedMesh == mesh;
        }


        private static void OnPrefabSaving(GameObject prefabRoot)
        {
            SaveMeshFiles(prefabRoot);
        }


        // Запекание берёт меши как есть: всем мешам Live Builder в открытых сценах — развёртку, если её стёрла пересборка.
        private static void OnBakeStarted()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);

                if (!scene.isLoaded)
                    continue;

                foreach (var root in scene.GetRootGameObjects())
                foreach (var mesh in MeshFilesUnder(root))
                    BoxModelMeshStorage.AddLightmapUVs(mesh);
            }
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Файлы мешей Live Builder под root: объектов, стен и частей зданий. Чужой меш (модель, ProBuilder) не входит.
        private static IEnumerable<Mesh> MeshFilesUnder(GameObject root)
        {
            foreach (var filter in FiltersUnder(root))
                if (BoxModelMeshStorage.IsBoxModelMeshFile(filter.sharedMesh))
                    yield return filter.sharedMesh;
        }

        // Фильтры мешей построек Live Builder под root: объектов из коробок, стен и частей зданий.
        private static List<MeshFilter> FiltersUnder(GameObject root)
        {
            var filters = new List<MeshFilter>();

            foreach (var model in root.GetComponentsInChildren<BoxModel>(true))
                AddFilter(filters, model);

            foreach (var wall in root.GetComponentsInChildren<SplineWall>(true))
                AddFilter(filters, wall);

            foreach (var part in root.GetComponentsInChildren<SplineBuildingPart>(true))
                AddFilter(filters, part);

            return filters;
        }

        private static void AddFilter(List<MeshFilter> filters, Component owner)
        {
            if (owner.TryGetComponent<MeshFilter>(out var filter))
                filters.Add(filter);
        }
    }
}
