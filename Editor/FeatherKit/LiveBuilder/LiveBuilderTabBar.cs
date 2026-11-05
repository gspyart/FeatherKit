using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Вид вкладок окна Live Builder (<see cref="LiveBuilderWindowTab"/>, цвет — <see cref="LiveBuilderTabColor"/>): ряд корешков со значком, названием
    /// и пометкой (сколько выделено), кнопка «?» справа — пояснения открытой вкладки, и подкраска страницы цветом открытой вкладки.
    /// Окно узкое и название не влезает — на корешке только значок, название — во всплывающей подсказке.
    /// Цвета — под светлую и тёмную тему редактора; ими же красятся выбранная плитка палитры и подсказка внизу окна.
    /// </summary>
    public static class LiveBuilderTabBar
    {
        public const float Height = 26f;

        private const float TabGap = 3f;
        private const float HelpButtonWidth = 22f;
        private const float IconSize = 14f;
        private const float ContentGap = 4f;
        private const float SidePadding = 4f;
        private const float UnderlineHeight = 2f;

        // Насколько виден корешок вкладки, которую сейчас не открыть.
        private const float UnavailableAlpha = 0.35f;

        private static GUIStyle titleStyle;
        private static GUIStyle selectedTitleStyle;
        private static GUIStyle badgeStyle;
        private static GUIStyle helpStyle;


        // ── Цвета ────────────────────────────────────────────────────────

        public static Color ColorOf(LiveBuilderTabColor tab)
        {
            var isDark = EditorGUIUtility.isProSkin;

            return tab switch
            {
                LiveBuilderTabColor.Tool => isDark ? new Color(0.23f, 0.46f, 0.78f) : new Color(0.16f, 0.42f, 0.82f),
                LiveBuilderTabColor.Selection => isDark ? new Color(0.8f, 0.52f, 0.17f) : new Color(0.85f, 0.5f, 0.1f),
                _ => isDark ? new Color(0.46f, 0.4f, 0.76f) : new Color(0.42f, 0.35f, 0.75f)
            };
        }


        // Лёгкая подкраска цветом вкладки.
        public static Color TintOf(LiveBuilderTabColor tab, float alpha = 0.07f)
        {
            var color = ColorOf(tab);
            color.a = alpha;
            return color;
        }


        private static Color TabColor => EditorGUIUtility.isProSkin ? new Color(0.27f, 0.27f, 0.27f) : new Color(0.86f, 0.86f, 0.86f);

        private static Color HoverColor => EditorGUIUtility.isProSkin ? new Color(0.33f, 0.33f, 0.33f) : new Color(0.93f, 0.93f, 0.93f);

        private static Color BorderColor => EditorGUIUtility.isProSkin ? new Color(0.13f, 0.13f, 0.13f) : new Color(0.6f, 0.6f, 0.6f);


        // ── Стили ────────────────────────────────────────────────────────

        private static GUIStyle TitleStyle => titleStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };

        private static GUIStyle SelectedTitleStyle => selectedTitleStyle ??= new GUIStyle(TitleStyle) { normal = { textColor = Color.white } };

        private static GUIStyle BadgeStyle => badgeStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(4, 4, 0, 0),
            normal = { textColor = Color.white }
        };

        private static GUIStyle HelpStyle => helpStyle ??= new GUIStyle(EditorStyles.miniButton) { fontStyle = FontStyle.Bold, fontSize = 11 };


        // ── Ряд вкладок ──────────────────────────────────────────────────

        // Корешки во всю ширину ряда, кроме «?» справа. Возвращает открытую вкладку — нажатую или прежнюю; isHelpShown — нажата ли «?».
        public static int Draw(Rect row, IReadOnlyList<LiveBuilderWindowTab> tabs, int selectedIndex, ref bool isHelpShown)
        {
            var tabsWidth = row.width - HelpButtonWidth - TabGap;
            var widths = TabWidths(tabs, tabsWidth - TabGap * (tabs.Count - 1));
            var x = row.x;

            for (var index = 0; index < tabs.Count; index++)
            {
                var tabRect = new Rect(x, row.y, widths[index], row.height - UnderlineHeight);
                x += widths[index] + TabGap;

                if (DrawTab(tabRect, tabs[index], index == selectedIndex))
                    selectedIndex = index;
            }

            var selectedColor = ColorOf(tabs[selectedIndex].TabColor);

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(row.x, row.yMax - UnderlineHeight, tabsWidth, UnderlineHeight), selectedColor);

            var helpRect = new Rect(row.xMax - HelpButtonWidth, row.y + 2f, HelpButtonWidth, row.height - UnderlineHeight - 4f);
            isHelpShown = DrawHelpButton(helpRect, isHelpShown, selectedColor);
            return selectedIndex;
        }


        // Ширины корешков: названия влезают все — каждому по его надобности и поровну остатка; не влезают — поровну, и узкие корешки остаются со значком.
        private static float[] TabWidths(IReadOnlyList<LiveBuilderWindowTab> tabs, float available)
        {
            var widths = new float[tabs.Count];
            var total = 0f;

            for (var index = 0; index < tabs.Count; index++)
            {
                widths[index] = WidthWithTitle(tabs[index]);
                total += widths[index];
            }

            var extra = (available - total) / tabs.Count;

            for (var index = 0; index < tabs.Count; index++)
                widths[index] = extra >= 0f ? widths[index] + extra : available / tabs.Count;

            return widths;
        }


        // Сколько нужно корешку, чтобы влезли значок, название и пометка.
        private static float WidthWithTitle(LiveBuilderWindowTab tab)
        {
            var badgeWidth = BadgeWidth(tab.Badge);
            return SidePadding * 2f + IconSize + ContentGap + TitleStyle.CalcSize(new GUIContent(tab.Title)).x + (badgeWidth > 0f ? ContentGap + badgeWidth : 0f);
        }


        // Корешок: открытый залит цветом вкладки, значок и название белые; закрытый — серый, значок цветом вкладки;
        // недоступный — бледный и не нажимается. Возвращает, нажали ли.
        private static bool DrawTab(Rect rect, LiveBuilderWindowTab tab, bool isSelected)
        {
            var badge = tab.Badge;
            var badgeContent = string.IsNullOrEmpty(badge) ? null : new GUIContent(badge);
            var badgeWidth = BadgeWidth(badge);
            var titleContent = new GUIContent(tab.Title);
            var titleWidth = TitleStyle.CalcSize(titleContent).x;
            var isShowingTitle = WidthWithTitle(tab) <= rect.width + 0.5f;
            var isAvailable = tab.IsAvailable;

            if (Event.current.type == EventType.Repaint)
            {
                var previousColor = GUI.color;

                if (!isAvailable)
                    GUI.color = new Color(1f, 1f, 1f, UnavailableAlpha);

                var isHovered = isAvailable && rect.Contains(Event.current.mousePosition);
                var color = ColorOf(tab.TabColor);
                DrawTabBackground(rect, isSelected, isHovered, color);

                var contentWidth = IconSize + (isShowingTitle ? ContentGap + titleWidth : 0f) + (badgeContent != null ? ContentGap + badgeWidth : 0f);
                var x = rect.center.x - contentWidth / 2f;
                LiveBuilderIconCache.Draw(new Rect(x, rect.center.y - IconSize / 2f, IconSize, IconSize), tab.Icon, isSelected ? Color.white : color);
                x += IconSize + ContentGap;

                if (isShowingTitle)
                {
                    (isSelected ? SelectedTitleStyle : TitleStyle).Draw(new Rect(x, rect.y, titleWidth, rect.height), titleContent, false, false, false, false);
                    x += titleWidth + ContentGap;
                }

                if (badgeContent != null)
                    DrawBadge(new Rect(x, rect.center.y - 7f, badgeWidth, 14f), badgeContent, isSelected, color);

                GUI.color = previousColor;
            }

            var tooltip = !isAvailable ? tab.Title + " — " + tab.UnavailableReason : badgeContent != null ? tab.Title + " · " + badge : tab.Title;
            return GUI.Button(rect, new GUIContent("", tooltip), GUIStyle.none) && !isSelected && isAvailable;
        }

        private static void DrawTabBackground(Rect rect, bool isSelected, bool isHovered, Color color)
        {
            var fill = isSelected ? color : isHovered ? HoverColor : TabColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, fill, new Vector4(0f, 0f, 0f, 0f), new Vector4(4f, 4f, 0f, 0f));

            if (!isSelected)
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, BorderColor, new Vector4(1f, 1f, 1f, 0f), new Vector4(4f, 4f, 0f, 0f));
        }

        // Ширина пометки; нет её — 0.
        private static float BadgeWidth(string badge)
        {
            return string.IsNullOrEmpty(badge) ? 0f : Mathf.Max(16f, BadgeStyle.CalcSize(new GUIContent(badge)).x);
        }


        // Пометка-пилюля: на открытой — белая полупрозрачная, на закрытой — цветом вкладки.
        private static void DrawBadge(Rect rect, GUIContent content, bool isSelected, Color color)
        {
            var fill = isSelected ? new Color(1f, 1f, 1f, 0.28f) : color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, fill, 0f, 7f);
            BadgeStyle.Draw(rect, content, false, false, false, false);
        }


        // «?» — пояснения открытой вкладки: нажата — видны, цветом вкладки.
        private static bool DrawHelpButton(Rect rect, bool isHelpShown, Color color)
        {
            var previous = GUI.backgroundColor;

            if (isHelpShown)
                GUI.backgroundColor = color;

            var content = new GUIContent("?", isHelpShown ? "Пояснения этой вкладки видны. Клик — спрятать" : "Показать пояснения этой вкладки: как пользоваться");
            isHelpShown = GUI.Toggle(rect, isHelpShown, content, HelpStyle);
            GUI.backgroundColor = previous;
            return isHelpShown;
        }


        // ── Страница ─────────────────────────────────────────────────────

        // Подкраска страницы под корешками цветом открытой вкладки.
        public static void DrawPageBackground(Rect rect, LiveBuilderTabColor tab)
        {
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, TintOf(tab));
        }
    }
}
