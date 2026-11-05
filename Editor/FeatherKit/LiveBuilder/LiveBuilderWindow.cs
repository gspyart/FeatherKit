using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Окно Live Builder — пульт инструмента сборки уровня, одной колонкой сверху вниз, по тому, от чего настройка зависит:
    /// - шапка — включён ли Live Builder, «i» — все пояснения сразу, клавиатура — клавиши
    /// - палитра — инструменты плитками в два ряда (<see cref="LiveBuilderToolPalette"/>), выбранная залита синим; цифра на плитке — клавиша в окне сцены
    /// - цветные зоны (<see cref="LiveBuilderWindowZone"/>, вид — <see cref="LiveBuilderZoneFrame"/>) — окно находит их само и ставит по порядку:
    ///   серая «Общее» — действует всегда: этаж, сетка, вид; не прокручивается (<see cref="LiveBuilderGlobalZone"/>);
    ///   синяя — настройки выбранного инструмента, только те, что на него действуют (<see cref="LiveBuilderToolZone"/>);
    ///   оранжевая «Выделено» — числа и действия выделенного; зоны нет, пока не выделено своё (<see cref="LiveBuilderSelectionZone"/>);
    ///   фиолетовая «Уровень» — этажи уровня, типы стен, уборка мешей; меняют редко, свёрнута (<see cref="LiveBuilderLevelZone"/>)
    /// - внизу — подсказка, что делать сейчас, та же, что в окне сцены, со значком инструмента
    /// Окно само ничего не переключает: зоны сворачиваешь ты, они это помнят. Рисование мышью — в окне сцены (<see cref="LiveBuilderSceneTool"/>).
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

        // Зоны по порядку: создаются, когда окно открывается, и отпускаются, когда закрывается.
        private readonly List<LiveBuilderWindowZone> zones = new();
        private Vector2 scrollPosition;
        private bool isShowingHotkeys;


        private static GUIStyle TopPadding => topPadding ??= new GUIStyle { padding = new RectOffset(8, 8, 6, 0) };

        private static GUIStyle HintPadding => hintPadding ??= new GUIStyle { padding = new RectOffset(8, 8, 4, 5) };

        private static GUIStyle HintTextStyle => hintTextStyle ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel);


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

            DrawZones(false);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);
            DrawZones(true);
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
                var isActive = LiveBuilderActivation.IsToolActive;
                var previous = GUI.backgroundColor;
                GUI.backgroundColor = isActive ? new Color(0.45f, 0.85f, 0.5f) : previous;
                var label = isActive ? " Live Builder включён · B" : " Включить Live Builder · B";
                var power = new GUIContent(label, LiveBuilderOptionIcons.Themed(LiveBuilderOptionIcon.Power), "Пока включён, мышь в окне сцены строит выбранным инструментом");

                if (GUILayout.Toggle(isActive, power, "Button", GUILayout.Height(24f)) != isActive)
                    LiveBuilderActivation.SetToolActive(!isActive);

                GUI.backgroundColor = previous;

                var isShowingAll = LiveBuilderWindowControls.IsShowingAllExplanations;
                var info = new GUIContent(LiveBuilderOptionIcons.Themed(LiveBuilderOptionIcon.Info), "Все пояснения «Как пользоваться» — сразу открыты");

                // Пояснения и клавиши появятся или пропадут — этот проход окна прерываем, иначе IMGUI путается в разметке.
                if (GUILayout.Toggle(isShowingAll, info, "Button", GUILayout.Width(28f), GUILayout.Height(24f)) != isShowingAll)
                {
                    LiveBuilderWindowControls.IsShowingAllExplanations = !isShowingAll;
                    GUIUtility.ExitGUI();
                }

                var keys = new GUIContent(LiveBuilderOptionIcons.Themed(LiveBuilderOptionIcon.Keyboard), "Клавиши в окне сцены");

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
            var tools = LiveBuilderToolPalette.Tools;
            var columns = (tools.Count + 1) / 2;
            EditorGUILayout.Space(4f);

            for (var start = 0; start < tools.Count; start += columns)
                DrawToolRow(tools, start, Mathf.Min(columns, tools.Count - start), columns);

            EditorGUILayout.Space(2f);
        }

        private static void DrawToolRow(IReadOnlyList<LiveBuilderTool> tools, int start, int count, int columns)
        {
            var row = EditorGUILayout.GetControlRect(false, TileHeight);
            var tileWidth = (row.width - GroupGap - TileGap * (columns - 2)) / columns;

            for (var index = 0; index < count; index++)
            {
                var tool = tools[start + index];
                var x = row.x + index * (tileWidth + TileGap) + (index > 0 ? GroupGap - TileGap : 0f);
                var tile = new Rect(x, row.y, tileWidth, row.height);

                if (!LiveBuilderWindowControls.ToolTile(tile, tool, tool == LiveBuilderToolPalette.Current))
                    continue;

                LiveBuilderToolPalette.Current = tool;
                LiveBuilderActivation.SetToolActive(true);
            }

            EditorGUILayout.Space(2f);
        }


        // ── Зоны ─────────────────────────────────────────────────────────

        // Прибитые сверху или прокручиваемые — по порядку. Зоны, которой сейчас нечего показать, нет совсем.
        private void DrawZones(bool isScrolled)
        {
            foreach (var zone in zones)
            {
                if (zone.IsScrolled != isScrolled || !zone.IsVisible)
                    continue;

                using (var scope = LiveBuilderZoneFrame.Begin(zone.ZoneColor, zone.Title, zone.Detail, zone.Icon, zone.PreferenceKey, zone.IsExpandedByDefault))
                    if (scope.IsExpanded)
                        zone.Draw();
            }
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
                    EditorGUI.DrawRect(rect, LiveBuilderZoneFrame.TintOf(LiveBuilderZoneColor.Tool, 0.14f));
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), LiveBuilderZoneFrame.ColorOf(LiveBuilderZoneColor.Tool));
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
            zones.AddRange(LiveBuilderTypeCatalog.CreateAll<LiveBuilderWindowZone>());
            zones.Sort((first, second) => first.Order.CompareTo(second.Order));
            Selection.selectionChanged += Repaint;
            LiveBuilderStoreys.Changed += Repaint;
            LiveBuilderToolPalette.Changed += Repaint;
            LiveBuilderRoomSettings.ShapeChanged += Repaint;
            LiveBuilderSceneTool.HintChanged += Repaint;
            LiveBuilderContourSelection.Changed += Repaint;
            ToolManager.activeToolChanged += Repaint;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
            LiveBuilderStoreys.Changed -= Repaint;
            LiveBuilderToolPalette.Changed -= Repaint;
            LiveBuilderRoomSettings.ShapeChanged -= Repaint;
            LiveBuilderSceneTool.HintChanged -= Repaint;
            LiveBuilderContourSelection.Changed -= Repaint;
            ToolManager.activeToolChanged -= Repaint;

            foreach (var zone in zones)
                zone.Dispose();

            zones.Clear();
        }
    }
}
