using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Поля настроек, общие у нескольких инструментов Live Builder: строка инструмента панели (режимы, свои настройки, «Into», To top и «⋯»),
    /// что выделяет клик, угол или плавно у новой точки, где линия относительно стен, перегородка в здании (и изменена ли она — для «Ещё»).
    /// Свои настройки каждый инструмент рисует сам (<see cref="LiveBuilderTool.DrawSettings"/>).
    /// Числа хранятся у каждого человека свои — настройки инструментов, например <see cref="LiveBuilderRoomSettings"/>.
    /// </summary>
    public static class LiveBuilderToolSettingsFields
    {
        // Что выделяет клик — у Select и Box: коробку целиком или кусок грани.
        public static void DrawSelectionModeSwitch()
        {
            var selectionMode = (BoxModelSelectionMode)LiveBuilderWindowControls.Switch(
                new GUIContent("Click Selects", "Objects — коробку целиком, тянешь точки на её гранях. " +
                                                "Faces — видимый кусок грани, двойной клик — грань целиком, Shift — несколько; ручка двигает все"),
                (int)BoxModelSelectionModeSetting.Current, ("Objects", LiveBuilderOptionIcon.Boxes), ("Faces", LiveBuilderOptionIcon.Faces));

            if (selectionMode != BoxModelSelectionModeSetting.Current)
                BoxModelSelectionModeSetting.Current = selectionMode;
        }


        // Угол или плавно — общий для стены, здания и комнаты по контуру.
        public static void DrawKnotModeSwitch()
        {
            LiveBuilderWallSettings.IsSmooth = LiveBuilderWindowControls.Switch(
                new GUIContent("New Point", "Corner — прямые отрезки. Smooth — кривая через точки. Shift на клике — наоборот"),
                LiveBuilderWallSettings.IsSmooth, ("Corner", LiveBuilderOptionIcon.Corner), ("Smooth", LiveBuilderOptionIcon.Smooth));
        }


        // Где нарисованная линия относительно стен — одними словами у здания и комнаты: 0 — снаружи стен, 1 — изнутри, 2 — по их оси.
        public static int DrawLineSideSwitch(int lineSide)
        {
            return LiveBuilderWindowControls.Switch(
                new GUIContent("Line", "Где нарисованная линия относительно стен. Outside — линия это габарит, стены встают внутрь от неё. " +
                                       "Inside — линия это комната изнутри, стены встают наружу. On Axis — линия идёт по середине стен"),
                lineSide, "Outside", "Inside", "On Axis");
        }


        // Перегородка внутри здания — у стены и комнаты общая: тип или своя толщина.
        public static void DrawPartitionThickness()
        {
            LiveBuilderWallSettings.PartitionWallType = LiveBuilderWallTypes.Popup(
                new GUIContent("Partition Type", "Тип стен внутри здания по контуру. Высота у них — до потолка его этажа, сама"),
                LiveBuilderWallSettings.PartitionWallType);

            if (LiveBuilderWallSettings.PartitionWallType != null)
                return;

            LiveBuilderWallSettings.PartitionThickness = EditorGUILayout.FloatField(
                new GUIContent("Partition Thickness, m", "Толщина стен внутри здания по контуру. Высота у них — до потолка его этажа, сама"),
                LiveBuilderWallSettings.PartitionThickness);
        }

        // Перегородка не как по умолчанию: выбран тип или своя толщина другая.
        public static bool IsPartitionChanged()
        {
            return LiveBuilderWallSettings.PartitionWallType != null
                || !Mathf.Approximately(LiveBuilderWallSettings.PartitionThickness, LiveBuilderWallSettings.FactoryPartitionThickness);
        }


        // ── Строка инструмента ───────────────────────────────────────────

        // Режимы, свои частые настройки инструмента, куда встанет новое и «⋯» с редким — то, что меняют каждый раз. Рисует панель окна сцены.
        public static void DrawToolLine(LiveBuilderTool tool)
        {
            DrawModes(tool);
            tool.DrawSettings();
            DrawIntoLine(tool);
        }

        // Имя инструмента и его режимы: клавиша инструмента ещё раз — следующий, с Shift — предыдущий.
        private static void DrawModes(LiveBuilderTool tool)
        {
            var modes = tool.Modes;

            if (modes == null)
                return;

            var key = LiveBuilderToolPalette.HotkeyOf(tool);
            var tooltip = "Клавиша инструмента (" + key + ") ещё раз — следующий режим, Shift + " + key + " — предыдущий.";

            foreach (var modeLabel in modes.Labels)
                tooltip += "\n" + modeLabel.Name + " — " + modeLabel.Tooltip;

            var toolLabel = new GUIContent(tool.DisplayName, tooltip);
            modes.CurrentIndex = modes.HasIcons
                ? LiveBuilderWindowControls.Switch(toolLabel, modes.CurrentIndex, IconOptionsOf(modes))
                : LiveBuilderWindowControls.Switch(toolLabel, modes.CurrentIndex, NamesOf(modes));
        }

        private static (string Text, LiveBuilderOptionIcon Icon)[] IconOptionsOf(LiveBuilderToolModes modes)
        {
            var options = new (string, LiveBuilderOptionIcon)[modes.Count];

            for (var index = 0; index < options.Length; index++)
                options[index] = (modes.Labels[index].Name, modes.Labels[index].Icon ?? default);

            return options;
        }

        private static string[] NamesOf(LiveBuilderToolModes modes)
        {
            var names = new string[modes.Count];

            for (var index = 0; index < names.Length; index++)
                names[index] = modes.Labels[index].Name;

            return names;
        }


        // «Into: …» — куда встанет новое; у тех, кто ставит своё в здание (<see cref="LiveBuilderTool.PlacesIntoBuilding"/>), рядом To top; в конце — «⋯».
        private static void DrawIntoLine(LiveBuilderTool tool)
        {
            var intoTarget = tool.IntoTarget;

            if (intoTarget == null && tool.MoreSettingsSummary == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (intoTarget != null)
                {
                    GUILayout.Label(new GUIContent("Into", "Куда встанет новое: место под мышью в сцене, иначе выделенное и этаж"), EditorStyles.miniLabel,
                        GUILayout.Width(28f));
                    LiveBuilderWindowControls.ValueChip(new GUIContent(intoTarget, intoTarget), GUILayout.MinWidth(40f));
                }
                else
                {
                    GUILayout.FlexibleSpace();
                }

                if (tool.PlacesIntoBuilding)
                    DrawToTopToggle();

                if (tool.MoreSettingsSummary != null)
                    DrawMoreButton(tool);
            }
        }


        // «⋯» — редкое во всплывающем окошке; точка в углу — там что-то не по умолчанию: спрятанное не удивит.
        private static void DrawMoreButton(LiveBuilderTool tool)
        {
            var tooltip = (tool.AreMoreSettingsChanged ? "Изменено: " : "Ещё: ") + tool.MoreSettingsSummary;
            var rect = GUILayoutUtility.GetRect(new GUIContent("⋯"), EditorStyles.miniButton, GUILayout.Width(24f), GUILayout.Height(18f));

            if (GUI.Button(rect, new GUIContent("⋯", tooltip), EditorStyles.miniButton))
                LiveBuilderMoreSettingsPopup.Show(rect, tool);

            if (tool.AreMoreSettingsChanged && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(new Rect(rect.xMax - 7f, rect.y + 2f, 5f, 5f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                    LiveBuilderWindowControls.AccentColor, 0f, 2.5f);
        }


        // To top — то же на каждом этаже здания выше, до верха: повтор по этажам (Array Y — To top).
        private static void DrawToTopToggle()
        {
            LiveBuilderBuildingContext.IsOnAllStoreys = LiveBuilderWindowControls.IconToggle(LiveBuilderBuildingContext.IsOnAllStoreys,
                LiveBuilderOptionIcon.AllStoreysAbove, "To top",
                "В здании — на этом этаже и на всех выше, до верха: перегородки, лестничная клетка, окна по фасаду. " +
                "Добавишь этаж — копия добавится. После перезапуска Unity — снова выключено");
        }
    }
}
