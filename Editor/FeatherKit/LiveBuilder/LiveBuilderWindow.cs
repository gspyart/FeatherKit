using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Окно Live Builder — пульт инструмента сборки уровня, одной колонкой сверху вниз, по тому, от чего настройка зависит:
    /// - шапка — включён ли Live Builder, «i» — все пояснения сразу, клавиатура — клавиши
    /// - палитра — инструменты плитками в два ряда, выбранная залита синим; цифра на плитке — клавиша в окне сцены
    /// - цветные зоны (<see cref="LiveBuilderWindowZones"/>):
    ///   серая «Общее» — действует всегда: этаж, сетка, вид (<see cref="LiveBuilderGlobalSettingsPanel"/>);
    ///   синяя — настройки выбранного инструмента, только те, что на него действуют (<see cref="LiveBuilderToolSettingsPanel"/>);
    ///   оранжевая «Выделено» — числа и действия выделенного; зоны нет, пока не выделено своё (<see cref="LiveBuilderSelectionPanel"/>);
    ///   фиолетовая «Уровень» — этажи уровня, типы стен, уборка мешей; меняют редко, свёрнута (<see cref="LiveBuilderLevelPanel"/>)
    /// - внизу — подсказка, что делать сейчас, та же, что в окне сцены, со значком инструмента
    /// Окно само ничего не переключает: зоны сворачиваешь ты, они это помнят. Рисование мышью — в окне сцены (<see cref="BoxModelSceneTool"/>).
    /// </summary>
    public class LiveBuilderWindow : EditorWindow
    {
        private const float TileHeight = 46f;
        private const float TileGap = 4f;

        // Промежуток между группами палитры: «Выделение» | «Строить» | «Детали и мера».
        private const float GroupGap = 10f;

        private const float HintIconSize = 14f;

        // Отступы сегментов. Создаются при первом рисовании: при загрузке окна Unity их делать не даёт.
        private static GUIStyle topPadding;
        private static GUIStyle hintPadding;
        private static GUIStyle hintTextStyle;

        private readonly LiveBuilderSelectionPanel selectionPanel = new();
        private Vector2 scrollPosition;
        private bool isShowingHotkeys;


        private static GUIStyle TopPadding => topPadding ??= new GUIStyle { padding = new RectOffset(8, 8, 6, 0) };

        private static GUIStyle HintPadding => hintPadding ??= new GUIStyle { padding = new RectOffset(8, 8, 4, 5) };

        private static GUIStyle HintTextStyle => hintTextStyle ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel);


        [MenuItem("FeatherKit/Live Builder", false, 0)]
        public static void Open()
        {
            var window = GetWindow<LiveBuilderWindow>();
            window.titleContent = new GUIContent("Live Builder", LiveBuilderToolIcons.Themed(LiveBuilderTool.Box));
            window.minSize = new Vector2(280f, 420f);
            window.Show();
        }


        // ── Окно ─────────────────────────────────────────────────────────

        private void OnGUI()
        {
            // Подсветка плитки и заголовков под мышью.
            if (Event.current.type == EventType.MouseMove)
                Repaint();

            // Подписи полей — не шире половины окна: в узком окне поля не уезжают за край.
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.42f, 110f, 160f);

            // Подсказка прибита к низу, шапка, палитра и «Общее» — сверху; прокручиваются зоны инструмента, выделенного и уровня.
            var hint = HintText();
            var hintHeight = HintHeight(hint);
            GUILayout.BeginArea(new Rect(0f, 0f, position.width, position.height - hintHeight));

            using (new EditorGUILayout.VerticalScope(TopPadding))
            {
                DrawHeader();

                if (isShowingHotkeys)
                    DrawHotkeys();

                DrawPalette();
            }

            using (var zone = LiveBuilderWindowZones.Begin(LiveBuilderWindowZone.Global, "Общее", "этаж, сетка, вид",
                       LiveBuilderOptionIcons.White(LiveBuilderOptionIcon.Global), "Global"))
                if (zone.IsExpanded)
                    LiveBuilderGlobalSettingsPanel.Draw();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);
            DrawZones();
            EditorGUILayout.Space(6f);
            EditorGUILayout.EndScrollView();

            GUILayout.EndArea();
            DrawHint(new Rect(0f, position.height - hintHeight, position.width, hintHeight), hint);
        }


        // ── Шапка ────────────────────────────────────────────────────────

        // Включён ли Live Builder — зелёной плашкой со значком питания; справа — «i» (все пояснения) и клавиши.
        private void DrawHeader()
        {
            using (new EditorGUIUtility.IconSizeScope(new Vector2(LiveBuilderWindowControls.IconSize, LiveBuilderWindowControls.IconSize)))
            using (new EditorGUILayout.HorizontalScope())
            {
                var isActive = BoxModelActions.IsToolActive;
                var previous = GUI.backgroundColor;
                GUI.backgroundColor = isActive ? new Color(0.45f, 0.85f, 0.5f) : previous;
                var label = isActive ? " Live Builder включён · B" : " Включить Live Builder · B";
                var power = new GUIContent(label, LiveBuilderOptionIcons.Themed(LiveBuilderOptionIcon.Power), "Пока включён, мышь в окне сцены строит выбранным инструментом");

                if (GUILayout.Toggle(isActive, power, "Button", GUILayout.Height(24f)) != isActive)
                    BoxModelActions.SetToolActive(!isActive);

                GUI.backgroundColor = previous;

                var isShowingAll = LiveBuilderWindowControls.IsShowingAllExplanations;
                var info = new GUIContent(LiveBuilderOptionIcons.Themed(LiveBuilderOptionIcon.Info), "Все пояснения «Как пользоваться» — сразу открыты");

                if (GUILayout.Toggle(isShowingAll, info, "Button", GUILayout.Width(28f), GUILayout.Height(24f)) != isShowingAll)
                    LiveBuilderWindowControls.IsShowingAllExplanations = !isShowingAll;

                var keys = new GUIContent(LiveBuilderOptionIcons.Themed(LiveBuilderOptionIcon.Keyboard), "Клавиши в окне сцены");
                isShowingHotkeys = GUILayout.Toggle(isShowingHotkeys, keys, "Button", GUILayout.Width(28f), GUILayout.Height(24f));
            }
        }


        private static void DrawHotkeys()
        {
            using (LiveBuilderWindowControls.Card("Клавиши в окне сцены"))
            {
                DrawHotkey("B", "включить Live Builder");
                DrawHotkey("1 – " + LiveBuilderToolSetting.PaletteOrder.Length, "инструмент, пока ничего не рисуешь");
                DrawHotkey("PageUp / PageDown", "этаж выше / ниже");
                DrawHotkey("[  ]", "сетка мельче / крупнее");
                DrawHotkey("Shift + клик", "рисуешь линию — точка наоборот: угол или плавно");
                DrawHotkey("цифры, Enter", "точная длина отрезка, пока рисуешь линию");
                DrawHotkey("Tab", "повернуть лестницу");
                DrawHotkey("Enter / Esc", "готово / отмена");
                DrawHotkey("Esc", "ничего не начато — на «Выделение», в нём — снять выделение");
                LiveBuilderWindowControls.Note("Точки выделенного контура:");
                DrawHotkey("Shift + клик, рамка", "выбрать несколько точек");
                DrawHotkey("двойной клик", "угол или плавно");
                DrawHotkey("стрелки", "сдвинуть выбранные на шаг сетки");
                DrawHotkey("Delete", "убрать выбранные точки");
            }
        }

        private static void DrawHotkey(string keys, string action)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(keys, EditorStyles.miniBoldLabel, GUILayout.Width(110f));
                GUILayout.Label(action, EditorStyles.miniLabel);
            }
        }


        // ── Палитра ──────────────────────────────────────────────────────

        // Два ряда по четыре плитки, группы — промежутком: «Выделение» | «Коробка», «Комната», «Стена» — и ниже «Здание» | «Лестница», «Проём», «Линейка».
        // Промежуток в обоих рядах на одном месте: «Здание» замыкает группу «Строить», начатую в верхнем ряду.
        private static void DrawPalette()
        {
            var tools = LiveBuilderToolSetting.PaletteOrder;
            var columns = (tools.Length + 1) / 2;
            EditorGUILayout.Space(4f);

            for (var start = 0; start < tools.Length; start += columns)
                DrawToolRow(tools, start, Mathf.Min(columns, tools.Length - start), columns);

            EditorGUILayout.Space(2f);
        }

        private static void DrawToolRow(LiveBuilderTool[] tools, int start, int count, int columns)
        {
            var row = EditorGUILayout.GetControlRect(false, TileHeight);
            var tileWidth = (row.width - GroupGap - TileGap * (columns - 2)) / columns;

            for (var index = 0; index < count; index++)
            {
                var tool = tools[start + index];
                var x = row.x + index * (tileWidth + TileGap) + (index > 0 ? GroupGap - TileGap : 0f);
                var tile = new Rect(x, row.y, tileWidth, row.height);

                if (!LiveBuilderWindowControls.ToolTile(tile, tool, tool == LiveBuilderToolSetting.Current))
                    continue;

                LiveBuilderToolSetting.Current = tool;
                BoxModelActions.SetToolActive(true);
            }

            EditorGUILayout.Space(2f);
        }


        // ── Зоны ─────────────────────────────────────────────────────────

        // Инструмент — всегда; выделенное — только когда выделено своё; уровень — в конце.
        private void DrawZones()
        {
            var tool = LiveBuilderToolSetting.Current;

            using (var zone = LiveBuilderWindowZones.Begin(LiveBuilderWindowZone.Tool, LiveBuilderToolSetting.DisplayName(tool), "инструмент",
                       LiveBuilderToolIcons.White(tool), "Tool"))
                if (zone.IsExpanded)
                    LiveBuilderToolSettingsPanel.Draw();

            if (LiveBuilderSelectionPanel.HasEditableSelection())
                using (var zone = LiveBuilderWindowZones.Begin(LiveBuilderWindowZone.Selection, "Выделено", SelectionDetail(),
                           LiveBuilderToolIcons.White(LiveBuilderTool.Select), "Selected"))
                    if (zone.IsExpanded)
                        selectionPanel.Draw();

            using (var zone = LiveBuilderWindowZones.Begin(LiveBuilderWindowZone.Level, "Уровень", "этажи, типы стен, меши",
                       LiveBuilderOptionIcons.White(LiveBuilderOptionIcon.Level), "Level", false))
                if (zone.IsExpanded)
                    LiveBuilderLevelPanel.Draw();
        }

        // «Box Model» или «Box Model и ещё 2».
        private static string SelectionDetail()
        {
            var count = Selection.gameObjects.Length;
            var name = Selection.activeGameObject.name;
            return count > 1 ? name + " и ещё " + (count - 1) : name;
        }


        // ── Подсказка ────────────────────────────────────────────────────

        // Внизу окна — что делать сейчас, как внизу окна сцены. Сцена ещё не перерисовалась — что делает инструмент.
        private static string HintText()
        {
            if (!BoxModelActions.IsToolActive)
                return "Live Builder выключен — B в окне сцены или кнопка вверху";

            var text = BoxModelSceneTool.CurrentHintText;
            return string.IsNullOrEmpty(text) ? LiveBuilderToolSetting.Tooltip(LiveBuilderToolSetting.Current) : text;
        }

        private float HintHeight(string hint)
        {
            var textWidth = position.width - HintPadding.padding.horizontal - HintIconSize - 6f;
            return Mathf.Ceil(HintTextStyle.CalcHeight(new GUIContent(hint), textWidth)) + HintPadding.padding.vertical;
        }

        // Полоса цвета инструмента: значок инструмента и текст. Live Builder выключен — серая, без значка.
        private static void DrawHint(Rect rect, string text)
        {
            var isActive = BoxModelActions.IsToolActive;
            var inner = HintPadding.padding.Remove(rect);

            if (Event.current.type == EventType.Repaint)
            {
                if (isActive)
                {
                    EditorGUI.DrawRect(rect, LiveBuilderWindowZones.TintOf(LiveBuilderWindowZone.Tool, 0.14f));
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), LiveBuilderWindowZones.ColorOf(LiveBuilderWindowZone.Tool));
                    LiveBuilderToolIcons.Draw(new Rect(inner.x, inner.y + 1f, HintIconSize, HintIconSize), LiveBuilderToolSetting.Current, LiveBuilderWindowControls.IconColor);
                }
                else
                    LiveBuilderWindowControls.DrawLine(new Rect(rect.x, rect.y, rect.width, 1f));
            }

            var textRect = new Rect(inner.x + HintIconSize + 6f, inner.y, inner.width - HintIconSize - 6f, inner.height);
            GUI.Label(textRect, text, HintTextStyle);
        }


        // ── Unity-события ────────────────────────────────────────────────

        private void OnEnable()
        {
            wantsMouseMove = true;
            Selection.selectionChanged += Repaint;
            LiveBuilderStoreys.Changed += Repaint;
            LiveBuilderToolSetting.Changed += Repaint;
            LiveBuilderRoomSettings.ShapeChanged += Repaint;
            BoxModelSceneTool.HintChanged += Repaint;
            LiveBuilderContourSelection.Changed += Repaint;
            ToolManager.activeToolChanged += Repaint;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
            LiveBuilderStoreys.Changed -= Repaint;
            LiveBuilderToolSetting.Changed -= Repaint;
            LiveBuilderRoomSettings.ShapeChanged -= Repaint;
            BoxModelSceneTool.HintChanged -= Repaint;
            LiveBuilderContourSelection.Changed -= Repaint;
            ToolManager.activeToolChanged -= Repaint;
            selectionPanel.Dispose();
        }
    }
}
