using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Общие элементы окна Live Builder, чтобы всё выглядело одинаково: карточка с тихим заголовком, переключатель из кнопок (со значками или словами),
    /// кнопка-значок «вкл / выкл», плитка инструмента, свёрнутое «Ещё» с редкими настройками, пояснение (видно, пока на вкладке нажата «?»),
    /// плашка посчитанного числа, разделитель, поле материала. Вкладки окна — в <see cref="LiveBuilderTabBar"/>. Цвета — под светлую и тёмную тему редактора.
    /// </summary>
    public static class LiveBuilderWindowControls
    {
        // Сторона значка на кнопках и у подписей, пикс.
        public const float IconSize = 14f;

        private const string MoreSectionPreferencePrefix = "FeatherKit.LiveBuilder.More.";
        private const float MoreSectionHeight = 20f;

        // Насколько бледная выключенная кнопка-значок: 1 — как включённая.
        private const float OffToggleAlpha = 0.45f;

        private static GUIStyle cardStyle;
        private static GUIStyle cardTitleStyle;
        private static GUIStyle tileLabelStyle;
        private static GUIStyle selectedTileLabelStyle;
        private static GUIStyle hotkeyStyle;
        private static GUIStyle selectedHotkeyStyle;
        private static GUIStyle moreTitleStyle;
        private static GUIStyle moreSummaryStyle;
        private static GUIStyle valueChipStyle;


        // ── Показ пояснений ──────────────────────────────────────────────

        // Видны ли пояснения: окно ставит перед рисованием вкладки — нажата ли на ней «?», и снимает после.
        public static bool IsShowingExplanations { get; set; }


        // ── Цвета ────────────────────────────────────────────────────────

        public static Color AccentColor => EditorGUIUtility.isProSkin ? new Color(0.35f, 0.6f, 1f) : new Color(0.1f, 0.38f, 0.82f);


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

        private static GUIStyle MoreTitleStyle => moreTitleStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleLeft };

        private static GUIStyle MoreSummaryStyle => moreSummaryStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip,
            normal = { textColor = MutedTextColor }
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
            // 30 — поля окна и вкладки и полоса прокрутки.
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
            return DrawIconToggle(value, new GUIContent(" " + text, LiveBuilderOptionIcons.Themed(icon), tooltip), GUILayout.MinWidth(30f));
        }


        // То же без слова: слово — во всплывающей подсказке.
        public static bool IconOnlyToggle(bool value, LiveBuilderOptionIcon icon, string tooltip, params GUILayoutOption[] options)
        {
            return DrawIconToggle(value, new GUIContent(LiveBuilderOptionIcons.Themed(icon), tooltip), options);
        }


        // Нажатая — яркая, отжатая — бледная.
        private static bool DrawIconToggle(bool value, GUIContent content, params GUILayoutOption[] options)
        {
            using (new EditorGUIUtility.IconSizeScope(new Vector2(IconSize, IconSize)))
            {
                var previous = GUI.contentColor;

                if (!value)
                    GUI.contentColor = new Color(1f, 1f, 1f, OffToggleAlpha);

                value = GUILayout.Toggle(value, content, EditorStyles.miniButton, options);
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


        // ── Свёрнутое «Ещё» ──────────────────────────────────────────────

        // Строка «Ещё» перед редкими настройками; возвращает, рисовать ли их. key — под ним помнится, раскрыто ли; summary — что внутри, серым.
        // Есть изменённые, не как по умолчанию (hasNonDefaultValues), — раскрыто всегда и помечено «изменено»: спрятанное не удивит.
        public static bool MoreSection(string key, bool hasNonDefaultValues, string summary)
        {
            var preferenceKey = MoreSectionPreferencePrefix + key;
            var isOpen = hasNonDefaultValues || EditorPrefs.GetBool(preferenceKey, false);
            var rect = GUILayoutUtility.GetRect(0f, MoreSectionHeight, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
                DrawMoreSectionLook(rect, isOpen, hasNonDefaultValues, summary);

            var tooltip = hasNonDefaultValues ? "Здесь есть изменённые настройки — поэтому раскрыто. Верни их как было — свернётся" : summary;

            if (!GUI.Button(rect, new GUIContent("", tooltip), GUIStyle.none) || hasNonDefaultValues)
                return isOpen;

            // Настройки появятся или пропадут — этот проход окна прерываем, иначе IMGUI путается в разметке.
            EditorPrefs.SetBool(preferenceKey, !isOpen);
            GUIUtility.ExitGUI();
            return !isOpen;
        }

        // Стрелка, «Ещё» и серым — что внутри. Изменено — вместо стрелки точка цвета акцента: свернуть всё равно нельзя.
        private static void DrawMoreSectionLook(Rect rect, bool isOpen, bool hasNonDefaultValues, string summary)
        {
            if (!hasNonDefaultValues && rect.Contains(Event.current.mousePosition))
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, HoverColor, 0f, 3f);

            var arrowRect = new Rect(rect.x + 2f, rect.y + (rect.height - 13f) / 2f, 13f, 13f);

            if (hasNonDefaultValues)
                GUI.DrawTexture(new Rect(arrowRect.x + 3f, arrowRect.y + 3f, 7f, 7f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, AccentColor, 0f, 3.5f);
            else
                EditorStyles.foldout.Draw(arrowRect, GUIContent.none, false, false, isOpen, false);

            var title = new GUIContent("Ещё");
            var titleWidth = MoreTitleStyle.CalcSize(title).x;
            MoreTitleStyle.Draw(new Rect(arrowRect.xMax + 3f, rect.y, titleWidth, rect.height), title, false, false, false, false);

            var detail = new GUIContent(hasNonDefaultValues ? "изменено · " + summary : summary);
            var detailX = arrowRect.xMax + 3f + titleWidth + 4f;
            MoreSummaryStyle.Draw(new Rect(detailX, rect.y, rect.xMax - detailX, rect.height), detail, false, false, false, false);
        }


        // ── Пояснения ────────────────────────────────────────────────────

        // Как пользоваться — длинное пояснение: видно, только пока на вкладке нажата «?» (<see cref="IsShowingExplanations"/>), иначе его нет совсем.
        public static void Explanation(string text)
        {
            if (IsShowingExplanations)
                Note(text);
        }


        // Мелкий серый текст-пояснение — всегда виден. Длинное «как пользоваться» — в Explanation.
        public static void Note(string text)
        {
            GUILayout.Label(text, EditorStyles.wordWrappedMiniLabel);
        }


        // ── Плитка инструмента ───────────────────────────────────────────

        // Значок, подпись и клавиша; выбранная залита цветом вкладки «Инструмент», значок и подпись — белые. Возвращает, нажали ли.
        public static bool ToolTile(Rect rect, LiveBuilderTool tool, bool isSelected)
        {
            var isHovered = rect.Contains(Event.current.mousePosition);

            if (Event.current.type == EventType.Repaint)
                DrawToolTileLook(rect, tool, isSelected, isHovered);

            var content = new GUIContent("", tool.DisplayName + " (" + LiveBuilderToolPalette.HotkeyOf(tool) + ") — " + tool.Tooltip);
            return GUI.Button(rect, content, GUIStyle.none);
        }

        private static void DrawToolTileLook(Rect rect, LiveBuilderTool tool, bool isSelected, bool isHovered)
        {
            var fill = isSelected ? LiveBuilderTabBar.ColorOf(LiveBuilderTabColor.Tool) : isHovered ? HoverColor : TileColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, fill, 0f, 4f);

            if (!isSelected)
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, BorderColor, 1f, 4f);

            var iconSize = Mathf.Min(20f, rect.height - 22f);
            var iconRect = new Rect(rect.center.x - iconSize / 2f, rect.y + 7f, iconSize, iconSize);
            LiveBuilderToolIcons.Draw(iconRect, tool, isSelected ? Color.white : isHovered ? LiveBuilderIconCache.ThemedColor : DimIconColor);

            var labelStyle = isSelected ? SelectedTileLabelStyle : TileLabelStyle;
            labelStyle.Draw(new Rect(rect.x + 2f, rect.yMax - 17f, rect.width - 4f, 14f), tool.DisplayName, false, false, false, false);
            (isSelected ? SelectedHotkeyStyle : HotkeyStyle).Draw(rect, LiveBuilderToolPalette.HotkeyOf(tool), false, false, false, false);
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
