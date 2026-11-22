using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderIconRasterizer;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Wall: кликами точки — по ним встаёт стена по линии (<see cref="LiveBuilderPathGesture"/>). Правит только стены по линии.
    /// Мышь и клавиши ведут линию крючки основы; здесь — описание и настройки.
    /// </summary>
    public sealed class LiveBuilderWallTool : LiveBuilderTool
    {
        private readonly LiveBuilderPathGesture wallGesture = new(LiveBuilderPathKind.Wall);


        // ── Описание ─────────────────────────────────────────────────────

        public override string Id => "Wall";

        public override string DisplayName => "Wall";

        public override string Tooltip => "Кликами ставишь точки — по ним встаёт стена. Угол или плавно — переключатель под «⋯» или Shift на точке. Enter — готово";

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


        // ── Куда встанет новое ───────────────────────────────────────────

        public override string IntoTarget => LiveBuilderIntoTarget.OfStorey();


        // ── Окно сцены ───────────────────────────────────────────────────

        public override LiveBuilderPathGesture PathGesture => wallGesture;

        public override string Hint => PathHint(wallGesture);


        // ── Окно: редкие настройки ───────────────────────────────────────

        public override string MoreSettingsSummary => "line, thickness, partition, material";

        public override bool AreMoreSettingsChanged =>
            LiveBuilderWallSettings.IsSmooth || LiveBuilderWallSettings.Alignment != SplineWallAlignment.Center
            || (LiveBuilderWallSettings.WallType == null && !Mathf.Approximately(LiveBuilderWallSettings.Thickness, LiveBuilderWallSettings.FactoryThickness))
            || (LiveBuilderWallSettings.WallType == null && LiveBuilderWallSettings.Material != null)
            || LiveBuilderToolSettingsFields.IsPartitionChanged();


        // ── Клавиша ──────────────────────────────────────────────────────

        [Shortcut(LiveBuilderShortcutIds.ToolsPrefix + "Wall", typeof(LiveBuilderIdleShortcutContext), KeyCode.Alpha4)]
        private static void SelectByShortcut()
        {
            LiveBuilderToolPalette.SelectToolOrNextMode<LiveBuilderWallTool>();
        }


        [Shortcut(LiveBuilderShortcutIds.ToolsPrefix + "Wall" + LiveBuilderShortcutIds.PreviousModeSuffix, typeof(LiveBuilderIdleShortcutContext),
            KeyCode.Alpha4, ShortcutModifiers.Shift)]
        private static void SelectPreviousModeByShortcut()
        {
            LiveBuilderToolPalette.SelectToolOrPreviousMode<LiveBuilderWallTool>();
        }


        // ── Что правит: проверка ─────────────────────────────────────────

        // Только стены по линии.
        protected override bool CanEditTarget(Component target)
        {
            return target is SplineWall;
        }


        // ── Окно ─────────────────────────────────────────────────────────

        // Тип и высота новой стены — меняют каждый раз.
        public override void DrawSettings()
        {
            using (LiveBuilderWindowControls.Card("Wall"))
            {
                LiveBuilderWallSettings.WallType = LiveBuilderWallTypes.Popup(
                    new GUIContent("Wall Type", "Толщина и материал — из типа, общие для всех стен этого типа: поменял тип — перестроились все. Без типа — свои числа под «⋯»"),
                    LiveBuilderWallSettings.WallType);

                LiveBuilderWallSettings.IsStoreyHigh = !LiveBuilderWindowControls.Switch(new GUIContent("Height", "To Ceiling — этаж минус перекрытие. Own — число ниже"),
                    !LiveBuilderWallSettings.IsStoreyHigh, "To Ceiling", "Own");

                if (LiveBuilderWallSettings.IsStoreyHigh)
                    LiveBuilderWindowControls.Computed(" ", LiveBuilderSceneText.FormatMeters(LiveBuilderWallSettings.Height) + " m", "Этаж минус перекрытие");
                else
                    LiveBuilderWallSettings.CustomHeight = EditorGUILayout.FloatField(new GUIContent("Own Height, m", "Высота стены над линией"),
                        LiveBuilderWallSettings.CustomHeight);
            }
        }


        // Где стена от линии, угол или плавно, своя толщина без типа, перегородка в здании, материал без типа.
        public override void DrawMoreSettings()
        {
            using (LiveBuilderWindowControls.Card("Line"))
            {
                LiveBuilderToolSettingsFields.DrawKnotModeSwitch();

                LiveBuilderWallSettings.Alignment = (SplineWallAlignment)LiveBuilderWindowControls.Switch(
                    new GUIContent("Alignment", "Center — линия посередине стены, как On Axis у здания и комнаты. Left или Right — по краю: удобно обводить по краю пола"),
                    (int)LiveBuilderWallSettings.Alignment, "Center", "Left", "Right");
            }

            using (LiveBuilderWindowControls.Card("Size"))
            {
                if (LiveBuilderWallSettings.WallType == null)
                    LiveBuilderWallSettings.Thickness = EditorGUILayout.FloatField(new GUIContent("Thickness, m", "Своя толщина стены без типа. Наружная — 0,3, перегородка — 0,15"),
                        LiveBuilderWallSettings.Thickness);

                LiveBuilderToolSettingsFields.DrawPartitionThickness();
            }

            if (LiveBuilderWallSettings.WallType == null)
                using (LiveBuilderWindowControls.Card("Material"))
                    LiveBuilderWallSettings.Material = LiveBuilderWindowControls.MaterialField("Wall", "Пусто — серый материал набора", LiveBuilderWallSettings.Material);
        }
    }
}
