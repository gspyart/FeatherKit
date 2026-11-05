using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Уборка файлов мешей Live Builder (<see cref="BoxModelMeshStorage"/>), на которые больше никто не ссылается: удалил объект,
    /// убрал этажи здания, отменил рисование — файл остался. Удалять файл вместе с объектом нельзя: префаб и копии могут делить файл.
    /// Поэтому с запасом: файл нужен, если на него ссылается хоть один ассет проекта на диске — сцена, префаб, ScriptableObject, таймлайн,
    /// контроллер анимаций, — или открытая сцена и открытый префаб как есть сейчас, с несохранёнными правками. Ненужные уходят в корзину системы — их можно вернуть.
    /// По всему проекту — по кнопке, с вопросом; рядом с сохранённой сценой — сами (<see cref="BoxModelMeshAutoCleanup"/>).
    /// Ctrl+Z после уборки вернёт объект без файла — Live Builder сам соберёт ему меш заново.
    /// </summary>
    public static class BoxModelMeshCleanup
    {
        // Файлы, которые на меш сослаться не могут: код, шейдеры, картинки, звук, видео, шрифты, текст, модели, материалы. Их не спрашиваем — быстрее.
        private static readonly HashSet<string> ExtensionsWithoutMeshReferences = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".dll", ".asmdef", ".asmref", ".shader", ".hlsl", ".cginc", ".compute",
            ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".exr", ".hdr", ".bmp", ".gif",
            ".wav", ".mp3", ".ogg", ".aif", ".aiff", ".flac", ".mp4", ".mov", ".webm",
            ".ttf", ".otf", ".txt", ".json", ".xml", ".md", ".bytes", ".csv",
            ".fbx", ".obj", ".blend", ".mat"
        };


        // Файлы мешей Live Builder, на которые не ссылается ничто в проекте и в открытых сценах; folder — только в этой папке, пусто — во всём проекте.
        // Сначала отсеивает нужные открытым сценам — дёшево; по всем ассетам проекта — один раз и только если кто-то остался.
        public static List<string> FindUnusedMeshFiles(string folder = null)
        {
            var searchFolder = string.IsNullOrEmpty(folder) ? "Assets" : folder;

            if (!AssetDatabase.IsValidFolder(searchFolder))
                return new List<string>();

            var unused = new SortedSet<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { searchFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (BoxModelMeshStorage.IsBoxModelMeshPath(path))
                    unused.Add(path);
            }

            if (unused.Count > 0)
                unused.ExceptWith(PathsUsedByOpenScenes());

            if (unused.Count > 0)
                unused.ExceptWith(PathsUsedByProjectAssets());

            return new List<string>(unused);
        }

        // Что нужно открытым сценам и открытому префабу сейчас, с несохранёнными правками.
        private static HashSet<string> PathsUsedByOpenScenes()
        {
            var used = new HashSet<string>();
            var roots = new List<Object>();

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);

                if (scene.isLoaded)
                    roots.AddRange(scene.GetRootGameObjects());
            }

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

            if (prefabStage != null)
                roots.Add(prefabStage.prefabContentsRoot);

            foreach (var dependency in EditorUtility.CollectDependencies(roots.ToArray()))
                if (dependency != null && EditorUtility.IsPersistent(dependency))
                    used.Add(AssetDatabase.GetAssetPath(dependency));

            return used;
        }

        // Что нужно ассетам проекта на диске: всем, кто может сослаться на меш, по прямым ссылкам — одним запросом.
        private static HashSet<string> PathsUsedByProjectAssets()
        {
            var referencingPaths = new List<string>();

            foreach (var path in AssetDatabase.GetAllAssetPaths())
                if (path.StartsWith("Assets/") && !BoxModelMeshStorage.IsBoxModelMeshPath(path)
                    && !ExtensionsWithoutMeshReferences.Contains(System.IO.Path.GetExtension(path)))
                    referencingPaths.Add(path);

            return new HashSet<string>(AssetDatabase.GetDependencies(referencingPaths.ToArray(), false));
        }


        // Спросить и убрать ненужные файлы в корзину. Возвращает, сколько убрано.
        public static int RemoveUnusedMeshFiles()
        {
            var unused = FindUnusedMeshFiles();

            if (unused.Count == 0)
            {
                EditorUtility.DisplayDialog("Лишние меши", "Лишних файлов мешей нет: на все ссылаются сцены, префабы или другие ассеты.", "Хорошо");
                return 0;
            }

            var message = "Файлов мешей, на которые не ссылается ни одна сцена, префаб или другой ассет: " + unused.Count + ".\n\n" + ListOf(unused) +
                          "\n\nОни уйдут в корзину системы — оттуда их можно вернуть.";

            if (!EditorUtility.DisplayDialog("Лишние меши", message, "Убрать в корзину", "Отмена"))
                return 0;

            return MoveToTrash(unused);
        }


        // Файлы — в корзину системы. Возвращает, сколько убрано.
        public static int MoveToTrash(List<string> paths)
        {
            var failed = new List<string>();
            AssetDatabase.MoveAssetsToTrash(paths.ToArray(), failed);
            return paths.Count - failed.Count;
        }

        // Первые несколько имён — чтобы видеть, что именно уйдёт.
        private static string ListOf(List<string> paths)
        {
            const int shownCount = 8;
            var lines = new List<string>();

            for (var index = 0; index < paths.Count && index < shownCount; index++)
                lines.Add(paths[index]);

            if (paths.Count > shownCount)
                lines.Add("… и ещё " + (paths.Count - shownCount));

            return string.Join("\n", lines);
        }
    }
}
