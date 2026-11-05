using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Строка общих настроек окна Live Builder под палитрой — то, что действует всегда, при любом инструменте и выделении, одной строкой значков:
    /// на каком этаже строишь (◀ N ▶ и высота пола), шаг сетки (выпадающий список), что видно в окне сцены (вид «Всё / Комнаты / Стены вниз»)
    /// и каркасы коробок — все или только выделенных. Слова — во всплывающих подсказках. Настройки инструмента — на вкладке <see cref="LiveBuilderToolTab"/>.
    /// </summary>
    public static class LiveBuilderCommonSettingsBar
    {
        private const float RowHeight = 20f;
        private const float StoreyArrowWidth = 20f;
        private const float StoreyFieldWidth = 26f;
        private const float IconButtonWidth = 24f;
        private const float GroupGap = 6f;


        public static void Draw()
        {
            using (new EditorGUIUtility.IconSizeScope(new Vector2(LiveBuilderWindowControls.IconSize, LiveBuilderWindowControls.IconSize)))
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(RowHeight)))
            {
                DrawStorey();
                GUILayout.Space(GroupGap);
                DrawGrid();
                GUILayout.Space(GroupGap);
                DrawView();
                GUILayout.Space(GroupGap - 2f);
                DrawWireframes();
            }
        }


        // ── Этаж ─────────────────────────────────────────────────────────

        // Значок этажа, ◀ номер ▶ и бледная плашка с высотой пола — она сжимается первой, когда окно узкое.
        private static void DrawStorey()
        {
            const string tooltip = "На каком этаже строишь — и в пустоте, и в зданиях: их этажи те же. PageUp и PageDown в окне сцены";
            GUILayout.Label(new GUIContent(LiveBuilderOptionIcons.Themed(LiveBuilderOptionIcon.Storey), tooltip), GUILayout.Width(18f), GUILayout.Height(RowHeight));

            if (GUILayout.Button(new GUIContent("◀", "Этаж ниже (PageDown)"), EditorStyles.miniButtonLeft, GUILayout.Width(StoreyArrowWidth)))
                LiveBuilderStoreys.CurrentStorey--;

            var storey = EditorGUILayout.IntField(LiveBuilderStoreys.CurrentStorey, EditorStyles.miniTextField, GUILayout.Width(StoreyFieldWidth));

            if (storey != LiveBuilderStoreys.CurrentStorey)
                LiveBuilderStoreys.CurrentStorey = storey;

            if (GUILayout.Button(new GUIContent("▶", "Этаж выше (PageUp)"), EditorStyles.miniButtonRight, GUILayout.Width(StoreyArrowWidth)))
                LiveBuilderStoreys.CurrentStorey++;

            GUILayout.Space(3f);
            var floor = LiveBuilderSceneText.FormatMeters(LiveBuilderStoreys.CurrentFloorHeight) + " м";
            LiveBuilderWindowControls.ValueChip(new GUIContent(floor, "Пол этого этажа в мире: " + floor), GUILayout.MinWidth(30f), GUILayout.MaxWidth(64f));
        }


        // ── Сетка ────────────────────────────────────────────────────────

        // Кнопка со значком сетки и шагом; клик — список шагов.
        private static void DrawGrid()
        {
            var content = new GUIContent(" " + LiveBuilderSceneText.FormatMeters(LiveBuilderGrid.CurrentStep), LiveBuilderOptionIcons.Themed(LiveBuilderOptionIcon.Grid),
                "Шаг сетки, м: всё прилипает к клеткам этого размера. Клавиши [ и ] в окне сцены");
            var rect = GUILayoutUtility.GetRect(content, EditorStyles.miniPullDown, GUILayout.Width(58f), GUILayout.Height(18f));

            if (!GUI.Button(rect, content, EditorStyles.miniPullDown))
                return;

            var menu = new GenericMenu();

            foreach (var step in LiveBuilderGrid.Steps)
            {
                var chosenStep = step;
                var isCurrent = Mathf.Abs(step - LiveBuilderGrid.CurrentStep) < 0.0001f;
                menu.AddItem(new GUIContent(LiveBuilderSceneText.FormatMeters(step) + " м"), isCurrent, () => LiveBuilderGrid.SetStep(chosenStep));
            }

            menu.DropDown(rect);
        }


        // ── Вид ──────────────────────────────────────────────────────────

        // Что видно в окне сцены: всё, этаж срезом на 3 м (комнаты) или на 1 м (стены вниз). Виды — общие с панелью над сценой (<see cref="LiveBuilderViewPresets"/>);
        // своя высота среза (вкладка «Уровень») — не горит ни одна кнопка.
        private static void DrawView()
        {
            var current = LiveBuilderViewPresets.Current();
            DrawViewButton(LiveBuilderViewPresets.ViewAll, current, LiveBuilderOptionIcon.ViewAll, "Вид: всё — как есть", EditorStyles.miniButtonLeft);
            DrawViewButton(LiveBuilderViewPresets.ViewRooms, current, LiveBuilderOptionIcon.ViewRooms,
                "Вид: комнаты — выше 3 м над полом этажа спрятано, видно стены и проёмы. Только в окне сцены", EditorStyles.miniButtonMid);
            DrawViewButton(LiveBuilderViewPresets.ViewWallsDown, current, LiveBuilderOptionIcon.ViewWallsDown,
                "Вид: стены вниз — выше 1 м спрятано, комнаты видно с любого угла. Только в окне сцены", EditorStyles.miniButtonRight);
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
