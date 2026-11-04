using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Окно Live Builder — пульт инструмента сборки уровня, одной колонкой сверху вниз, по тому, от чего настройка зависит:
    /// - шапка — включён ли Live Builder, справка по клавишам; палитра — инструменты плитками, цифра на плитке — клавиша в окне сцены
    /// - «Общее» — действует всегда: этаж, сетка, вид (<see cref="LiveBuilderGlobalSettingsPanel"/>)
    /// - «Инструмент» — настройки выбранного инструмента, только те, что на него действуют (<see cref="LiveBuilderToolSettingsPanel"/>)
    /// - «Выделено» — числа и действия выделенного; секции нет, пока не выделено своё (<see cref="LiveBuilderSelectionPanel"/>)
    /// - «Уровень» — этажи уровня, типы стен, уборка мешей; меняют редко, свёрнута (<see cref="LiveBuilderLevelPanel"/>)
    /// - внизу — подсказка, что делать сейчас, та же, что в окне сцены
    /// Окно само ничего не переключает: секции сворачиваешь ты, они это помнят. Рисование мышью — в окне сцены (<see cref="BoxModelSceneTool"/>).
    /// </summary>
    public class LiveBuilderWindow : EditorWindow
    {
        private const float TileHeight = 46f;
        private const float TileGap = 4f;

        // Отступы сегментов. Создаются при первом рисовании: при загрузке окна Unity их делать не даёт.
        private static GUIStyle topPadding;
        private static GUIStyle contentPadding;
        private static GUIStyle hintPadding;

        private readonly LiveBuilderSelectionPanel selectionPanel = new();
        private Vector2 scrollPosition;
        private bool isShowingHotkeys;


        private static GUIStyle TopPadding => topPadding ??= new GUIStyle { padding = new RectOffset(8, 8, 6, 0) };

        private static GUIStyle ContentPadding => contentPadding ??= new GUIStyle { padding = new RectOffset(8, 8, 2, 8) };

        private static GUIStyle HintPadding => hintPadding ??= new GUIStyle { padding = new RectOffset(8, 8, 2, 4) };


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
            // Подсветка плитки под мышью.
            if (Event.current.type == EventType.MouseMove)
                Repaint();

            // Подписи полей — не шире половины окна: в узком окне поля не уезжают за край.
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.42f, 110f, 160f);

            // Подсказка прибита к низу, шапка, палитра и «Общее» — сверху; прокручиваются секции инструмента, выделенного и уровня.
            var hint = HintText();
            var hintHeight = Mathf.Ceil(EditorStyles.wordWrappedMiniLabel.CalcHeight(new GUIContent(hint), position.width - 16f)) + 8f;
            GUILayout.BeginArea(new Rect(0f, 0f, position.width, position.height - hintHeight));

            using (new EditorGUILayout.VerticalScope(TopPadding))
            {
                DrawHeader();

                if (isShowingHotkeys)
                    DrawHotkeys();

                DrawPalette();

                if (LiveBuilderWindowControls.Section("Общее", "Global"))
                    LiveBuilderGlobalSettingsPanel.Draw();
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);

            using (new EditorGUILayout.VerticalScope(ContentPadding))
                DrawSections();

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            DrawHint(new Rect(0f, position.height - hintHeight, position.width, hintHeight), hint);
        }


        // ── Шапка ────────────────────────────────────────────────────────

        // Включён ли Live Builder — плашкой с цветом; справа — справка по клавишам.
        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var isActive = BoxModelActions.IsToolActive;
                var previous = GUI.backgroundColor;
                GUI.backgroundColor = isActive ? new Color(0.45f, 0.85f, 0.5f) : previous;
                var label = isActive ? "Live Builder включён · B" : "Включить Live Builder · B";

                if (GUILayout.Toggle(isActive, new GUIContent(label, "Пока включён, мышь в окне сцены строит выбранным инструментом"), "Button", GUILayout.Height(24f)) != isActive)
                    BoxModelActions.SetToolActive(!isActive);

                GUI.backgroundColor = previous;
                isShowingHotkeys = GUILayout.Toggle(isShowingHotkeys, new GUIContent("?", "Клавиши"), "Button", GUILayout.Width(26f), GUILayout.Height(24f));
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

        // «Выделение» — широкой плиткой сверху, под ним два ряда по четыре: чем строят и детали с мерой.
        private static void DrawPalette()
        {
            var tools = LiveBuilderToolSetting.PaletteOrder;
            var selectCount = LiveBuilderToolSetting.SelectToolCount;
            var buildCount = LiveBuilderToolSetting.BuildToolCount;
            DrawToolRow("Выделить и править", tools, 0, selectCount, selectCount);
            DrawToolRow("Строить", tools, selectCount, buildCount, buildCount);
            DrawToolRow("Детали и мера", tools, selectCount + buildCount, tools.Length - selectCount - buildCount, buildCount);
        }

        private static void DrawToolRow(string caption, LiveBuilderTool[] tools, int start, int count, int columns)
        {
            GUILayout.Label(caption, EditorStyles.centeredGreyMiniLabel, GUILayout.ExpandWidth(false));
            var row = EditorGUILayout.GetControlRect(false, TileHeight);
            var tileWidth = (row.width - TileGap * (columns - 1)) / columns;

            for (var index = 0; index < count; index++)
            {
                var tool = tools[start + index];
                var tile = new Rect(row.x + index * (tileWidth + TileGap), row.y, tileWidth, row.height);

                if (!LiveBuilderWindowControls.ToolTile(tile, tool, tool == LiveBuilderToolSetting.Current))
                    continue;

                LiveBuilderToolSetting.Current = tool;
                BoxModelActions.SetToolActive(true);
            }

            EditorGUILayout.Space(2f);
        }


        // ── Секции ───────────────────────────────────────────────────────

        // Инструмент — всегда; выделенное — только когда выделено своё; уровень — в конце.
        private void DrawSections()
        {
            var tool = LiveBuilderToolSetting.Current;

            if (LiveBuilderWindowControls.Section("Инструмент: " + LiveBuilderToolSetting.DisplayName(tool), "Tool"))
                LiveBuilderToolSettingsPanel.Draw();

            if (LiveBuilderSelectionPanel.HasEditableSelection() && LiveBuilderWindowControls.Section(SelectionTitle(), "Selected"))
                selectionPanel.Draw();

            if (LiveBuilderWindowControls.Section("Уровень: этажи, типы стен, меши", "Level", false))
                LiveBuilderLevelPanel.Draw();
        }

        // «Выделено: Box Model» или «Выделено: Box Model и ещё 2».
        private static string SelectionTitle()
        {
            var count = Selection.gameObjects.Length;
            var title = "Выделено: " + Selection.activeGameObject.name;
            return count > 1 ? title + " и ещё " + (count - 1) : title;
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

        private static void DrawHint(Rect rect, string text)
        {
            if (Event.current.type == EventType.Repaint)
                LiveBuilderWindowControls.DrawLine(new Rect(rect.x, rect.y, rect.width, 1f));

            GUI.Label(HintPadding.padding.Remove(rect), text, EditorStyles.wordWrappedMiniLabel);
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
