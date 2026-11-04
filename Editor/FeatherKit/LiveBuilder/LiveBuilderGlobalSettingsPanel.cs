using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Зона «Общее» окна Live Builder — то, что действует всегда, при любом инструменте и выделении:
    /// на каком этаже строишь, сетка, что видно в окне сцены (срез этажа, каркасы коробок).
    /// Настройки конкретного инструмента — в <see cref="LiveBuilderToolSettingsPanel"/>.
    /// </summary>
    public static class LiveBuilderGlobalSettingsPanel
    {
        // Виды окна сцены — общие с панелью над сценой (<see cref="LiveBuilderSceneOverlay"/>). Своя высота среза (панель «Уровень») —
        // ViewCustom: не горит ни одна кнопка, даже «Всё», ведь срез включён.
        public const int ViewCustom = -1;
        public const int ViewAll = 0;
        public const int ViewRooms = 1;
        public const int ViewWallsDown = 2;

        // Срез «Комнаты» и «Стены вниз» — на сколько выше пола этажа, м.
        private const float RoomsSectionHeight = 3f;
        private const float WallsDownSectionHeight = 1f;


        public static void Draw()
        {
            DrawStorey();
            DrawGrid();
            DrawSection();
            DrawWireframes();
        }


        // ── Этаж ─────────────────────────────────────────────────────────

        private static void DrawStorey()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                LiveBuilderWindowControls.PrefixLabel(LiveBuilderWindowControls.IconLabel("Этаж", LiveBuilderOptionIcon.Storey,
                    "На каком этаже строишь — и в пустоте, и в зданиях: их этажи те же. PageUp и PageDown в окне сцены"));

                if (GUILayout.Button("◀", EditorStyles.miniButtonLeft, GUILayout.Width(22f)))
                    LiveBuilderStoreys.CurrentStorey--;

                var storey = EditorGUILayout.IntField(LiveBuilderStoreys.CurrentStorey, EditorStyles.miniTextField, GUILayout.Width(30f));

                if (storey != LiveBuilderStoreys.CurrentStorey)
                    LiveBuilderStoreys.CurrentStorey = storey;

                if (GUILayout.Button("▶", EditorStyles.miniButtonRight, GUILayout.Width(22f)))
                    LiveBuilderStoreys.CurrentStorey++;

                LiveBuilderWindowControls.ValueChip(new GUIContent("пол " + BoxModelSceneText.FormatMeters(LiveBuilderStoreys.CurrentFloorHeight) + " м",
                    "Высота пола этого этажа в мире"));
            }
        }


        // ── Сетка ────────────────────────────────────────────────────────

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

            var chosen = LiveBuilderWindowControls.Switch(
                LiveBuilderWindowControls.IconLabel("Сетка, м", LiveBuilderOptionIcon.Grid, "Всё прилипает к клеткам этого размера. Клавиши [ и ] в окне сцены"), current, names);

            if (chosen != current)
                BoxModelGrid.SetStep(steps[chosen]);
        }


        // ── Вид ──────────────────────────────────────────────────────────

        // Что видно в окне сцены: всё, этаж срезом на 3 м (комнаты) или на 1 м (стены вниз). Игру не меняет.
        private static void DrawSection()
        {
            var current = CurrentViewPreset();

            var chosen = LiveBuilderWindowControls.Switch(
                LiveBuilderWindowControls.IconLabel("Вид", LiveBuilderOptionIcon.Eye, "Всё — как есть. Комнаты — выше 3 м над полом этажа спрятано, видно стены и проёмы. " +
                                                                                     "Стены вниз — выше 1 м: видно комнаты с любого угла. Только в окне сцены"),
                current, ("Всё", LiveBuilderOptionIcon.ViewAll), ("Комнаты", LiveBuilderOptionIcon.ViewRooms), ("Стены вниз", LiveBuilderOptionIcon.ViewWallsDown));

            if (chosen != current && chosen >= 0)
                ApplyViewPreset(chosen);
        }


        // Какой из видов сейчас: Всё, Комнаты, Стены вниз или своя высота среза.
        public static int CurrentViewPreset()
        {
            if (!LiveBuilderStoreys.IsSectionEnabled)
                return ViewAll;

            var height = LiveBuilderStoreys.SectionHeightAboveFloor;

            if (Mathf.Approximately(height, RoomsSectionHeight))
                return ViewRooms;

            return Mathf.Approximately(height, WallsDownSectionHeight) ? ViewWallsDown : ViewCustom;
        }


        public static void ApplyViewPreset(int preset)
        {
            if (preset == ViewRooms || preset == ViewWallsDown)
                LiveBuilderStoreys.SectionHeightAboveFloor = preset == ViewRooms ? RoomsSectionHeight : WallsDownSectionHeight;

            LiveBuilderStoreys.IsSectionEnabled = preset != ViewAll;
        }


        // Каркасы коробок, голубые и красные: у всех коробок выделенного объекта или только у выделенных.
        private static void DrawWireframes()
        {
            var isShowingOnlySelected = LiveBuilderWindowControls.Switch(
                LiveBuilderWindowControls.IconLabel("Каркасы", LiveBuilderOptionIcon.Wireframe, "Голубые (добавляют) и красные (вырезают) каркасы коробок в окне сцены. " +
                                                                                               "Выделенные — только у выделенной коробки или её грани: в сложном объекте каркасы не мешают"),
                !BoxModelWireframesSetting.IsShowingAll, "Все", "Выделенные");

            if (isShowingOnlySelected == BoxModelWireframesSetting.IsShowingAll)
                BoxModelWireframesSetting.IsShowingAll = !isShowingOnlySelected;
        }
    }
}
