using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Окно Live Builder — пульт инструмента сборки уровня, как боковая панель Blender с вкладками. Сверху вниз:
    /// - шапка — включён ли Live Builder, клавиатура — клавиши окна сцены
    /// - палитра — инструменты плитками в два ряда (<see cref="LiveBuilderToolPalette"/>), выбранная залита синим; цифра на плитке — клавиша в окне сцены
    /// - строка о выделенном — что выделено и какого размера, клик — вкладка «Выделено» (<see cref="LiveBuilderSelectionStrip"/>)
    /// - строка общего — этаж, сетка, вид, каркасы: действует всегда (<see cref="LiveBuilderCommonSettingsBar"/>)
    /// - вкладки (<see cref="LiveBuilderWindowTab"/>, вид — <see cref="LiveBuilderTabBar"/>): синяя «Инструмент», оранжевая «Выделено», фиолетовая «Уровень»;
    ///   окно находит их само и ставит по порядку. «?» справа — пояснения открытой вкладки. Открытая вкладка и «?» у каждой помнятся
    /// - под вкладками — содержимое открытой, прокручивается; внизу — подсказка, что делать сейчас, та же, что в окне сцены, со значком инструмента
    /// Вкладки переключаются и сами, если это не выключено на вкладке «Уровень»: выделил постройку — «Выделено», выбрал инструмент — «Инструмент».
    /// Рисование мышью — в окне сцены (<see cref="LiveBuilderSceneTool"/>).
    /// </summary>
    public class LiveBuilderWindow : EditorWindow
    {
        private const float TileHeight = 46f;
        private const float TileGap = 4f;

        // Промежуток между группами палитры: «Выделение» | «Строить» | «Детали и мера».
        private const float GroupGap = 10f;

        private const float HintIconSize = 14f;

        private const string SelectedTabPreferenceKey = "FeatherKit.LiveBuilder.Tab";
        private const string HelpPreferencePrefix = "FeatherKit.LiveBuilder.Help.";
        private const string AutomaticTabsPreferenceKey = "FeatherKit.LiveBuilder.SwitchTabsAutomatically";

        // Отступы сегментов. Создаются при первом рисовании: при загрузке окна Unity их делать не даёт.
        private static GUIStyle topPadding;
        private static GUIStyle pagePadding;
        private static GUIStyle hintPadding;
        private static GUIStyle hintTextStyle;

        // Вкладки по порядку: создаются, когда окно открывается, и отпускаются, когда закрывается.
        private readonly List<LiveBuilderWindowTab> tabs = new();

        // Вкладка, которую открыть самой, — открывается в начале следующего прохода окна, а не посреди рисования.
        private LiveBuilderWindowTab pendingTab;
        private Vector2 scrollPosition;
        private bool isShowingHotkeys;


        // Переключать ли вкладки самим: выделил постройку — «Выделено», выбрал инструмент — «Инструмент». У каждого человека своё.
        public static bool IsSwitchingTabsAutomatically
        {
            get => EditorPrefs.GetBool(AutomaticTabsPreferenceKey, true);
            set => EditorPrefs.SetBool(AutomaticTabsPreferenceKey, value);
        }


        private static GUIStyle TopPadding => topPadding ??= new GUIStyle { padding = new RectOffset(8, 8, 6, 0) };

        private static GUIStyle PagePadding => pagePadding ??= new GUIStyle { padding = new RectOffset(8, 8, 6, 6) };

        private static GUIStyle HintPadding => hintPadding ??= new GUIStyle { padding = new RectOffset(8, 8, 4, 5) };

        private static GUIStyle HintTextStyle => hintTextStyle ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel);

        // Открытая вкладка: помнится по её ключу; такой нет или её сейчас не открыть — первая доступная.
        // Запомненная не забывается: «Выделено», закрытая из-за пустого выделения, вернётся, как только что-то выделишь.
        private LiveBuilderWindowTab SelectedTab
        {
            get
            {
                var key = EditorPrefs.GetString(SelectedTabPreferenceKey, "");
                return tabs.Find(tab => tab.PreferenceKey == key && tab.IsAvailable) ?? tabs.Find(tab => tab.IsAvailable) ?? tabs[0];
            }
            set => EditorPrefs.SetString(SelectedTabPreferenceKey, value.PreferenceKey);
        }


        [MenuItem("FeatherKit/Live Builder", false, 0)]
        public static void Open()
        {
            var window = GetWindow<LiveBuilderWindow>();
            window.titleContent = new GUIContent("Live Builder", LiveBuilderToolIcons.Themed(LiveBuilderToolPalette.Get<LiveBuilderBoxTool>()));
            window.minSize = new Vector2(280f, 420f);
            window.Show();
        }


        // ── Окно ─────────────────────────────────────────────────────────

        private void OnGUI()
        {
            // Подсветка плитки, корешка и «Ещё» под мышью.
            if (Event.current.type == EventType.MouseMove)
                Repaint();

            ApplyPendingTab();

            // Подписи полей — не шире половины окна: в узком окне поля не уезжают за край.
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.42f, 110f, 160f);

            // Подсказка прибита к низу; шапка, палитра, строка общего и корешки — сверху; прокручивается только открытая вкладка.
            var hint = HintText();
            var hintHeight = HintHeight(hint);
            var areaHeight = position.height - hintHeight;
            GUILayout.BeginArea(new Rect(0f, 0f, position.width, areaHeight));

            Rect tabRow;

            using (new EditorGUILayout.VerticalScope(TopPadding))
            {
                DrawHeader();

                if (isShowingHotkeys)
                    DrawHotkeys();

                DrawPalette();
                DrawSelectionStrip();
                LiveBuilderCommonSettingsBar.Draw();
                EditorGUILayout.Space(6f);
                tabRow = DrawTabBar();
            }

            var selectedTab = SelectedTab;
            LiveBuilderTabBar.DrawPageBackground(new Rect(0f, tabRow.yMax, position.width, areaHeight - tabRow.yMax), selectedTab.TabColor);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);

            using (new EditorGUILayout.VerticalScope(PagePadding))
                DrawTab(selectedTab);

            EditorGUILayout.EndScrollView();

            GUILayout.EndArea();
            DrawHint(new Rect(0f, areaHeight, position.width, hintHeight), hint);
        }


        // ── Шапка ────────────────────────────────────────────────────────

        // Включён ли Live Builder — зелёной плашкой со значком питания; справа — клавиши окна сцены.
        private void DrawHeader()
        {
            using (new EditorGUIUtility.IconSizeScope(new Vector2(LiveBuilderWindowControls.IconSize, LiveBuilderWindowControls.IconSize)))
            using (new EditorGUILayout.HorizontalScope())
            {
                var isActive = LiveBuilderActivation.IsToolActive;
                var previous = GUI.backgroundColor;
                GUI.backgroundColor = isActive ? new Color(0.45f, 0.85f, 0.5f) : previous;
                var label = isActive ? " Live Builder включён · B" : " Включить Live Builder · B";
                var power = new GUIContent(label, LiveBuilderOptionIcons.Themed(LiveBuilderOptionIcon.Power), "Пока включён, мышь в окне сцены строит выбранным инструментом");

                if (GUILayout.Toggle(isActive, power, "Button", GUILayout.Height(24f)) != isActive)
                    LiveBuilderActivation.SetToolActive(!isActive);

                GUI.backgroundColor = previous;

                var keys = new GUIContent(LiveBuilderOptionIcons.Themed(LiveBuilderOptionIcon.Keyboard), "Клавиши в окне сцены");

                // Клавиши появятся или пропадут — этот проход окна прерываем, иначе IMGUI путается в разметке.
                if (GUILayout.Toggle(isShowingHotkeys, keys, "Button", GUILayout.Width(28f), GUILayout.Height(24f)) != isShowingHotkeys)
                {
                    isShowingHotkeys = !isShowingHotkeys;
                    GUIUtility.ExitGUI();
                }
            }
        }


        private static void DrawHotkeys()
        {
            using (LiveBuilderWindowControls.Card("Клавиши в окне сцены"))
            {
                DrawHotkey("B", "включить Live Builder");
                DrawHotkey("1 – " + LiveBuilderToolPalette.Tools.Count, "инструмент, пока ничего не рисуешь");
                DrawHotkey("PageUp / PageDown", "этаж выше / ниже");
                DrawHotkey("[  ]", "сетка мельче / крупнее");
                DrawHotkey("S", "прилипание к углам, серединам и рёбрам построек: выключить / включить");
                DrawHotkey("Esc", "ничего не начато — на «Выделение», в нём — снять выделение");
                DrawHotkey("Shift + H", "изолировать выделенное вместе с его объектом");
                DrawHotkey("/", "шпаргалка клавиш в углу окна сцены: спрятать или показать");

                LiveBuilderWindowControls.Note("Пока рисуешь:");
                DrawHotkey("клик", "готово: коробка на выбранной глубине, лестница на высоте");
                DrawHotkey("Enter", "готово: лестница; линия — замкнуть контур или закончить стену");
                DrawHotkey("Esc", "бросить начатое целиком: коробку, лестницу, проём, стену, здание, комнату; оставить — Enter или клик");
                DrawHotkey("Shift + клик", "линия — точка наоборот: угол или плавно");
                DrawHotkey("цифры, Enter", "точно: длина отрезка линии, глубина коробки, высота лестницы");
                DrawHotkey("4 X 3, Enter", "коробка на шаге глубины — стороны прямоугольника; «4 X 3 X 2» — и глубина");
                DrawHotkey("Ctrl + Z", "линия — убрать последнюю точку");
                DrawHotkey("Tab", "лестница — повернуть на четверть");

                LiveBuilderWindowControls.Note("Выделенное:");
                DrawHotkey("тянуть ручки переноса", "перенести по сетке, не выходя из Live Builder");
                DrawHotkey("Shift + ручки переноса", "копия: уезжает копия, оригинал стоит; у проёма — второе окно");

                LiveBuilderWindowControls.Note("Выделенная коробка, комната, лестница:");
                DrawHotkey("тянуть квадрат", "двигать грань по сетке");
                DrawHotkey("Alt / Alt + Shift", "тянуть обе грани / все стороны");
                DrawHotkey("Ctrl + тянуть", "выдавить новую коробку от грани");
                DrawHotkey("Shift + клик", "по квадратам — набрать несколько граней");
                DrawHotkey("Tab", "повернуть выделенную лестницу");

                LiveBuilderWindowControls.Note("Точки выделенной стены или здания:");
                DrawHotkey("рамка по пустому", "выбрать несколько точек; с Shift — добавить");
                DrawHotkey("Shift + клик", "добавить точку к выбору или убрать");
                DrawHotkey("ручки переноса выбранных", "двигать все выбранные по осям; у стены — и вверх");
                DrawHotkey("«+» на отрезке", "новая точка; за концом линии — продлить");
                DrawHotkey("двойной клик", "вид точки: угол, плавно, Безье");
                DrawHotkey("клавиши ←↑→↓", "сдвинуть выбранные на шаг сетки");
                DrawHotkey("Delete", "убрать выбранные точки");
                DrawHotkey("C", "замкнуть или разомкнуть стену");
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
        private void DrawPalette()
        {
            var tools = LiveBuilderToolPalette.Tools;
            var columns = (tools.Count + 1) / 2;
            EditorGUILayout.Space(4f);

            for (var start = 0; start < tools.Count; start += columns)
                DrawToolRow(tools, start, Mathf.Min(columns, tools.Count - start), columns);

            EditorGUILayout.Space(2f);
        }

        private void DrawToolRow(IReadOnlyList<LiveBuilderTool> tools, int start, int count, int columns)
        {
            var row = EditorGUILayout.GetControlRect(false, TileHeight);
            var tileWidth = (row.width - GroupGap - TileGap * (columns - 2)) / columns;

            for (var index = 0; index < count; index++)
            {
                var tool = tools[start + index];
                var x = row.x + index * (tileWidth + TileGap) + (index > 0 ? GroupGap - TileGap : 0f);
                var tile = new Rect(x, row.y, tileWidth, row.height);

                var isCurrent = tool == LiveBuilderToolPalette.Current;

                if (!LiveBuilderWindowControls.ToolTile(tile, tool, isCurrent))
                    continue;

                LiveBuilderToolPalette.Current = tool;
                LiveBuilderActivation.SetToolActive(true);

                // Клик по уже выбранному палитра не считает сменой — вкладку «Инструмент» открываем сами.
                if (isCurrent)
                    OnToolChanged();

                // Под палитрой теперь настройки другого инструмента, а то и другая вкладка — этот проход окна прерываем.
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.Space(2f);
        }


        // Строка о выделенном под палитрой — при любой открытой вкладке; клик — открыть «Выделено».
        private void DrawSelectionStrip()
        {
            if (!LiveBuilderSelectionStrip.Draw())
                return;

            var selectionTab = tabs.Find(tab => tab is LiveBuilderSelectionTab);

            if (selectionTab == null || selectionTab == SelectedTab)
                return;

            SelectedTab = selectionTab;
            scrollPosition = Vector2.zero;
            GUIUtility.ExitGUI();
        }


        // ── Вкладки ──────────────────────────────────────────────────────

        // Корешки и «?»; клик по корешку или «?» меняет, что ниже, — этот проход окна прерываем. Возвращает ряд корешков: под ним начинается страница.
        private Rect DrawTabBar()
        {
            var row = EditorGUILayout.GetControlRect(false, LiveBuilderTabBar.Height);
            var selectedTab = SelectedTab;
            var selectedIndex = tabs.IndexOf(selectedTab);
            var isHelpShown = IsHelpShown(selectedTab);
            var chosenIndex = LiveBuilderTabBar.Draw(row, tabs, selectedIndex, ref isHelpShown);

            if (chosenIndex != selectedIndex)
            {
                SelectedTab = tabs[chosenIndex];
                GUIUtility.ExitGUI();
            }

            if (isHelpShown != IsHelpShown(selectedTab))
            {
                EditorPrefs.SetBool(HelpPreferencePrefix + selectedTab.PreferenceKey, isHelpShown);
                GUIUtility.ExitGUI();
            }

            return row;
        }


        // Содержимое вкладки; пояснения в ней видны, только пока нажата её «?».
        private static void DrawTab(LiveBuilderWindowTab tab)
        {
            LiveBuilderWindowControls.IsShowingExplanations = IsHelpShown(tab);

            try
            {
                tab.Draw();
            }
            finally
            {
                LiveBuilderWindowControls.IsShowingExplanations = false;
            }
        }


        // Пояснения сначала видны: новичку они нужнее всего; кто привык — спрячет «?», и это запомнится.
        private static bool IsHelpShown(LiveBuilderWindowTab tab)
        {
            return EditorPrefs.GetBool(HelpPreferencePrefix + tab.PreferenceKey, true);
        }


        // ── Вкладки сами ─────────────────────────────────────────────────

        // Сменилось выделение — открыть вкладку, которая этого хочет («Выделено» у построек).
        private void OnSelectionChanged()
        {
            RequestTab(tab => tab.ShouldOpenWhenSelectionChanges);
            Repaint();
        }


        // Выбран инструмент — открыть вкладку, которая этого хочет («Инструмент»).
        private void OnToolChanged()
        {
            RequestTab(tab => tab.ShouldOpenWhenToolChanges);
            Repaint();
        }


        // Первая вкладка, которая хочет открыться, — откроется в начале следующего прохода окна.
        // Выключено на вкладке «Уровень» или печатаешь в поле — ничего не переключается: поле не пропадёт из-под рук.
        private void RequestTab(System.Predicate<LiveBuilderWindowTab> wantsToOpen)
        {
            if (!IsSwitchingTabsAutomatically || EditorGUIUtility.editingTextField)
                return;

            var tab = tabs.Find(wantsToOpen);

            if (tab != null)
                pendingTab = tab;
        }


        // Открыть запрошенную вкладку — только в начале прохода разметки, пока ничего не нарисовано.
        private void ApplyPendingTab()
        {
            if (pendingTab == null || Event.current.type != EventType.Layout)
                return;

            if (pendingTab != SelectedTab)
            {
                SelectedTab = pendingTab;
                scrollPosition = Vector2.zero;
            }

            pendingTab = null;
        }


        // ── Подсказка ────────────────────────────────────────────────────

        // Внизу окна — что делать сейчас, как внизу окна сцены. Сцена ещё не перерисовалась — что делает инструмент.
        private static string HintText()
        {
            if (!LiveBuilderActivation.IsToolActive)
                return "Live Builder выключен — B в окне сцены или кнопка вверху";

            var text = LiveBuilderSceneTool.CurrentHintText;
            return string.IsNullOrEmpty(text) ? LiveBuilderToolPalette.Current.Tooltip : text;
        }

        private float HintHeight(string hint)
        {
            var textWidth = position.width - HintPadding.padding.horizontal - HintIconSize - 6f;
            return Mathf.Ceil(HintTextStyle.CalcHeight(new GUIContent(hint), textWidth)) + HintPadding.padding.vertical;
        }

        // Полоса цвета инструмента: значок инструмента и текст. Live Builder выключен — серая, без значка.
        private static void DrawHint(Rect rect, string text)
        {
            var isActive = LiveBuilderActivation.IsToolActive;
            var inner = HintPadding.padding.Remove(rect);

            if (Event.current.type == EventType.Repaint)
            {
                if (isActive)
                {
                    EditorGUI.DrawRect(rect, LiveBuilderTabBar.TintOf(LiveBuilderTabColor.Tool, 0.14f));
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), LiveBuilderTabBar.ColorOf(LiveBuilderTabColor.Tool));
                    LiveBuilderToolIcons.Draw(new Rect(inner.x, inner.y + 1f, HintIconSize, HintIconSize), LiveBuilderToolPalette.Current, LiveBuilderIconCache.ThemedColor);
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
            tabs.AddRange(LiveBuilderTypeCatalog.CreateAll<LiveBuilderWindowTab>());
            tabs.Sort((first, second) => first.Order.CompareTo(second.Order));
            Selection.selectionChanged += OnSelectionChanged;
            LiveBuilderStoreys.Changed += Repaint;
            LiveBuilderToolPalette.Changed += OnToolChanged;
            LiveBuilderRoomSettings.ShapeChanged += Repaint;
            LiveBuilderSceneTool.HintChanged += Repaint;
            LiveBuilderContourSelection.Changed += Repaint;
            ToolManager.activeToolChanged += Repaint;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            LiveBuilderStoreys.Changed -= Repaint;
            LiveBuilderToolPalette.Changed -= OnToolChanged;
            LiveBuilderRoomSettings.ShapeChanged -= Repaint;
            LiveBuilderSceneTool.HintChanged -= Repaint;
            LiveBuilderContourSelection.Changed -= Repaint;
            ToolManager.activeToolChanged -= Repaint;

            foreach (var tab in tabs)
                tab.Dispose();

            tabs.Clear();
        }
    }
}
