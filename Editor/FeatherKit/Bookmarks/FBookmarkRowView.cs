using System.IO;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Как выглядит закладка в окне: иконка, имя, приписка и подсказка.
    /// - найден, в закрытой сцене или потерян — у каждого свой вид
    /// - общие размеры строк окна закладок
    /// </summary>
    public static class FBookmarkRowView
    {
        public const float Height = 20f;
        public const float IconSize = 16f;
        public const float Gap = 4f;

        private const float Indent = 20f;

        private static readonly Color SelectedColor = new Color(0.24f, 0.48f, 0.9f, 0.45f);
        private static readonly Color NoteColor = new Color(0.55f, 0.55f, 0.55f);

        private static GUIStyle noteStyle;


        /// <summary>Серая мелкая приписка: сцена, «не найден», число закладок в папке.</summary>
        public static GUIStyle NoteStyle => noteStyle ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = NoteColor } };


        // ── Строка ───────────────────────────────────────────────────────

        public static void Draw(Rect rect, FBookmark bookmark, bool isSelected)
        {
            var target = bookmark.Resolve();
            var iconRect = new Rect(rect.x + Indent, rect.y + (rect.height - IconSize) * 0.5f, IconSize, IconSize);
            var labelRect = new Rect(iconRect.xMax + Gap, rect.y, rect.xMax - iconRect.xMax - Gap, rect.height);

            if (Event.current.type == EventType.Repaint)
            {
                if (isSelected)
                    EditorGUI.DrawRect(rect, SelectedColor);

                var icon = IconOf(bookmark, target);

                if (icon != null)
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }

            var name = new GUIContent(bookmark.DisplayName, TooltipOf(bookmark, target));

            DrawNameWithNote(labelRect, name, target != null ? EditorStyles.label : NoteStyle, NoteOf(bookmark, target));
        }


        /// <summary>Имя и сразу за ним серая приписка.</summary>
        public static void DrawNameWithNote(Rect rect, GUIContent name, GUIStyle nameStyle, string note)
        {
            var nameWidth = nameStyle.CalcSize(name).x;

            GUI.Label(rect, name, nameStyle);

            if (!string.IsNullOrEmpty(note))
                GUI.Label(new Rect(rect.x + nameWidth + Gap, rect.y, rect.width - nameWidth - Gap, rect.height), note, NoteStyle);
        }


        // ── Части строки ─────────────────────────────────────────────────

        private static Texture IconOf(FBookmark bookmark, Object target)
        {
            if (target != null)
                return AssetPreview.GetMiniThumbnail(target);

            return EditorGUIUtility.IconContent(bookmark.IsInClosedScene ? "SceneAsset Icon" : "console.warnicon.sm").image;
        }


        private static string NoteOf(FBookmark bookmark, Object target)
        {
            if (bookmark.IsInClosedScene)
                return SceneNameOf(bookmark) + " — закрыта";

            if (target == null)
                return "не найден";

            return bookmark.IsSceneObject ? SceneNameOf(bookmark) : string.Empty;
        }


        private static string TooltipOf(FBookmark bookmark, Object target)
        {
            if (target == null)
                return bookmark.IsInClosedScene
                    ? $"{bookmark.ScenePath}\nДвойной клик — открыть сцену и найти объект"
                    : "Объект удалён, или удалена его сцена";

            if (!bookmark.IsSceneObject)
                return AssetDatabase.GetAssetPath(target);

            return target is GameObject gameObject ? HierarchyPathOf(gameObject.transform) : target.name;
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static string SceneNameOf(FBookmark bookmark)
        {
            return Path.GetFileNameWithoutExtension(bookmark.ScenePath);
        }


        private static string HierarchyPathOf(Transform transform)
        {
            var path = transform.name;

            for (var parent = transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;

            return path;
        }
    }
}
