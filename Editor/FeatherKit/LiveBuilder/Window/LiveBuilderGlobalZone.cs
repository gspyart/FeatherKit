using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Зона «Общее» окна Live Builder — то, что действует всегда, при любом инструменте и выделении:
    /// на каком этаже строишь, сетка, что видно в окне сцены (срез этажа, каркасы коробок). Не прокручивается — всегда под палитрой.
    /// Настройки конкретного инструмента — в <see cref="LiveBuilderToolZone"/>.
    /// </summary>
    public sealed class LiveBuilderGlobalZone : LiveBuilderWindowZone
    {
        // ── Зона ─────────────────────────────────────────────────────────

        public override string Title => "Общее";

        public override string Detail => "этаж, сетка, вид";

        public override Texture2D Icon => LiveBuilderOptionIcons.White(LiveBuilderOptionIcon.Global);

        public override LiveBuilderZoneColor ZoneColor => LiveBuilderZoneColor.Global;

        public override int Order => 0;

        public override string PreferenceKey => "Global";

        public override bool IsScrolled => false;


        public override void Draw()
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

                LiveBuilderWindowControls.ValueChip(new GUIContent("пол " + LiveBuilderSceneText.FormatMeters(LiveBuilderStoreys.CurrentFloorHeight) + " м",
                    "Высота пола этого этажа в мире"));
            }
        }


        // ── Сетка ────────────────────────────────────────────────────────

        private static void DrawGrid()
        {
            var steps = LiveBuilderGrid.Steps;
            var names = new string[steps.Length];
            var current = 0;

            for (var index = 0; index < steps.Length; index++)
            {
                names[index] = LiveBuilderSceneText.FormatMeters(steps[index]);

                if (Mathf.Abs(steps[index] - LiveBuilderGrid.CurrentStep) < 0.0001f)
                    current = index;
            }

            var chosen = LiveBuilderWindowControls.Switch(
                LiveBuilderWindowControls.IconLabel("Сетка, м", LiveBuilderOptionIcon.Grid, "Всё прилипает к клеткам этого размера. Клавиши [ и ] в окне сцены"), current, names);

            if (chosen != current)
                LiveBuilderGrid.SetStep(steps[chosen]);
        }


        // ── Вид ──────────────────────────────────────────────────────────

        // Что видно в окне сцены: всё, этаж срезом на 3 м (комнаты) или на 1 м (стены вниз). Виды — общие с панелью над сценой (<see cref="LiveBuilderViewPresets"/>).
        private static void DrawSection()
        {
            var current = LiveBuilderViewPresets.Current();

            var chosen = LiveBuilderWindowControls.Switch(
                LiveBuilderWindowControls.IconLabel("Вид", LiveBuilderOptionIcon.Eye, "Всё — как есть. Комнаты — выше 3 м над полом этажа спрятано, видно стены и проёмы. " +
                                                                                     "Стены вниз — выше 1 м: видно комнаты с любого угла. Только в окне сцены"),
                current, ("Всё", LiveBuilderOptionIcon.ViewAll), ("Комнаты", LiveBuilderOptionIcon.ViewRooms), ("Стены вниз", LiveBuilderOptionIcon.ViewWallsDown));

            if (chosen != current && chosen >= 0)
                LiveBuilderViewPresets.Apply(chosen);
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
