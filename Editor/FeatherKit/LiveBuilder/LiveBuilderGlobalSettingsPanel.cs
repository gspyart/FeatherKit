using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Секция «Общее» окна Live Builder — то, что действует всегда, при любом инструменте и выделении:
    /// на каком этаже строишь, сетка, что видно в окне сцены (срез этажа, каркасы коробок).
    /// Настройки конкретного инструмента — в <see cref="LiveBuilderToolSettingsPanel"/>.
    /// </summary>
    public static class LiveBuilderGlobalSettingsPanel
    {
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

            var chosen = LiveBuilderWindowControls.Switch(new GUIContent("Сетка, м", "Всё прилипает к клеткам этого размера. Клавиши [ и ] в окне сцены"), current, names);

            if (chosen != current)
                BoxModelGrid.SetStep(steps[chosen]);
        }


        // ── Вид ──────────────────────────────────────────────────────────

        // Что видно в окне сцены: всё, этаж срезом на 3 м (комнаты) или на 1 м (стены вниз). Игру не меняет.
        private static void DrawSection()
        {
            var isSectioned = LiveBuilderStoreys.IsSectionEnabled;
            var height = LiveBuilderStoreys.SectionHeightAboveFloor;
            var current = !isSectioned ? 0 : Mathf.Approximately(height, 1f) ? 2 : 1;

            var chosen = LiveBuilderWindowControls.Switch(
                new GUIContent("Вид", "Всё — как есть. Комнаты — выше 3 м над полом этажа спрятано, видно стены и проёмы. " +
                                      "Стены вниз — выше 1 м: видно комнаты с любого угла. Только в окне сцены"),
                current, "Всё", "Комнаты", "Стены вниз");

            if (chosen == current)
                return;

            if (chosen > 0)
                LiveBuilderStoreys.SectionHeightAboveFloor = chosen == 1 ? 3f : 1f;

            LiveBuilderStoreys.IsSectionEnabled = chosen > 0;
        }


        // Каркасы коробок, голубые и красные: у всех коробок выделенного объекта или только у выделенных.
        private static void DrawWireframes()
        {
            var isShowingOnlySelected = LiveBuilderWindowControls.Switch(
                new GUIContent("Каркасы", "Голубые (добавляют) и красные (вырезают) каркасы коробок в окне сцены. " +
                                          "Выделенные — только у выделенной коробки или её грани: в сложном объекте каркасы не мешают"),
                !BoxModelWireframesSetting.IsShowingAll, "Все", "Выделенные");

            if (isShowingOnlySelected == BoxModelWireframesSetting.IsShowingAll)
                BoxModelWireframesSetting.IsShowingAll = !isShowingOnlySelected;
        }
    }
}
