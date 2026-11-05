using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Хранит меш объекта из коробок и стены по линии отдельным файлом, а не внутри сцены: сцена не раздувается, как с ProBuilder.
    /// Папка — рядом со сценой, «<Сцена>_BoxModels»; сцена ещё не сохранена — общая папка из настроек проекта,
    /// при сохранении сцены её файлы оттуда переезжают копией в папку сцены (<see cref="BoxModelMeshSaving"/>).
    /// Меш для столкновений со скатами лестниц — второй меш в том же файле.
    /// Пересобранный меш записывается на диск сам, через короткую паузу, — не ждёт сохранения сцены:
    /// префаб или копия, которые на него ссылаются, никогда не останутся с незаписанной геометрией.
    /// </summary>
    [InitializeOnLoad]
    public static class BoxModelMeshStorage
    {
        private const string FolderSuffix = "_BoxModels";
        private const string ColliderMeshSuffix = " Collider";

        // Пауза после последней пересборки перед записью на диск: пока тянешь ручку, файл не пишется на каждый шаг, с.
        private const double SaveDelaySeconds = 0.5;

        private static readonly HashSet<Mesh> MeshesToSave = new();

        // Префабы, загруженные кодом для пересборки: сцена → файл префаба. Меши их построек — в папке префаба, а не в общей.
        private static readonly Dictionary<Scene, string> LoadedPrefabPaths = new();

        private static double saveTime;


        static BoxModelMeshStorage()
        {
            EditorApplication.update += SavePendingMeshes;
        }


        // Сюда — меши объектов несохранённой сцены; папка — из настроек проекта.
        public static string FallbackFolder => LiveBuilderProjectSettings.instance.UnsavedSceneMeshFolder;


        // Меш-файл объекта (объекта из коробок, стены, части здания): тот, что уже стоит на нём, если он его; иначе — новый пустой файл с этим именем.
        public static Mesh GetOrCreateMeshFile(Component owner, string fileName = null)
        {
            var currentMesh = owner.GetComponent<MeshFilter>().sharedMesh;

            if (IsOwnMeshFile(owner, currentMesh))
                return currentMesh;

            var folder = FolderFor(owner.gameObject);
            EnsureFolder(folder);

            var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeFileName(fileName ?? owner.name) + ".asset");
            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }


        // Файл меша объекта из коробок — чужие меши (модели, ProBuilder) не перезаписываем.
        public static bool IsBoxModelMeshFile(Mesh mesh)
        {
            return mesh != null && IsBoxModelMeshPath(AssetDatabase.GetAssetPath(mesh));
        }


        // То же по пути файла, не загружая его.
        public static bool IsBoxModelMeshPath(string path)
        {
            return path.EndsWith(".asset") && (path.StartsWith(FallbackFolder + "/") || path.Contains(FolderSuffix + "/"));
        }


        // Файл Live Builder и лежит в папке сцены или префаба этого объекта. Из папки другой сцены или префаба — чужой:
        // объект скопировали туда или распаковали префаб, а файл всё ещё смотрит и туда.
        // Общая папка — своя только у объекта несохранённой сцены (на тот же файл в открытых сценах никто больше не смотрит — это сверяет запись).
        // У сохранённой сцены своя папка: файл из общей ей уже чужой, а при сохранении сцены такие файлы переезжают копией в её папку.
        public static bool IsOwnMeshFile(Component owner, Mesh mesh)
        {
            if (!IsBoxModelMeshFile(mesh))
                return false;

            var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(mesh))?.Replace('\\', '/');
            return folder == FolderFor(owner.gameObject);
        }


        // ── Общая папка несохранённых сцен ───────────────────────────────

        // Файл Live Builder из общей папки несохранённых сцен.
        public static bool IsFallbackMeshFile(Mesh mesh)
        {
            return IsBoxModelMeshFile(mesh) && AssetDatabase.GetAssetPath(mesh).StartsWith(FallbackFolder + "/");
        }


        // Копия файла меша (вместе с мешем для столкновений) в папку folder. Возвращает пары «меш файла → его копия».
        public static Dictionary<Mesh, Mesh> CopyMeshFileTo(Mesh mainMesh, string folder)
        {
            EnsureFolder(folder);

            var sourcePath = AssetDatabase.GetAssetPath(mainMesh);
            var copyPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Path.GetFileName(sourcePath));
            var copies = new Dictionary<Mesh, Mesh>();

            if (!AssetDatabase.CopyAsset(sourcePath, copyPath))
                return copies;

            var mainCopy = AssetDatabase.LoadAssetAtPath<Mesh>(copyPath);
            copies[mainMesh] = mainCopy;

            var sourceCollider = FindColliderMesh(mainMesh);
            var colliderCopy = FindColliderMesh(mainCopy);

            if (sourceCollider != null && colliderCopy != null)
                copies[sourceCollider] = colliderCopy;

            return copies;
        }


        // ── Префаб, загруженный кодом ────────────────────────────────────

        // Пересборка префаба через LoadPrefabContents: его сцена — не открытый префаб, а у построек в ней — папка мешей этого префаба.
        public static void BeginLoadedPrefab(Scene contentsScene, string prefabPath)
        {
            LoadedPrefabPaths[contentsScene] = prefabPath;
        }


        public static void EndLoadedPrefab(Scene contentsScene)
        {
            LoadedPrefabPaths.Remove(contentsScene);
        }


        // Сцена префаба, загруженного кодом, — не сцена уровня и не открытый в редакторе префаб.
        public static bool IsLoadedPrefabContents(Scene scene)
        {
            var openedPrefab = PrefabStageUtility.GetCurrentPrefabStage();
            return EditorSceneManager.IsPreviewScene(scene) && (openedPrefab == null || openedPrefab.scene != scene);
        }


        // ── Запись на диск ───────────────────────────────────────────────

        // Записать файл меша чуть позже — после пачки правок одним разом.
        public static void RequestSave(Mesh mesh)
        {
            if (!IsBoxModelMeshFile(mesh))
                return;

            MeshesToSave.Add(mesh);
            saveTime = EditorApplication.timeSinceStartup + SaveDelaySeconds;
        }

        private static void SavePendingMeshes()
        {
            if (MeshesToSave.Count == 0 || EditorApplication.timeSinceStartup < saveTime || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            foreach (var mesh in MeshesToSave)
                if (mesh != null)
                    AssetDatabase.SaveAssetIfDirty(mesh);

            MeshesToSave.Clear();
        }


        // ── Меш для столкновений ─────────────────────────────────────────

        // Второй меш в файле основного — для коллайдера со скатами лестниц.
        public static Mesh GetOrCreateColliderMesh(Mesh mainMesh)
        {
            var existing = FindColliderMesh(mainMesh);

            if (existing != null)
                return existing;

            var colliderMesh = new Mesh { name = mainMesh.name + ColliderMeshSuffix };
            AssetDatabase.AddObjectToAsset(colliderMesh, mainMesh);
            return colliderMesh;
        }


        // Лестниц больше нет — второй меш не нужен.
        public static void RemoveColliderMesh(Mesh mainMesh)
        {
            var existing = FindColliderMesh(mainMesh);

            if (existing == null)
                return;

            AssetDatabase.RemoveObjectFromAsset(existing);
            Object.DestroyImmediate(existing, true);
        }

        private static Mesh FindColliderMesh(Mesh mainMesh)
        {
            if (!IsBoxModelMeshFile(mainMesh))
                return null;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(mainMesh)))
                if (asset is Mesh mesh && mesh != mainMesh)
                    return mesh;

            return null;
        }


        // Развёртка под запекание света — долгая, поэтому только при сохранении сцены и перед запеканием, а не на каждую правку.
        public static void AddLightmapUVs(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount == 0 || mesh.uv2.Length > 0)
                return;

            Unwrapping.GenerateSecondaryUVSet(mesh);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
        }


        // ── Папка ────────────────────────────────────────────────────────

        // Папка мешей сцены или префаба по его файлу: «<папка>/<Имя>_BoxModels»; файла нет (не сохранено) — папка из настроек проекта.
        public static string FolderNextTo(string sceneOrPrefabPath)
        {
            if (string.IsNullOrEmpty(sceneOrPrefabPath))
                return FallbackFolder;

            var directory = Path.GetDirectoryName(sceneOrPrefabPath)?.Replace('\\', '/');
            return directory + "/" + Path.GetFileNameWithoutExtension(sceneOrPrefabPath) + FolderSuffix;
        }

        private static string FolderFor(GameObject owner)
        {
            if (LoadedPrefabPaths.TryGetValue(owner.scene, out var loadedPrefabPath))
                return FolderNextTo(loadedPrefabPath);

            var prefabStage = PrefabStageUtility.GetPrefabStage(owner);
            return FolderNextTo(prefabStage != null ? prefabStage.assetPath : owner.scene.path);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var slash = folderPath.LastIndexOf('/');
            var parent = folderPath.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderPath.Substring(slash + 1));
        }

        private static string SafeFileName(string name)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            return name;
        }
    }
}
