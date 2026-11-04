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
    /// </summary>
    [InitializeOnLoad]
    public static class BoxModelMeshSaving
    {
        static BoxModelMeshSaving()
        {
            EditorSceneManager.sceneSaving += OnSceneSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;
        }


        // Меши объектов, стен и зданий под root — на диск. Зовут сохранение сцены, префаба и пересборка префаба.
        public static void SaveMeshFiles(GameObject root)
        {
            foreach (var model in root.GetComponentsInChildren<BoxModel>(true))
                SaveMeshFile(model.GetComponent<MeshFilter>().sharedMesh);

            foreach (var wall in root.GetComponentsInChildren<SplineWall>(true))
                SaveMeshFile(wall.GetComponent<MeshFilter>().sharedMesh);

            foreach (var part in root.GetComponentsInChildren<SplineBuildingPart>(true))
                SaveMeshFile(part.GetComponent<MeshFilter>().sharedMesh);
        }

        // Только свои файлы: чужой меш (модель, ProBuilder) не разворачиваем и не пишем.
        private static void SaveMeshFile(Mesh mesh)
        {
            if (!BoxModelMeshStorage.IsBoxModelMeshFile(mesh))
                return;

            BoxModelMeshStorage.AddLightmapUVs(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
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
    }
}
