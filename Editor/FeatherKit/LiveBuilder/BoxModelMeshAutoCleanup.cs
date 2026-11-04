using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Сохранил сцену — лишние файлы мешей в её папке «<Сцена>_BoxModels» сами уходят в корзину системы, с одной строкой в консоли.
    /// «Лишний» — как у кнопки уборки (<see cref="BoxModelMeshCleanup"/>): на него не ссылается ни одна сцена и ни один префаб проекта
    /// и ни одна открытая сцена. Только папка сохранённой сцены, не весь проект. Выключается в настройках проекта Live Builder.
    /// </summary>
    [InitializeOnLoad]
    public static class BoxModelMeshAutoCleanup
    {
        static BoxModelMeshAutoCleanup()
        {
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }


        // Убрать после сохранения, а не во время: в разгар записи сцены базу ассетов не трогаем.
        private static void OnSceneSaved(Scene scene)
        {
            if (!LiveBuilderProjectSettings.instance.IsUnusedMeshCleanupOnSave || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var scenePath = scene.path;
            EditorApplication.delayCall += () => RemoveUnusedNextTo(scenePath);
        }


        // Лишние файлы мешей в папке сцены — в корзину. Возвращает, сколько убрано.
        public static int RemoveUnusedNextTo(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
                return 0;

            var unused = BoxModelMeshCleanup.FindUnusedMeshFiles(BoxModelMeshStorage.FolderNextTo(scenePath));

            if (unused.Count == 0)
                return 0;

            var removed = BoxModelMeshCleanup.MoveToTrash(unused);
            Debug.Log("Live Builder: убрано лишних файлов мешей рядом со сценой «" + System.IO.Path.GetFileNameWithoutExtension(scenePath) + "» — " +
                      removed + ", в корзину системы.");
            return removed;
        }
    }
}
