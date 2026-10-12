using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Что делает клик по закладке.
    /// - один клик: выделить и подсветить объект
    /// - двойной: открыть ассет или показать объект в окне сцены
    /// - объект в закрытой сцене: открыть её и найти объект
    /// </summary>
    public static class FBookmarkOpener
    {
        public static void Select(FBookmark bookmark)
        {
            var target = bookmark.Resolve();

            if (target != null)
            {
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
                return;
            }

            // Сцена закрыта — подсвечиваем её файл: видно, где искать.
            if (bookmark.IsInClosedScene)
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(bookmark.ScenePath));
        }


        public static void Open(FBookmark bookmark)
        {
            if (bookmark.IsInClosedScene && !TryOpenScene(bookmark))
                return;

            var target = bookmark.Resolve();

            if (target == null)
                return;

            if (bookmark.IsSceneObject)
                FrameInSceneView(target);
            else
                AssetDatabase.OpenAsset(target);
        }

        // Открытая сцена заменяет текущие, поэтому сначала спрашиваем про несохранённое.
        private static bool TryOpenScene(FBookmark bookmark)
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            EditorSceneManager.OpenScene(bookmark.ScenePath);
            FBookmarksStorage.instance.RetryNotFound();

            return true;
        }

        private static void FrameInSceneView(Object target)
        {
            Selection.activeObject = target;

            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
        }
    }
}
