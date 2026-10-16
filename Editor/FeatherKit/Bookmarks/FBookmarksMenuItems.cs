using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// «Добавить в закладки» по правому клику в Project и Hierarchy.
    /// Кладёт в активную папку открытой вкладки — ту, по которой кликнули последней.
    /// </summary>
    public static class FBookmarksMenuItems
    {
        [MenuItem("Assets/Add to Bookmarks", false, 32)]
        private static void AddSelectedAssets()
        {
            AddToActiveFolder(Selection.objects);
        }

        [MenuItem("Assets/Add to Bookmarks", true)]
        private static bool CanAddSelectedAssets()
        {
            return Selection.objects.Length > 0;
        }


        // Приоритет 10 — с ним пункт попадает и в меню правого клика в Hierarchy.
        // Оттуда он зовётся по разу на каждый выделенный объект — всё выделение берём только на одном.
        [MenuItem("GameObject/Add to Bookmarks", false, 10)]
        private static void AddSelectedSceneObjects(MenuCommand command)
        {
            if (command.context != null && command.context != Selection.activeGameObject)
                return;

            AddToActiveFolder(Selection.gameObjects);
        }

        [MenuItem("GameObject/Add to Bookmarks", true)]
        private static bool CanAddSelectedSceneObjects()
        {
            return Selection.gameObjects.Length > 0;
        }


        private static void AddToActiveFolder(Object[] targets)
        {
            var storage = FBookmarksStorage.instance;
            var folder = storage.ActiveFolder;
            var rejected = storage.AddBookmarks(folder, targets);
            var window = FBookmarksWindow.Open();

            window.ShowNotification(new GUIContent(rejected > 0
                ? FBookmarksWindow.DescribeRejected(rejected)
                : $"Добавлено в «{folder.Name}»"));
        }
    }
}
