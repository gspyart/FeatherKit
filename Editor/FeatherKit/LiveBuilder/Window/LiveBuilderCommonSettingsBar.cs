using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Нижняя строка панели Live Builder — то, что действует всегда, при любом инструменте и выделении, одной строкой значков:
    /// на каком этаже строишь (◀ N ▶ и высота пола), шаг сетки (выпадающий список) и прилипание к точкам построек, что видно в окне сцены (вид «All / Rooms / Walls down»)
    /// и каркасы коробок — все или только выделенных. Слова — во всплывающих подсказках. В узкой панели пол и каркасы прячутся, вид — одной кнопкой со списком.
    /// </summary>
    public static class LiveBuilderCommonSettingsBar
    {
        private const float RowHeight = 20f;
        private const float StoreyArrowWidth = 20f;
        private const float StoreyFieldWidth = 26f;
        private const float IconButtonWidth = 24f;
        private const float GroupGap = 6f;


        public static void Draw(bool isNarrow)
        {
            using (new EditorGUIUtility.IconSizeScope(new Vector2(LiveBuilderWindowControls.IconSize, LiveBuilderWindowControls.IconSize)))
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(RowHeight)))
            {
                DrawStorey(isNarrow);
                GUILayout.Space(GroupGap);
                DrawGrid();
                DrawPointSnap();
                GUILayout.Space(GroupGap);

                if (isNarrow)
                {
                    DrawViewMenu();
                    return;
                }

                DrawView();
                GUILayout.Space(GroupGap - 2f);
                DrawWireframes();
            }
        }


        // ── Этаж ─────────────────────────────────────────────────────────

        // Значок этажа, ◀ номер ▶ и бледная плашка с высотой пола — в узкой панели её нет.
        private static void DrawStorey(bool isNarrow)
        {
            var downKey = LiveBuilderShortcutIds.KeyLabelOf(LiveBuilderShortcutIds.StoreyDown);
            var upKey = LiveBuilderShortcutIds.KeyLabelOf(LiveBuilderShortcutIds.StoreyUp);
            var tooltip = "На каком этаже строишь — и в пустоте, и в зданиях: их этажи те же. Первый этаж — 1, подвал — −1. " + upKey + " и " + downKey + " в окне сцены";
            GUILayout.Label(new GUIContent(LiveBuilderOptionIcons.Themed(LiveBuilderOptionIcon.Storey), tooltip), GUILayout.Width(18f), GUILayout.Height(RowHeight));

            if (GUILayout.Button(new GUIContent("◀", "Этаж ниже (" + downKey + ")"), EditorStyles.miniButtonLeft, GUILayout.Width(StoreyArrowWidth)))
                LiveBuilderStoreys.CurrentStorey--;

            var storeyNumber = EditorGUILayout.IntField(LiveBuilderStoreys.CurrentStoreyNumber, EditorStyles.miniTextField, GUILayout.Width(StoreyFieldWidth));

            if (storeyNumber != LiveBuilderStoreys.CurrentStoreyNumber)
                LiveBuilderStoreys.CurrentStoreyNumber = storeyNumber;

            if (GUILayout.Button(new GUIContent("▶", "Этаж выше (" + upKey + ")"), EditorStyles.miniButtonRight, GUILayout.Width(StoreyArrowWidth)))
                LiveBuilderStoreys.CurrentStorey++;

            if (isNarrow)
                return;

            GUILayout.Space(3f);
            var floor = LiveBuilderSceneText.FormatMeters(LiveBuilderStoreys.CurrentFloorHeight) + " m";
            LiveBuilderWindowControls.ValueChip(new GUIContent(floor, "Пол этого этажа в мире: " + floor), GUILayout.MinWidth(30f), GUILayout.MaxWidth(64f));
        }


        // ── Сетка ────────────────────────────────────────────────────────

        // Кнопка со значком сетки и шагом; клик — список шагов.
        private static void DrawGrid()
        {
            var content = new GUIContent(" " + LiveBuilderSceneText.FormatMeters(LiveBuilderGrid.CurrentStep), LiveBuilderOptionIcons.Themed(LiveBuilderOptionIcon.Grid),
                "Шаг сетки, м: всё прилипает к клеткам этого размера. Клавиши " + LiveBuilderShortcutIds.KeyLabelOf(LiveBuilderShortcutIds.GridStepDown) + " и " +
                LiveBuilderShortcutIds.KeyLabelOf(LiveBuilderShortcutIds.GridStepUp) + " в окне сцены");
            var rect = GUILayoutUtility.GetRect(content, EditorStyles.miniPullDown, GUILayout.Width(58f), GUILayout.Height(18f));

            if (!GUI.Button(rect, content, EditorStyles.miniPullDown))
                return;

            var menu = new GenericMenu();

            foreach (var step in LiveBuilderGrid.Steps)
            {
                var chosenStep = step;
                var isCurrent = Mathf.Abs(step - LiveBuilderGrid.CurrentStep) < 0.0001f;
                menu.AddItem(new GUIContent(LiveBuilderSceneText.FormatMeters(step) + " m"), isCurrent, () => LiveBuilderGrid.SetStep(chosenStep));
            }

            menu.DropDown(rect);
        }


        // Прилипание к углам, серединам и рёбрам построек: нажата — прилипает, бледная — только сетка.
        private static void DrawPointSnap()
        {
            var isEnabled = LiveBuilderPointSnap.IsEnabled;
            var key = LiveBuilderShortcutIds.KeyLabelOf(LiveBuilderShortcutIds.TogglePointSnap);
            var tooltip = isEnabled
                ? "Прилипание к точкам: включено — к углам, серединам и рёбрам построек рядом с мышью и к направляющим линии, сильнее сетки. Клик или " + key +
                  " в окне сцены — выключить"
                : "Прилипание к точкам и направляющим: выключено — только сетка. Клик или " + key + " в окне сцены — включить";
            var isPressed = LiveBuilderWindowControls.IconOnlyToggle(isEnabled, LiveBuilderOptionIcon.SnapPoints, tooltip, GUILayout.Width(IconButtonWidth), GUILayout.Height(18f));

            if (isPressed == isEnabled)
                return;

            LiveBuilderPointSnap.IsEnabled = isPressed;
            SceneView.RepaintAll();
        }


        // ── Вид ──────────────────────────────────────────────────────────

        // Что видно в окне сцены: всё, этаж срезом на 3 м (комнаты) или на 1 м (стены вниз) (<see cref="LiveBuilderViewPresets"/>);
        // своя высота среза (окно уровня, «Storeys») — не горит ни одна кнопка.
        private static void DrawView()
        {
            var current = LiveBuilderViewPresets.Current();
            DrawViewButton(LiveBuilderViewPresets.ViewAll, current, LiveBuilderOptionIcon.ViewAll, "Вид: всё — как есть", EditorStyles.miniButtonLeft);
            DrawViewButton(LiveBuilderViewPresets.ViewRooms, current, LiveBuilderOptionIcon.ViewRooms,
                "Вид: комнаты — выше 3 м над полом этажа спрятано, видно стены и проёмы. Только в окне сцены", EditorStyles.miniButtonMid);
            DrawViewButton(LiveBuilderViewPresets.ViewWallsDown, current, LiveBuilderOptionIcon.ViewWallsDown,
                "Вид: стены вниз — выше 1 м спрятано, комнаты видно с любого угла. Только в окне сцены", EditorStyles.miniButtonRight);
        }

        // Узкая панель: значок нынешнего вида, клик — список видов.
        private static void DrawViewMenu()
        {
            var current = LiveBuilderViewPresets.Current();
            var icon = current == LiveBuilderViewPresets.ViewRooms ? LiveBuilderOptionIcon.ViewRooms
                : current == LiveBuilderViewPresets.ViewWallsDown ? LiveBuilderOptionIcon.ViewWallsDown
                : LiveBuilderOptionIcon.ViewAll;
            var content = new GUIContent(LiveBuilderOptionIcons.Themed(icon), "Что видно в окне сцены: всё, комнаты (срез 3 м над полом этажа), стены вниз (срез 1 м)");
            var rect = GUILayoutUtility.GetRect(content, EditorStyles.miniPullDown, GUILayout.Width(36f), GUILayout.Height(18f));

            if (!GUI.Button(rect, content, EditorStyles.miniPullDown))
                return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("All"), current == LiveBuilderViewPresets.ViewAll, () => LiveBuilderViewPresets.Apply(LiveBuilderViewPresets.ViewAll));
            menu.AddItem(new GUIContent("Rooms"), current == LiveBuilderViewPresets.ViewRooms, () => LiveBuilderViewPresets.Apply(LiveBuilderViewPresets.ViewRooms));
            menu.AddItem(new GUIContent("Walls down"), current == LiveBuilderViewPresets.ViewWallsDown, () => LiveBuilderViewPresets.Apply(LiveBuilderViewPresets.ViewWallsDown));
            menu.DropDown(rect);
        }

        private static void DrawViewButton(int preset, int current, LiveBuilderOptionIcon icon, string tooltip, GUIStyle style)
        {
            var isCurrent = current == preset;
            var content = new GUIContent(LiveBuilderOptionIcons.Themed(icon), tooltip);

            if (GUILayout.Toggle(isCurrent, content, style, GUILayout.Width(IconButtonWidth), GUILayout.Height(18f)) && !isCurrent)
                LiveBuilderViewPresets.Apply(preset);
        }


        // ── Каркасы ──────────────────────────────────────────────────────

        // Каркасы коробок, голубые и красные: нажата — у всех коробок выделенного объекта, бледная — только у выделенных.
        private static void DrawWireframes()
        {
            var isShowingAll = BoxModelWireframesSetting.IsShowingAll;
            var tooltip = isShowingAll
                ? "Каркасы: все — голубые (добавляют) и красные (вырезают) каркасы всех коробок выделенного объекта. Клик — только у выделенных"
                : "Каркасы: только выделенные — у выделенной коробки или её грани, в сложном объекте не мешают. Клик — у всех";
            var isPressed = LiveBuilderWindowControls.IconOnlyToggle(isShowingAll, LiveBuilderOptionIcon.Wireframe, tooltip, GUILayout.Width(IconButtonWidth), GUILayout.Height(18f));

            if (isPressed != isShowingAll)
                BoxModelWireframesSetting.IsShowingAll = isPressed;
        }
    }
}
