using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Строка о выделенном под палитрой окна Live Builder: что выделено и какого размера (<see cref="LiveBuilderSelectionInfo"/>),
    /// цветом вкладки «Выделено» — видно при любой открытой вкладке. Клик по строке — открыть «Выделено».
    /// Ничего не выделено или выделена не постройка — бледная строка, как выделить.
    /// </summary>
    public static class LiveBuilderSelectionStrip
    {
        private const float Height = 22f;
        private const float IconSize = 14f;
        private const float AccentWidth = 3f;
        private const float Padding = 6f;

        private static GUIStyle textStyle;
        private static GUIStyle quietTextStyle;


        private static GUIStyle TextStyle => textStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };

        private static GUIStyle QuietTextStyle => quietTextStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };


        // Рисует строку во всю ширину. Возвращает, нажали ли по ней, когда есть что открыть.
        public static bool Draw()
        {
            var rect = EditorGUILayout.GetControlRect(false, Height);
            var selected = Selection.activeGameObject;
            var isLiveBuilderObject = LiveBuilderSelectionInfo.TryDescribe(selected, out var info);
            var color = LiveBuilderTabBar.ColorOf(LiveBuilderTabColor.Selection);

            if (Event.current.type == EventType.Repaint)
            {
                var isHovered = isLiveBuilderObject && rect.Contains(Event.current.mousePosition);
                EditorGUI.DrawRect(rect, isLiveBuilderObject ? LiveBuilderTabBar.TintOf(LiveBuilderTabColor.Selection, isHovered ? 0.3f : 0.18f) : new Color(0.5f, 0.5f, 0.5f, 0.08f));

                if (isLiveBuilderObject)
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, AccentWidth, rect.height), color);

                var iconRect = new Rect(rect.x + AccentWidth + Padding, rect.center.y - IconSize / 2f, IconSize, IconSize);
                LiveBuilderToolIcons.Draw(iconRect, LiveBuilderToolPalette.Get<LiveBuilderSelectTool>(), isLiveBuilderObject ? color : LiveBuilderIconCache.ThemedColor);
            }

            var textRect = new Rect(rect.x + AccentWidth + Padding * 2f + IconSize, rect.y, rect.width - AccentWidth - Padding * 3f - IconSize, rect.height);
            var text = isLiveBuilderObject ? info.KindName + " «" + selected.name + "» · " + info.SizeText
                : selected != null ? "«" + selected.name + "» — не постройка Live Builder"
                : "Ничего не выделено — кликни по постройке в окне сцены";

            GUI.Label(textRect, text, isLiveBuilderObject ? TextStyle : QuietTextStyle);

            // Подсказка — у кнопки поверх строки: она последней под мышью.
            var tooltip = isLiveBuilderObject ? text + "\nШ — ширина по X объекта, Г — глубина по Z, В — высота.\nКлик — открыть вкладку «Выделено»" : text;
            return GUI.Button(rect, new GUIContent("", tooltip), GUIStyle.none) && isLiveBuilderObject;
        }
    }
}
