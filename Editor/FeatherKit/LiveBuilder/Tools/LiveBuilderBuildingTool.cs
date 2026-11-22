using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderIconRasterizer;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Building: кликами контур — по нему встаёт здание по контуру: стены на все этажи, перекрытия и крыша (<see cref="LiveBuilderPathGesture"/>).
    /// Правит только здания. Строит новое на этаже уровня, а не внутри другого здания.
    /// </summary>
    public sealed class LiveBuilderBuildingTool : LiveBuilderTool
    {
        private readonly LiveBuilderPathGesture buildingGesture = new(LiveBuilderPathKind.Building);


        // ── Описание ─────────────────────────────────────────────────────

        public override string Id => "Building";

        public override string DisplayName => "Building";

        public override string Tooltip => "Кликами ставишь контур — по нему встаёт здание: стены на все этажи, перекрытия и крыша. " +
                                          "Угол или плавно — переключатель или Shift. Клик по первой точке или Enter — готово";

        public override int PaletteOrder => 4;

        // Дом с окнами и дверью.
        public override List<Vector2[]> IconLines
        {
            get
            {
                var lines = new List<Vector2[]> { Points(6, 21, 6, 3, 18, 3, 18, 21, 4, 21, 20, 21), Points(10.5f, 21, 10.5f, 17, 13.5f, 17, 13.5f, 21) };

                foreach (var y in new[] { 6.5f, 11.5f })
                    foreach (var x in new[] { 9f, 13f })
                        lines.Add(Points(x, y, x + 2, y, x + 2, y + 2.5f, x, y + 2.5f, x, y));

                return lines;
            }
        }


        // ── Куда встанет новое ───────────────────────────────────────────

        public override string IntoTarget => LiveBuilderIntoTarget.OfStorey();


        // ── Окно сцены ───────────────────────────────────────────────────

        public override LiveBuilderPathGesture PathGesture => buildingGesture;

        public override string Hint => PathHint(buildingGesture);


        // ── Окно: редкие настройки ───────────────────────────────────────

        public override string MoreSettingsSummary => "outline, thicknesses, floor and roof, materials";

        public override bool AreMoreSettingsChanged =>
            LiveBuilderWallSettings.IsSmooth || LiveBuilderBuildingSettings.ContourSide != SplineBuildingContourSide.Outside
            || (LiveBuilderBuildingSettings.WallType == null && !Mathf.Approximately(LiveBuilderBuildingSettings.WallThickness, LiveBuilderBuildingSettings.FactoryWallThickness))
            || !Mathf.Approximately(LiveBuilderBuildingSettings.SlabThickness, LiveBuilderBuildingSettings.FactorySlabThickness)
            || !LiveBuilderBuildingSettings.HasFloor || !LiveBuilderBuildingSettings.HasRoof
            || AreMaterialsChanged;

        // Материалы стен снаружи считаются, только пока нет типа: с типом поле спрятано.
        private static bool AreMaterialsChanged =>
            (LiveBuilderBuildingSettings.WallType == null && LiveBuilderBuildingSettings.OuterWallMaterial != null)
            || LiveBuilderBuildingSettings.InnerWallMaterial != null || LiveBuilderBuildingSettings.FloorMaterial != null
            || LiveBuilderBuildingSettings.CeilingMaterial != null || LiveBuilderBuildingSettings.RoofMaterial != null;


        // ── Клавиша ──────────────────────────────────────────────────────

        [Shortcut(LiveBuilderShortcutIds.ToolsPrefix + "Building", typeof(LiveBuilderIdleShortcutContext), KeyCode.Alpha5)]
        private static void SelectByShortcut()
        {
            LiveBuilderToolPalette.SelectToolOrNextMode<LiveBuilderBuildingTool>();
        }


        [Shortcut(LiveBuilderShortcutIds.ToolsPrefix + "Building" + LiveBuilderShortcutIds.PreviousModeSuffix, typeof(LiveBuilderIdleShortcutContext),
            KeyCode.Alpha5, ShortcutModifiers.Shift)]
        private static void SelectPreviousModeByShortcut()
        {
            LiveBuilderToolPalette.SelectToolOrPreviousMode<LiveBuilderBuildingTool>();
        }


        // ── Что правит: проверка ─────────────────────────────────────────

        // Только здания по контуру.
        protected override bool CanEditTarget(Component target)
        {
            return target is SplineBuilding;
        }


        // ── Окно ─────────────────────────────────────────────────────────

        // Сколько этажей и тип стен нового здания — меняют каждый раз.
        public override void DrawSettings()
        {
            using (LiveBuilderWindowControls.Card("Building"))
            {
                LiveBuilderBuildingSettings.StoreyCount = EditorGUILayout.IntSlider(
                    new GUIContent("Storeys", "Перекрытия между этажами ставятся сами. Здание встанет на выбранный этаж уровня: его пол — на полу этажа"),
                    LiveBuilderBuildingSettings.StoreyCount, 1, SplineBuilding.MaxStoreys);
                LiveBuilderWindowControls.Computed("Storey Height", LiveBuilderSceneText.FormatMeters(LiveBuilderStoreys.StoreyHeight) + " m",
                    "Как у этажей уровня — раздел Storeys окна Live Builder");

                LiveBuilderBuildingSettings.WallType = LiveBuilderWallTypes.Popup(
                    new GUIContent("Wall Type", "Толщина и материал снаружи — из типа, общие со всеми стенами этого типа. Без типа — свои числа под «⋯»"),
                    LiveBuilderBuildingSettings.WallType);

                LiveBuilderBuildingSettings.HasParapet = EditorGUILayout.Toggle(
                    new GUIContent("Parapet", "Стены продолжаются над крышей бортиком по всему краю: за него цепляются, с крыши не скатиться"),
                    LiveBuilderBuildingSettings.HasParapet);

                if (LiveBuilderBuildingSettings.HasParapet)
                    LiveBuilderBuildingSettings.ParapetHeight = EditorGUILayout.FloatField(new GUIContent("Parapet Height, m", "По пояс — 1, низкий бортик — 0,4"),
                        LiveBuilderBuildingSettings.ParapetHeight);
            }
        }


        // Что такое контур, угол или плавно, толщины, пол и крыша, материалы.
        public override void DrawMoreSettings()
        {
            using (LiveBuilderWindowControls.Card("Outline"))
            {
                LiveBuilderToolSettingsFields.DrawKnotModeSwitch();

                LiveBuilderBuildingSettings.ContourSide = (SplineBuildingContourSide)LiveBuilderToolSettingsFields.DrawLineSideSwitch((int)LiveBuilderBuildingSettings.ContourSide);
            }

            DrawWallsAndSlabs();
            DrawMaterials();
        }

        // Своя толщина стен без типа, толщина перекрытий, есть ли пол и крыша.
        private static void DrawWallsAndSlabs()
        {
            using (LiveBuilderWindowControls.Card("Walls And Slabs"))
            {
                if (LiveBuilderBuildingSettings.WallType == null)
                    LiveBuilderBuildingSettings.WallThickness = EditorGUILayout.FloatField(new GUIContent("Wall Thickness, m", "Своя толщина стен без типа. Наружная — 0,3"),
                        LiveBuilderBuildingSettings.WallThickness);

                LiveBuilderBuildingSettings.SlabThickness = EditorGUILayout.FloatField(new GUIContent("Slab Thickness, m", "Пол, перекрытия и крыша"),
                    LiveBuilderBuildingSettings.SlabThickness);

                using (new EditorGUILayout.HorizontalScope())
                {
                    LiveBuilderWindowControls.PrefixLabel(new GUIContent("Has", "Нажата — есть, бледная — нет"));
                    LiveBuilderBuildingSettings.HasFloor = LiveBuilderWindowControls.IconToggle(LiveBuilderBuildingSettings.HasFloor, LiveBuilderOptionIcon.Floor,
                        "Floor", "Плита пола первого этажа");
                    LiveBuilderBuildingSettings.HasRoof = LiveBuilderWindowControls.IconToggle(LiveBuilderBuildingSettings.HasRoof, LiveBuilderOptionIcon.Roof,
                        "Roof", "Плоская крыша сверху. Нет — сверху видно этажи");
                }
            }
        }

        // Материалы стен снаружи и изнутри, пола, потолка и крыши.
        private static void DrawMaterials()
        {
            using (LiveBuilderWindowControls.Card("Materials"))
            {
                if (LiveBuilderBuildingSettings.WallType == null)
                    LiveBuilderBuildingSettings.OuterWallMaterial = LiveBuilderWindowControls.MaterialField("Walls Outside", "Пусто — серый материал набора",
                        LiveBuilderBuildingSettings.OuterWallMaterial);

                LiveBuilderBuildingSettings.InnerWallMaterial = LiveBuilderWindowControls.MaterialField("Walls Inside", "Пусто — как снаружи", LiveBuilderBuildingSettings.InnerWallMaterial);
                LiveBuilderBuildingSettings.FloorMaterial = LiveBuilderWindowControls.MaterialField("Floor", "Пусто — как стены снаружи", LiveBuilderBuildingSettings.FloorMaterial);
                LiveBuilderBuildingSettings.CeilingMaterial = LiveBuilderWindowControls.MaterialField("Ceiling", "Пусто — как стены снаружи", LiveBuilderBuildingSettings.CeilingMaterial);
                LiveBuilderBuildingSettings.RoofMaterial = LiveBuilderWindowControls.MaterialField("Roof", "Пусто — как стены снаружи", LiveBuilderBuildingSettings.RoofMaterial);
            }
        }
    }
}
