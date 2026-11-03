using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Окно Live Builder — пульт инструмента сборки уровня, сегментами сверху вниз:
    /// - шапка — включён ли Live Builder, справка по клавишам
    /// - палитра — инструменты плитками со значками, «Строить» и «Детали и мера»; цифра на плитке — клавиша в окне сцены
    /// - этаж — на каком этаже строишь, сетка, что видно в окне сцены, «в здании — этот или все выше»
    /// - вкладки: «Новое» — настройки того, что нарисуешь (<see cref="LiveBuilderToolSettingsPanel"/>), «Выделено» — числа выделенного
    ///   (<see cref="LiveBuilderSelectionPanel"/>), «Уровень» — этажи уровня и типы стен (<see cref="LiveBuilderLevelPanel"/>)
    /// - внизу — подсказка, что делать сейчас, та же, что в окне сцены
    /// Вкладка переключается сама: выделил объект — «Выделено», выбрал инструмент — «Новое». Рисование мышью — в окне сцены (<see cref="BoxModelSceneTool"/>).
    /// </summary>
    public class LiveBuilderWindow : EditorWindow
    {
        private const float TileHeight = 46f;
        private const float TileGap = 4f;

        private const int NewTab = 0;
        private const int SelectedTab = 1;
        private const int LevelTab = 2;

        // Отступы сегментов. Создаются при первом рисовании: при загрузке окна Unity их делать не даёт.
        private static GUIStyle topPadding;
        private static GUIStyle contentPadding;
        private static GUIStyle hintPadding;

        private readonly LiveBuilderSelectionPanel selectionPanel = new();
        private Vector2 scrollPosition;
        private int currentTab = NewTab;
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

            // Подсказка прибита к низу, всё остальное — над ней; прокручивается только содержимое вкладки.
            var hint = HintText();
            var hintHeight = Mathf.Ceil(EditorStyles.wordWrappedMiniLabel.CalcHeight(new GUIContent(hint), position.width - 16f)) + 8f;
            GUILayout.BeginArea(new Rect(0f, 0f, position.width, position.height - hintHeight));

            using (new EditorGUILayout.VerticalScope(TopPadding))
            {
                DrawHeader();

                if (isShowingHotkeys)
                    DrawHotkeys();

                DrawPalette();
                LiveBuilderWindowControls.Separator();
                DrawStoreyBar();
                EditorGUILayout.Space(4f);
                DrawTabs();
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);

            using (new EditorGUILayout.VerticalScope(ContentPadding))
                DrawTabContent();

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


        // ── Этаж ─────────────────────────────────────────────────────────

        // Этаж и сетка — одной строкой; что видно и как строить в здании — переключателями.
        private static void DrawStoreyBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("Этаж", "На каком этаже строишь — и в пустоте, и в зданиях: их этажи те же. PageUp и PageDown в окне сцены"));

                if (GUILayout.Button("◀", EditorStyles.miniButtonLeft, GUILayout.Width(22f)))
                    LiveBuilderStoreys.CurrentStorey--;

                var storey = EditorGUILayout.IntField(LiveBuilderStoreys.CurrentStorey, EditorStyles.miniTextField, GUILayout.Width(30f));

                if (storey != LiveBuilderStoreys.CurrentStorey)
                    LiveBuilderStoreys.CurrentStorey = storey;

                if (GUILayout.Button("▶", EditorStyles.miniButtonRight, GUILayout.Width(22f)))
                    LiveBuilderStoreys.CurrentStorey++;

                GUILayout.Label("пол " + BoxModelSceneText.FormatMeters(LiveBuilderStoreys.CurrentFloorHeight) + " м", EditorStyles.miniLabel);
            }

            DrawGrid();
            DrawVisibility();

            LiveBuilderBuildingContext.IsOnAllStoreys = LiveBuilderWindowControls.Switch(
                new GUIContent("В здании", "Наведёшь на здание — стена, комната, лестница и проём встанут на этот этаж здания, до его потолка, с его толщинами. " +
                                           "И все выше — то же на каждом этаже выше: перегородки, лестничная клетка, окна по фасаду. После перезапуска Unity — снова «только этот»"),
                LiveBuilderBuildingContext.IsOnAllStoreys, "Только этот", "И все выше");
        }


        private static void DrawGrid()
        {
            var steps = BoxModelGrid.Steps;
            var names = new string[steps.Length];
            var current = 0;

            for (var index = 0; index < steps.Length; index++)
            {
                names[index] = BoxModelSceneText.FormatMeters(steps[index]);

                if (Mathf.Abs(steps[index] - BoxModelGrid.CurrentStep) < 0.0001f)
                    current = index;
            }

            var chosen = LiveBuilderWindowControls.Switch(new GUIContent("Сетка, м", "Всё прилипает к клеткам этого размера. Клавиши [ и ] в окне сцены"), current, names);

            if (chosen != current)
                BoxModelGrid.SetStep(steps[chosen]);
        }


        // Что видно в окне сцены: всё, этаж срезом на 3 м (комнаты) или на 1 м (стены вниз). Игру не меняет.
        private static void DrawVisibility()
        {
            var isSectioned = LiveBuilderStoreys.IsSectionEnabled;
            var height = LiveBuilderStoreys.SectionHeightAboveFloor;
            var current = !isSectioned ? 0 : Mathf.Approximately(height, 1f) ? 2 : 1;

            var chosen = LiveBuilderWindowControls.Switch(
                new GUIContent("Показать", "Всё — как есть. Комнаты — выше 3 м над полом этажа спрятано, видно стены и проёмы. " +
                                           "Стены вниз — выше 1 м: видно комнаты с любого угла. Только в окне сцены"),
                current, "Всё", "Комнаты", "Стены вниз");

            if (chosen == current)
                return;

            if (chosen > 0)
                LiveBuilderStoreys.SectionHeightAboveFloor = chosen == 1 ? 3f : 1f;

            LiveBuilderStoreys.IsSectionEnabled = chosen > 0;
        }


        // ── Вкладки ──────────────────────────────────────────────────────

        private void DrawTabs()
        {
            var selected = Selection.activeGameObject;
            var selectedTitle = LiveBuilderSelectionPanel.HasEditableSelection() && selected != null ? "Выделено: " + selected.name : "Выделено";

            currentTab = LiveBuilderWindowControls.Tabs(currentTab,
                new GUIContent("Новое: " + LiveBuilderToolSetting.DisplayName(LiveBuilderToolSetting.Current), "Настройки того, что нарисуешь дальше"),
                new GUIContent(selectedTitle, "Числа выделенного — меняются сразу"),
                new GUIContent("Уровень", "Этажи уровня и типы стен — общее на весь уровень"));
        }


        private void DrawTabContent()
        {
            EditorGUILayout.Space(4f);

            switch (currentTab)
            {
                case SelectedTab:
                    selectionPanel.Draw();
                    break;

                case LevelTab:
                    LiveBuilderLevelPanel.Draw();
                    break;

                default:
                    LiveBuilderToolSettingsPanel.Draw();
                    break;
            }
        }


        // Выделил объект — его числа; снял выделение — настройки нового. «Уровень» не трогаем: туда заходят нарочно.
        private void ShowSelectionProperties()
        {
            if (currentTab != LevelTab)
                currentTab = LiveBuilderSelectionPanel.HasEditableSelection() ? SelectedTab : NewTab;

            Repaint();
        }


        // Выбрал инструмент — значит, сейчас будешь рисовать: настройки нового.
        private void ShowNewSettings()
        {
            currentTab = NewTab;
            Repaint();
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
            Selection.selectionChanged += ShowSelectionProperties;
            LiveBuilderStoreys.Changed += Repaint;
            LiveBuilderToolSetting.Changed += ShowNewSettings;
            LiveBuilderRoomSettings.ShapeChanged += Repaint;
            BoxModelSceneTool.HintChanged += Repaint;
            LiveBuilderContourSelection.Changed += Repaint;
            ToolManager.activeToolChanged += Repaint;
            ShowSelectionProperties();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= ShowSelectionProperties;
            LiveBuilderStoreys.Changed -= Repaint;
            LiveBuilderToolSetting.Changed -= ShowNewSettings;
            LiveBuilderRoomSettings.ShapeChanged -= Repaint;
            BoxModelSceneTool.HintChanged -= Repaint;
            LiveBuilderContourSelection.Changed -= Repaint;
            ToolManager.activeToolChanged -= Repaint;
            selectionPanel.Dispose();
        }
    }
}
