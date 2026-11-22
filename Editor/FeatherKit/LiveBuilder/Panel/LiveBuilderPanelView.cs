using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Что рисует панель Live Builder в окне сцены (<see cref="LiveBuilderPanelOverlay"/>), сверху вниз:
    /// - ряд 8 инструментов с клавишей в углу, рядом — окно уровня и выключить; строка инструмента — про новое (синяя полоса)
    /// - карточка выделенного (<see cref="LiveBuilderCardHeader"/>, <see cref="LiveBuilderSelectionCardSet"/>) прокручивается; пока рисуешь — секция Drawing
    /// - нижняя строка: этаж, сетка, прилипание, вид (<see cref="LiveBuilderCommonSettingsBar"/>)
    /// Уже 260 px — узкий вид: инструменты в два ряда, поля столбиком. Выключен Live Builder — одна кнопка «включить».
    /// </summary>
    public sealed class LiveBuilderPanelView : System.IDisposable
    {
        private const float NarrowWidth = 260f;
        private const float ToolButtonSize = 26f;
        private const float ToolIconSize = 16f;
        private const float AccentWidth = 3f;
        private const float SectionRowHeight = 20f;

        private static GUIStyle hotkeyStyle;
        private static GUIStyle toolNameStyle;
        private static GUIStyle emptySectionStyle;
        private static GUIStyle lineStyle;

        private readonly LiveBuilderSelectionCardSet cards = new();
        private Vector2 cardScrollPosition;
        private float cardContentHeight = 120f;
        private LiveBuilderDrawingReport drawingReport;


        private static GUIStyle HotkeyStyle => hotkeyStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 8,
            alignment = TextAnchor.LowerRight,
            padding = new RectOffset(0, 2, 0, 0)
        };

        private static GUIStyle ToolNameStyle => toolNameStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleLeft };

        private static GUIStyle EmptySectionStyle => emptySectionStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.58f, 0.58f, 0.58f) : new Color(0.38f, 0.38f, 0.38f) }
        };

        private static GUIStyle LineStyle => lineStyle ??= new GUIStyle { padding = new RectOffset(9, 4, 3, 3) };


        // Карточка стала выше или ниже — панель просит перерисовку: высоту прокрутки она узнаёт после разметки.
        public event System.Action RepaintRequested;


        // width — ширина панели; maxCardHeight — сколько места дать карточке, дальше она прокручивается.
        public void Draw(float width, float maxCardHeight)
        {
            var isNarrow = width <= NarrowWidth;
            var labelWidth = EditorGUIUtility.labelWidth;
            var wideMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.labelWidth = isNarrow ? 78f : Mathf.Clamp(width * 0.34f, 88f, 120f);
            EditorGUIUtility.wideMode = !isNarrow;

            try
            {
                using (LiveBuilderWindowControls.InPanel(width))
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(width)))
                    DrawContent(width, maxCardHeight, isNarrow);
            }
            finally
            {
                EditorGUIUtility.labelWidth = labelWidth;
                EditorGUIUtility.wideMode = wideMode;
            }
        }

        private void DrawContent(float width, float maxCardHeight, bool isNarrow)
        {
            if (!LiveBuilderActivation.IsToolActive)
            {
                DrawTurnOnButton();
                return;
            }

            DrawToolRow(isNarrow);
            DrawToolLine();
            LiveBuilderWindowControls.Separator();
            DrawCardOrDrawing(width, maxCardHeight);
            LiveBuilderWindowControls.Separator();
            LiveBuilderCommonSettingsBar.Draw(isNarrow);
        }


        // IDisposable: панель закрыта — секции отпускают готовые инспекторы.
        public void Dispose()
        {
            cards.Dispose();
        }


        // ── Выключен ─────────────────────────────────────────────────────

        private static void DrawTurnOnButton()
        {
            var key = LiveBuilderShortcutIds.KeyLabelOf(LiveBuilderShortcutIds.ToggleLiveBuilder);

            if (GUILayout.Button(new GUIContent("Turn on Live Builder (" + key + ")", "Включить инструмент сборки уровня; клавиши 1–8, S, [ ] — его, пока включён"),
                    GUILayout.Height(24f)))
                LiveBuilderActivation.SetToolActive(true);
        }


        // ── Ряд инструментов ─────────────────────────────────────────────

        // Один ряд из восьми, в узкой панели — два по четыре; справа — окно уровня и выключить.
        private static void DrawToolRow(bool isNarrow)
        {
            var tools = LiveBuilderToolPalette.Tools;
            var perRow = isNarrow ? Mathf.CeilToInt(tools.Count / 2f) : tools.Count;

            for (var first = 0; first < tools.Count; first += perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var index = first; index < Mathf.Min(first + perRow, tools.Count); index++)
                        DrawToolButton(tools[index]);

                    GUILayout.FlexibleSpace();

                    if (first + perRow >= tools.Count)
                        DrawPanelButtons();
                }
            }
        }

        private static void DrawToolButton(LiveBuilderTool tool)
        {
            var rect = GUILayoutUtility.GetRect(ToolButtonSize, ToolButtonSize, GUILayout.Width(ToolButtonSize), GUILayout.Height(ToolButtonSize));
            var isCurrent = LiveBuilderToolPalette.Current == tool;
            var hotkey = LiveBuilderToolPalette.HotkeyOf(tool);

            if (Event.current.type == EventType.Repaint)
            {
                var isHovered = rect.Contains(Event.current.mousePosition);

                if (isCurrent || isHovered)
                    GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                        isCurrent ? LiveBuilderAccentColors.ColorOf(LiveBuilderAccentColor.Tool) : LiveBuilderAccentColors.TintOf(LiveBuilderAccentColor.Tool, 0.25f), 0f, 3f);

                var iconRect = new Rect(rect.center.x - ToolIconSize / 2f, rect.center.y - ToolIconSize / 2f - 1f, ToolIconSize, ToolIconSize);
                LiveBuilderToolIcons.Draw(iconRect, tool, isCurrent ? Color.white : LiveBuilderIconCache.ThemedColor);
                HotkeyStyle.Draw(rect, hotkey, false, false, false, false);
            }

            if (GUI.Button(rect, new GUIContent("", tool.DisplayName + " (" + hotkey + ") — " + tool.Tooltip), GUIStyle.none) && !isCurrent)
                LiveBuilderToolPalette.Current = tool;
        }

        // ⚙ — окно уровня (этажи, типы стен, материалы, проверка); ⏻ — выключить Live Builder.
        private static void DrawPanelButtons()
        {
            using (new EditorGUIUtility.IconSizeScope(new Vector2(LiveBuilderWindowControls.IconSize, LiveBuilderWindowControls.IconSize)))
            {
                if (GUILayout.Button(new GUIContent(LiveBuilderOptionIcons.Themed(LiveBuilderOptionIcon.Level),
                        "Окно уровня: проверка, этажи, типы стен, материалы, файлы мешей"), EditorStyles.miniButtonLeft, GUILayout.Width(24f), GUILayout.Height(20f)))
                    LiveBuilderWindow.Open();

                var key = LiveBuilderShortcutIds.KeyLabelOf(LiveBuilderShortcutIds.ToggleLiveBuilder);

                if (GUILayout.Button(new GUIContent(LiveBuilderOptionIcons.Themed(LiveBuilderOptionIcon.Power), "Выключить Live Builder (" + key + ")"),
                        EditorStyles.miniButtonRight, GUILayout.Width(24f), GUILayout.Height(20f)))
                    LiveBuilderActivation.SetToolActive(false);
            }
        }


        // ── Строка инструмента ───────────────────────────────────────────

        // Про новое — синяя полоса слева; пока рисуешь — притушена, но работает.
        private static void DrawToolLine()
        {
            var tool = LiveBuilderToolPalette.Current;
            var color = GUI.color;

            if (tool.IsBusy)
                GUI.color = new Color(color.r, color.g, color.b, color.a * 0.6f);

            var rect = EditorGUILayout.BeginVertical(LineStyle);

            if (tool.Modes == null)
                GUILayout.Label(tool.DisplayName, ToolNameStyle);

            LiveBuilderToolSettingsFields.DrawToolLine(tool);
            EditorGUILayout.EndVertical();
            GUI.color = color;

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, AccentWidth, rect.height), LiveBuilderAccentColors.ColorOf(LiveBuilderAccentColor.Tool));
        }


        // ── Карточка выделенного или Drawing ─────────────────────────────

        // Отчёт берётся на разметке и держится до следующей: разметка и отрисовка IMGUI должны видеть одни и те же строки.
        private void DrawCardOrDrawing(float width, float maxCardHeight)
        {
            var tool = LiveBuilderToolPalette.Current;

            if (Event.current.type == EventType.Layout)
                drawingReport = tool.IsBusy ? tool.DrawingReport : null;

            if (drawingReport != null)
                LiveBuilderDrawingSection.Draw(drawingReport);
            else
                DrawCard(width, maxCardHeight);
        }


        private void DrawCard(float width, float maxCardHeight)
        {
            var selected = SelectedObjects();

            if (selected.Count == 0)
            {
                using (new EditorGUILayout.VerticalScope(LineStyle))
                    GUILayout.Label("Nothing selected — click a build in the Scene view or the Hierarchy.", EmptySectionStyle);

                return;
            }

            var shown = cards.ShownFor(selected);
            cardScrollPosition = EditorGUILayout.BeginScrollView(cardScrollPosition, GUILayout.Height(Mathf.Min(cardContentHeight, maxCardHeight)));
            var content = EditorGUILayout.BeginVertical(LineStyle);

            LiveBuilderCardHeader.Draw(selected, width - 16f);

            foreach (var card in shown)
                DrawSection(card, selected);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            RememberCardHeight(content.height);
        }

        // Высота содержимого — после разметки: поменялась — следующий проход возьмёт новую.
        private void RememberCardHeight(float height)
        {
            if (Event.current.type != EventType.Repaint || height < 1f || Mathf.Abs(height - cardContentHeight) < 0.5f)
                return;

            cardContentHeight = height;
            RepaintRequested?.Invoke();
        }


        // Пустая секция — одна строка «+ Add», остальные — свёртки по порядку.
        private static void DrawSection(LiveBuilderSelectionCard card, IReadOnlyList<GameObject> selected)
        {
            if (card.IsEmptyFor(selected))
            {
                DrawEmptySection(card, selected);
                return;
            }

            using var section = LiveBuilderWindowControls.FoldoutSection("Panel." + card.Title, card.Title, card.SummaryOf(selected),
                LiveBuilderAccentColor.Selection, card.IsOpenByDefault);

            if (section.IsOpen)
                card.Draw(selected);
        }

        private static void DrawEmptySection(LiveBuilderSelectionCard card, IReadOnlyList<GameObject> selected)
        {
            var rect = GUILayoutUtility.GetRect(0f, SectionRowHeight, GUILayout.ExpandWidth(true));
            var addContent = new GUIContent(selected.Count > 1 ? "+ Add to each" : "+ Add", card.AddTooltip);
            var addWidth = EditorStyles.miniButton.CalcSize(addContent).x + 4f;
            var addRect = new Rect(rect.xMax - addWidth, rect.y + 1f, addWidth, rect.height - 2f);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, LiveBuilderAccentColors.TintOf(LiveBuilderAccentColor.Selection, 0.05f));
                EmptySectionStyle.Draw(new Rect(rect.x + 8f, rect.y, rect.width - addWidth - 8f, rect.height), card.Title + "  ·  none", false, false, false, false);
            }

            if (GUI.Button(addRect, addContent, EditorStyles.miniButton))
                card.Add(selected, addRect);
        }


        // Выделенное в сцене; активный — первым: по нему заголовок и путь.
        private static List<GameObject> SelectedObjects()
        {
            var selected = new List<GameObject>();
            var active = Selection.activeGameObject;

            if (active != null && active.scene.IsValid())
                selected.Add(active);

            foreach (var each in Selection.gameObjects)
                if (each != active && each.scene.IsValid())
                    selected.Add(each);

            return selected;
        }
    }
}
