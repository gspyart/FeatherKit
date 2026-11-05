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
            foreach (var root in scene.GetRootGameObjects())
                SaveMeshFiles(root);
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
            var filters = new List<MeshFilter>();

            foreach (var model in root.GetComponentsInChildren<BoxModel>(true))
                filters.Add(model.GetComponent<MeshFilter>());

            foreach (var wall in root.GetComponentsInChildren<SplineWall>(true))
                filters.Add(wall.GetComponent<MeshFilter>());

            foreach (var part in root.GetComponentsInChildren<SplineBuildingPart>(true))
                filters.Add(part.GetComponent<MeshFilter>());

            foreach (var filter in filters)
                if (filter != null && BoxModelMeshStorage.IsBoxModelMeshFile(filter.sharedMesh))
                    yield return filter.sharedMesh;
        }
    }
}
