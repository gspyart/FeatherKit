using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderIconRasterizer;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Стена»: кликами точки — по ним встаёт стена по линии (<see cref="LiveBuilderPathGesture"/>). Правит только стены по линии.
    /// Мышь и клавиши ведут линию крючки основы; здесь — описание и настройки.
    /// </summary>
    public sealed class LiveBuilderWallTool : LiveBuilderTool
    {
        private readonly LiveBuilderPathGesture wallGesture = new(LiveBuilderPathKind.Wall);


        // ── Описание ─────────────────────────────────────────────────────

        public override string Id => "Wall";

        public override string DisplayName => "Стена";

        public override string Tooltip => "Кликами ставишь точки — по ним встаёт стена. Угол или плавно — переключатель в окне или Shift на точке. Enter — готово";

        public override int PaletteOrder => 3;

        // Кладка: два ряда кирпичей со сдвигом.
        public override List<Vector2[]> IconLines => new()
        {
            Points(3, 6, 21, 6, 21, 18, 3, 18, 3, 6),
            Points(3, 12, 21, 12),
            Points(9, 6, 9, 12), Points(15, 6, 15, 12),
            Points(6, 12, 6, 18), Points(12, 12, 12, 18), Points(18, 12, 18, 18)
        };


        // ── Что правит ───────────────────────────────────────────────────

        public override bool PlacesIntoBuilding => true;


        // ── Окно сцены ───────────────────────────────────────────────────

        public override LiveBuilderPathGesture PathGesture => wallGesture;

        public override string Hint => PathHint(wallGesture);


        // ── Что правит ───────────────────────────────────────────────────

        protected override bool CanEditTarget(Component target)
        {
            return target is SplineWall;
        }


        // ── Окно ─────────────────────────────────────────────────────────

        public override void DrawSettings()
        {
            using (LiveBuilderWindowControls.Card("Линия"))
            {
                LiveBuilderToolSettingsFields.DrawKnotModeSwitch();

                LiveBuilderWallSettings.Alignment = (SplineWallAlignment)LiveBuilderWindowControls.Switch(
                    new GUIContent("Где стена", "Линия посередине стены или по её краю — удобно обводить по краю пола"),
                    (int)LiveBuilderWallSettings.Alignment, "По центру", "Слева", "Справа");
            }

            using (LiveBuilderWindowControls.Card("Размер"))
            {
                LiveBuilderWallSettings.WallType = LiveBuilderWallTypes.Popup(
                    new GUIContent("Тип", "Толщина и материал — из типа, общие для всех стен этого типа: поменял тип — перестроились все. Без типа — свои числа ниже"),
                    LiveBuilderWallSettings.WallType);

                if (LiveBuilderWallSettings.WallType == null)
                    LiveBuilderWallSettings.Thickness = EditorGUILayout.FloatField(new GUIContent("Толщина, м", "Наружная — 0,3, перегородка — 0,15"),
                        LiveBuilderWallSettings.Thickness);

                LiveBuilderWallSettings.IsStoreyHigh = !LiveBuilderWindowControls.Switch(new GUIContent("Высота", "До потолка — этаж минус перекрытие. Своя — число ниже"),
                    !LiveBuilderWallSettings.IsStoreyHigh, "До потолка", "Своя");

                if (LiveBuilderWallSettings.IsStoreyHigh)
                    LiveBuilderWindowControls.Computed(" ", BoxModelSceneText.FormatMeters(LiveBuilderWallSettings.Height) + " м", "Этаж минус перекрытие");
                else
                    LiveBuilderWallSettings.CustomHeight = EditorGUILayout.FloatField(new GUIContent("Своя высота, м", "Высота стены над линией"),
                        LiveBuilderWallSettings.CustomHeight);

                LiveBuilderToolSettingsFields.DrawPartitionThickness();
            }

            if (LiveBuilderWallSettings.WallType == null)
                using (LiveBuilderWindowControls.Card("Материал"))
                    LiveBuilderWallSettings.Material = LiveBuilderWindowControls.MaterialField("Стена", "Пусто — серый материал набора", LiveBuilderWallSettings.Material);
        }
    }
}
