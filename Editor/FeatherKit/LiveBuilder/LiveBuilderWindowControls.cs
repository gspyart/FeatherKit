using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Общие элементы окна Live Builder, чтобы всё выглядело одинаково: карточка с тихим заголовком, переключатель из кнопок (со значками или словами),
    /// кнопка-значок «вкл / выкл», плитка инструмента, пояснение, которое раскрывается по клику, плашка посчитанного числа, разделитель, поле материала.
    /// Цветные зоны окна — в <see cref="LiveBuilderWindowZones"/>. Цвета — под светлую и тёмную тему редактора.
    /// </summary>
    public static class LiveBuilderWindowControls
    {
        // Сторона значка на кнопках и у подписей, пикс.
        public const float IconSize = 14f;

        private const string ExplanationPreferencePrefix = "FeatherKit.LiveBuilder.Explanation.";
        private const string AllExplanationsPreferenceKey = "FeatherKit.LiveBuilder.ShowAllExplanations";

        // Насколько бледная выключенная кнопка-значок: 1 — как включённая.
        private const float OffToggleAlpha = 0.45f;

        private static GUIStyle cardStyle;
        private static GUIStyle cardTitleStyle;
        private static GUIStyle tileLabelStyle;
        private static GUIStyle selectedTileLabelStyle;
        private static GUIStyle hotkeyStyle;
        private static GUIStyle selectedHotkeyStyle;
        private static GUIStyle explanationLinkStyle;
        private static GUIStyle valueChipStyle;


        // ── Показ пояснений ──────────────────────────────────────────────

        // Показывать все пояснения сразу — кнопка «i» в шапке окна. Выключено — каждое раскрывается своей строкой.
        public static bool IsShowingAllExplanations
        {
            get => EditorPrefs.GetBool(AllExplanationsPreferenceKey, false);
            set => EditorPrefs.SetBool(AllExplanationsPreferenceKey, value);
        }


        // ── Цвета ────────────────────────────────────────────────────────

        public static Color AccentColor => EditorGUIUtility.isProSkin ? new Color(0.35f, 0.6f, 1f) : new Color(0.1f, 0.38f, 0.82f);

        public static Color IconColor => EditorGUIUtility.isProSkin ? new Color(0.78f, 0.78f, 0.78f) : new Color(0.25f, 0.25f, 0.25f);

        private static Color CardColor => EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.84f, 0.84f, 0.84f);

        private static Color BorderColor => EditorGUIUtility.isProSkin ? new Color(0.13f, 0.13f, 0.13f) : new Color(0.6f, 0.6f, 0.6f);

        private static Color TileColor => EditorGUIUtility.isProSkin ? new Color(0.27f, 0.27f, 0.27f) : new Color(0.86f, 0.86f, 0.86f);

        private static Color HoverColor => EditorGUIUtility.isProSkin ? new Color(0.33f, 0.33f, 0.33f) : new Color(0.93f, 0.93f, 0.93f);

        private static Color DimIconColor => EditorGUIUtility.isProSkin ? new Color(0.68f, 0.68f, 0.68f) : new Color(0.35f, 0.35f, 0.35f);

        private static Color MutedTextColor => EditorGUIUtility.isProSkin ? new Color(0.58f, 0.58f, 0.58f) : new Color(0.38f, 0.38f, 0.38f);

        private static Color ValueChipColor => EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.18f) : new Color(0f, 0f, 0f, 0.07f);


        // ── Стили ────────────────────────────────────────────────────────

        private static GUIStyle CardStyle => cardStyle ??= new GUIStyle { padding = new RectOffset(8, 8, 4, 6), margin = new RectOffset(0, 0, 3, 3) };

        private static GUIStyle CardTitleStyle => cardTitleStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = MutedTextColor } };

        private static GUIStyle TileLabelStyle => tileLabelStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };

        private static GUIStyle SelectedTileLabelStyle => selectedTileLabelStyle ??= new GUIStyle(TileLabelStyle) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };

        private static GUIStyle HotkeyStyle => hotkeyStyle ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.UpperRight, padding = new RectOffset(0, 4, 2, 0) };

        private static GUIStyle SelectedHotkeyStyle => selectedHotkeyStyle ??= new GUIStyle(HotkeyStyle) { normal = { textColor = new Color(1f, 1f, 1f, 0.75f) } };

        private static GUIStyle ExplanationLinkStyle => explanationLinkStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = MutedTextColor },
            hover = { textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black },
            padding = new RectOffset(0, 0, 1, 1)
        };

        private static GUIStyle ValueChipStyle => valueChipStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = MutedTextColor },
            padding = new RectOffset(6, 6, 1, 1),
            clipping = TextClipping.Clip
        };


        // ── Карточка ─────────────────────────────────────────────────────

        // Группа настроек в рамке с тихим серым заголовком. Использовать в using: всё внутри — содержимое карточки.
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

                GUILayout.Label(title, CardTitleStyle);
            }

            public void Dispose()
            {
                EditorGUILayout.EndVertical();
            }
        }


        // ── Переключатели ────────────────────────────────────────────────

        // Подпись и ряд кнопок со словами, одна нажата. Возвращает выбранную.
        public static int Switch(GUIContent label, int selected, params string[] options)
        {
            var contents = new GUIContent[options.Length];

            for (var index = 0; index < options.Length; index++)
                contents[index] = new GUIContent(options[index]);

            return DrawSwitch(label, selected, contents);
        }


        // Подпись и ряд кнопок со значками и словами. Слова не влезают в узкое окно — остаются значки, слово — в подсказке.
        public static int Switch(GUIContent label, int selected, params (string Text, LiveBuilderOptionIcon Icon)[] options)
        {
            var isShowingText = CanFitTexts(label, options);
            var contents = new GUIContent[options.Length];

            for (var index = 0; index < options.Length; index++)
            {
                var icon = LiveBuilderOptionIcons.Themed(options[index].Icon);
                contents[index] = isShowingText ? new GUIContent(" " + options[index].Text, icon) : new GUIContent(icon, options[index].Text);
            }

            return DrawSwitch(label, selected, contents);
        }

        // Влезут ли слова на кнопки при нынешней ширине окна.
        private static bool CanFitTexts(GUIContent label, (string Text, LiveBuilderOptionIcon Icon)[] options)
        {
            // 30 — поля окна и зоны и полоса прокрутки.
            var available = EditorGUIUtility.currentViewWidth - (label != GUIContent.none ? EditorGUIUtility.labelWidth : 0f) - 30f;
            var widest = 0f;

            foreach (var option in options)
                widest = Mathf.Max(widest, EditorStyles.miniButton.CalcSize(new GUIContent(" " + option.Text)).x + IconSize);

            return available / options.Length >= widest;
        }


        // Да или нет двумя кнопками со словами: «нет» слева.
        public static bool Switch(GUIContent label, bool value, string offOption, string onOption)
        {
            return Switch(label, value ? 1 : 0, offOption, onOption) == 1;
        }


        // Да или нет двумя кнопками со значками: «нет» слева.
        public static bool Switch(GUIContent label, bool value, (string Text, LiveBuilderOptionIcon Icon) offOption, (string Text, LiveBuilderOptionIcon Icon) onOption)
        {
            return Switch(label, value ? 1 : 0, offOption, onOption) == 1;
        }


        // Кнопка-значок «вкл / выкл»: включённая — нажата и яркая, выключенная — бледная. Ставить в ряд, после своего PrefixLabel (ниже).
        public static bool IconToggle(bool value, LiveBuilderOptionIcon icon, string text, string tooltip)
        {
            using (new EditorGUIUtility.IconSizeScope(new Vector2(IconSize, IconSize)))
            {
                var previous = GUI.contentColor;

                if (!value)
                    GUI.contentColor = new Color(1f, 1f, 1f, OffToggleAlpha);

                value = GUILayout.Toggle(value, new GUIContent(" " + text, LiveBuilderOptionIcons.Themed(icon), tooltip), EditorStyles.miniButton, GUILayout.MinWidth(30f));
                GUI.contentColor = previous;
            }

            return value;
        }


        // Ряд кнопок, одна нажата. Значки на кнопках и у подписи — одного размера.
        private static int DrawSwitch(GUIContent label, int selected, GUIContent[] options)
        {
            using (new EditorGUIUtility.IconSizeScope(new Vector2(IconSize, IconSize)))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (label != GUIContent.none)
                    PrefixLabel(label);

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


        // ── Подписи и значения ───────────────────────────────────────────

        // Подпись строки со значком слева.
        public static GUIContent IconLabel(string text, LiveBuilderOptionIcon icon, string tooltip)
        {
            return new GUIContent(" " + text, LiveBuilderOptionIcons.Themed(icon), tooltip);
        }


        // Подпись строки в ряду, значок у неё — того же размера, что на кнопках.
        // Рисуется сразу: у EditorGUILayout.PrefixLabel подпись ждёт следующего поля и берёт его вид — перед плашкой или бледной кнопкой она бледнеет.
        public static void PrefixLabel(GUIContent label)
        {
            using (new EditorGUIUtility.IconSizeScope(new Vector2(IconSize, IconSize)))
            {
                var rect = GUILayoutUtility.GetRect(EditorGUIUtility.labelWidth, 18f, EditorStyles.label, GUILayout.ExpandWidth(false));
                var labelRect = EditorGUI.IndentedRect(rect);
                EditorGUI.HandlePrefixLabel(rect, labelRect, label, GUIUtility.GetControlID(FocusType.Passive), EditorStyles.label);
            }
        }


        // Посчитанное число, не поле ввода: подпись и бледная плашка — чтобы не путать с тем, что правится.
        public static void Computed(string label, string value, string tooltip = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                PrefixLabel(new GUIContent(label, tooltip));
                ValueChip(new GUIContent(value, tooltip));
            }
        }


        // Бледная плашка с текстом во всю оставшуюся ширину ряда или в заданную.
        public static void ValueChip(GUIContent content, params GUILayoutOption[] options)
        {
            var rect = EditorGUILayout.GetControlRect(false, 18f, options);

            if (Event.current.type != EventType.Repaint)
                return;

            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, ValueChipColor, 0f, 3f);
            ValueChipStyle.Draw(rect, content, false, false, false, false);
        }


        // ── Пояснения ────────────────────────────────────────────────────

        // Как пользоваться — длинное пояснение, спрятанное за строкой «Как пользоваться». Раскрыто или нет — помнится по key.
        public static void Explanation(string key, string text)
        {
            if (IsShowingAllExplanations)
            {
                Note(text);
                return;
            }

            var preferenceKey = ExplanationPreferencePrefix + key;
            var isOpen = EditorPrefs.GetBool(preferenceKey, false);
            var content = new GUIContent(isOpen ? "Скрыть пояснение" : "Как пользоваться");
            var rect = GUILayoutUtility.GetRect(IconSize + 4f + ExplanationLinkStyle.CalcSize(content).x, 16f, GUILayout.ExpandWidth(false));
            var isHovered = rect.Contains(Event.current.mousePosition);

            if (Event.current.type == EventType.Repaint)
            {
                LiveBuilderOptionIcons.Draw(new Rect(rect.x, rect.y + 2f, 12f, 12f), LiveBuilderOptionIcon.Info, isHovered ? IconColor : MutedTextColor);
                ExplanationLinkStyle.Draw(new Rect(rect.x + IconSize + 2f, rect.y, rect.width - IconSize - 2f, rect.height), content, isHovered, false, false, false);
            }

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                isOpen = !isOpen;
                EditorPrefs.SetBool(preferenceKey, isOpen);
            }

            if (isOpen)
                Note(text);
        }


        // Мелкий серый текст-пояснение — всегда виден. Длинное «как пользоваться» — в Explanation.
        public static void Note(string text)
        {
            GUILayout.Label(text, EditorStyles.wordWrappedMiniLabel);
        }


        // ── Плитка инструмента ───────────────────────────────────────────

        // Значок, подпись и клавиша; выбранная залита цветом зоны инструмента, значок и подпись — белые. Возвращает, нажали ли.
        public static bool ToolTile(Rect rect, LiveBuilderTool tool, bool isSelected)
        {
            var isHovered = rect.Contains(Event.current.mousePosition);

            if (Event.current.type == EventType.Repaint)
                DrawToolTileLook(rect, tool, isSelected, isHovered);

            var content = new GUIContent("", LiveBuilderToolSetting.DisplayName(tool) + " (" + LiveBuilderToolSetting.HotkeyOf(tool) + ") — " + LiveBuilderToolSetting.Tooltip(tool));
            return GUI.Button(rect, content, GUIStyle.none);
        }

        private static void DrawToolTileLook(Rect rect, LiveBuilderTool tool, bool isSelected, bool isHovered)
        {
            var fill = isSelected ? LiveBuilderWindowZones.ColorOf(LiveBuilderWindowZone.Tool) : isHovered ? HoverColor : TileColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, fill, 0f, 4f);

            if (!isSelected)
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, BorderColor, 1f, 4f);

            var iconSize = Mathf.Min(20f, rect.height - 22f);
            var iconRect = new Rect(rect.center.x - iconSize / 2f, rect.y + 7f, iconSize, iconSize);
            LiveBuilderToolIcons.Draw(iconRect, tool, isSelected ? Color.white : isHovered ? IconColor : DimIconColor);

            var labelStyle = isSelected ? SelectedTileLabelStyle : TileLabelStyle;
            labelStyle.Draw(new Rect(rect.x + 2f, rect.yMax - 17f, rect.width - 4f, 14f), LiveBuilderToolSetting.DisplayName(tool), false, false, false, false);
            (isSelected ? SelectedHotkeyStyle : HotkeyStyle).Draw(rect, LiveBuilderToolSetting.HotkeyOf(tool), false, false, false, false);
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


        // Заливка с рамкой в один пиксель, углы скруглены.
        private static void DrawFrame(Rect rect, Color fill)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, fill, 0f, 4f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, BorderColor, 1f, 4f);
        }
    }
}
