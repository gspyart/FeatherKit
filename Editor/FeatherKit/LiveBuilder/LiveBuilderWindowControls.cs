using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Общие элементы окна Live Builder, чтобы всё выглядело одинаково: карточка с заголовком, переключатель из кнопок,
    /// плитка инструмента, вкладки, разделитель, поле материала. Цвета — под светлую и тёмную тему редактора.
    /// </summary>
    public static class LiveBuilderWindowControls
    {
        private static GUIStyle cardStyle;
        private static GUIStyle tabStyle;
        private static GUIStyle tileLabelStyle;
        private static GUIStyle hotkeyStyle;


        // ── Цвета ────────────────────────────────────────────────────────

        public static Color AccentColor => EditorGUIUtility.isProSkin ? new Color(0.35f, 0.6f, 1f) : new Color(0.1f, 0.38f, 0.82f);

        public static Color IconColor => EditorGUIUtility.isProSkin ? new Color(0.78f, 0.78f, 0.78f) : new Color(0.25f, 0.25f, 0.25f);

        private static Color CardColor => EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.84f, 0.84f, 0.84f);

        private static Color BorderColor => EditorGUIUtility.isProSkin ? new Color(0.13f, 0.13f, 0.13f) : new Color(0.6f, 0.6f, 0.6f);

        private static Color HoverColor => EditorGUIUtility.isProSkin ? new Color(0.31f, 0.31f, 0.31f) : new Color(0.9f, 0.9f, 0.9f);

        private static Color SelectedColor => EditorGUIUtility.isProSkin ? new Color(0.2f, 0.3f, 0.45f) : new Color(0.74f, 0.82f, 0.95f);


        // ── Стили ────────────────────────────────────────────────────────

        private static GUIStyle CardStyle => cardStyle ??= new GUIStyle { padding = new RectOffset(8, 8, 6, 8), margin = new RectOffset(0, 0, 3, 3) };

        private static GUIStyle TabStyle => tabStyle ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(8, 8, 4, 6) };

        private static GUIStyle TileLabelStyle => tileLabelStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };

        private static GUIStyle HotkeyStyle => hotkeyStyle ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.UpperRight, padding = new RectOffset(0, 4, 2, 0) };


        // ── Карточка ─────────────────────────────────────────────────────

        // Группа настроек в рамке с заголовком. Использовать в using: всё внутри — содержимое карточки.
        public static CardScope Card(string title)
        {
            return new CardScope(title);
        }

        public readonly struct CardScope : System.IDisposable
        {
            public CardScope(string title)
            {
                var rect = EditorGUILayout.BeginVertical(CardStyle);

                if (Event.current.type == EventType.Repaint)
                    DrawFrame(rect, CardColor);

                GUILayout.Label(title, EditorStyles.boldLabel);
            }

            public void Dispose()
            {
                EditorGUILayout.EndVertical();
            }
        }


        // ── Переключатели ────────────────────────────────────────────────

        // Подпись и ряд кнопок, одна нажата. Возвращает выбранную.
        public static int Switch(GUIContent label, int selected, params string[] options)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (label != GUIContent.none)
                    EditorGUILayout.PrefixLabel(label);

                for (var index = 0; index < options.Length; index++)
                {
                    var style = options.Length == 1 ? EditorStyles.miniButton
                        : index == 0 ? EditorStyles.miniButtonLeft
                        : index == options.Length - 1 ? EditorStyles.miniButtonRight
                        : EditorStyles.miniButtonMid;

                    if (GUILayout.Toggle(index == selected, options[index], style, GUILayout.MinWidth(30f)) && index != selected)
                        selected = index;
                }
            }

            return selected;
        }


        // Да или нет двумя кнопками: «нет» слева.
        public static bool Switch(GUIContent label, bool value, string offOption, string onOption)
        {
            return Switch(label, value ? 1 : 0, offOption, onOption) == 1;
        }


        // ── Плитка инструмента ───────────────────────────────────────────

        // Значок, подпись и клавиша; выбранная — цветом акцента. Возвращает, нажали ли.
        public static bool ToolTile(Rect rect, LiveBuilderTool tool, bool isSelected)
        {
            var isHovered = rect.Contains(Event.current.mousePosition);

            if (Event.current.type == EventType.Repaint)
            {
                DrawFrame(rect, isSelected ? SelectedColor : isHovered ? HoverColor : CardColor);
                var iconSize = Mathf.Min(22f, rect.height - 22f);
                var iconRect = new Rect(rect.center.x - iconSize / 2f, rect.y + 6f, iconSize, iconSize);
                LiveBuilderToolIcons.Draw(iconRect, tool, isSelected ? AccentColor : IconColor);
                TileLabelStyle.Draw(new Rect(rect.x + 2f, rect.yMax - 17f, rect.width - 4f, 14f), LiveBuilderToolSetting.DisplayName(tool), false, false, false, false);
                HotkeyStyle.Draw(rect, LiveBuilderToolSetting.HotkeyOf(tool), false, false, false, false);
            }

            var content = new GUIContent("", LiveBuilderToolSetting.DisplayName(tool) + " (" + LiveBuilderToolSetting.HotkeyOf(tool) + ") — " + LiveBuilderToolSetting.Tooltip(tool));
            return GUI.Button(rect, content, GUIStyle.none);
        }


        // ── Вкладки ──────────────────────────────────────────────────────

        // Вкладки с чертой под выбранной. Возвращает выбранную.
        public static int Tabs(int selected, params GUIContent[] tabs)
        {
            var rect = EditorGUILayout.GetControlRect(false, 24f);
            var width = rect.width / tabs.Length;

            for (var index = 0; index < tabs.Length; index++)
            {
                var tabRect = new Rect(rect.x + index * width, rect.y, width, rect.height);
                var isSelected = index == selected;

                if (Event.current.type == EventType.Repaint)
                {
                    var previous = GUI.contentColor;
                    GUI.contentColor = isSelected ? GUI.contentColor : new Color(1f, 1f, 1f, 0.6f);
                    TabStyle.Draw(tabRect, tabs[index], tabRect.Contains(Event.current.mousePosition), isSelected, false, false);
                    GUI.contentColor = previous;

                    if (isSelected)
                        EditorGUI.DrawRect(new Rect(tabRect.x + 4f, tabRect.yMax - 2f, tabRect.width - 8f, 2f), AccentColor);
                }

                if (GUI.Button(tabRect, new GUIContent("", tabs[index].tooltip), GUIStyle.none))
                    selected = index;
            }

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax, rect.width, 1f), BorderColor);

            return selected;
        }


        // ── Прочее ───────────────────────────────────────────────────────

        // Тонкая линия между сегментами окна.
        public static void Separator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1f);

            if (Event.current.type == EventType.Repaint)
                DrawLine(new Rect(rect.x - 4f, rect.y, rect.width + 8f, 1f));
        }


        // Линия цвета рамок в заданном прямоугольнике.
        public static void DrawLine(Rect rect)
        {
            EditorGUI.DrawRect(rect, BorderColor);
        }


        public static Material MaterialField(string label, string tooltip, Material material)
        {
            return (Material)EditorGUILayout.ObjectField(new GUIContent(label, tooltip), material, typeof(Material), false);
        }


        // Мелкий серый текст-пояснение.
        public static void Note(string text)
        {
            GUILayout.Label(text, EditorStyles.wordWrappedMiniLabel);
        }


        // Заливка с рамкой в один пиксель.
        private static void DrawFrame(Rect rect, Color fill)
        {
            EditorGUI.DrawRect(rect, BorderColor);
            EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), fill);
        }
    }
}
