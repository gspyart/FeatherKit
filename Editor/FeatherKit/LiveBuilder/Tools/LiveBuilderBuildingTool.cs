using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderIconRasterizer;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Здание»: кликами контур — по нему встаёт здание по контуру: стены на все этажи, перекрытия и крыша (<see cref="LiveBuilderPathGesture"/>).
    /// Правит только здания. Строит новое, а не внутри другого здания: в подсказке — этаж уровня.
    /// </summary>
    public sealed class LiveBuilderBuildingTool : LiveBuilderTool
    {
        private readonly LiveBuilderPathGesture buildingGesture = new(LiveBuilderPathKind.Building);


        // ── Описание ─────────────────────────────────────────────────────

        public override string Id => "Building";

        public override string DisplayName => "Здание";

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


        // ── Окно сцены ───────────────────────────────────────────────────

        public override LiveBuilderPathGesture PathGesture => buildingGesture;

        public override string Hint => PathHint(buildingGesture);


        // ── Окно: редкие настройки ───────────────────────────────────────

        public override string MoreSettingsSummary => "контур, толщины, пол и крыша, материалы";

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
            using (LiveBuilderWindowControls.Card("Здание"))
            {
                LiveBuilderBuildingSettings.StoreyCount = EditorGUILayout.IntSlider(new GUIContent("Этажей", "Перекрытия между этажами ставятся сами"),
                    LiveBuilderBuildingSettings.StoreyCount, 1, SplineBuilding.MaxStoreys);
                LiveBuilderWindowControls.Computed("Высота этажа", LiveBuilderSceneText.FormatMeters(LiveBuilderStoreys.StoreyHeight) + " м",
                    "Как у этажей уровня — на вкладке «Уровень»");

                LiveBuilderBuildingSettings.WallType = LiveBuilderWallTypes.Popup(
                    new GUIContent("Тип стен", "Толщина и материал снаружи — из типа, общие со всеми стенами этого типа. Без типа — свои числа в «Ещё»"),
                    LiveBuilderBuildingSettings.WallType);

                LiveBuilderBuildingSettings.HasParapet = EditorGUILayout.Toggle(
                    new GUIContent("Парапет", "Стены продолжаются над крышей бортиком по всему краю: за него цепляются, с крыши не скатиться"),
                    LiveBuilderBuildingSettings.HasParapet);

                if (LiveBuilderBuildingSettings.HasParapet)
                    LiveBuilderBuildingSettings.ParapetHeight = EditorGUILayout.FloatField(new GUIContent("Высота парапета, м", "По пояс — 1, низкий бортик — 0,4"),
                        LiveBuilderBuildingSettings.ParapetHeight);
            }

            LiveBuilderWindowControls.Explanation("Здание встанет на выбранный этаж: его пол — на полу этажа, этажи — те же, что в окне.");
        }


        // Что такое контур, угол или плавно, толщины, пол и крыша, материалы.
        public override void DrawMoreSettings()
        {
            using (LiveBuilderWindowControls.Card("Контур"))
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
            using (LiveBuilderWindowControls.Card("Стены и перекрытия"))
            {
                if (LiveBuilderBuildingSettings.WallType == null)
                    LiveBuilderBuildingSettings.WallThickness = EditorGUILayout.FloatField(new GUIContent("Толщина стен, м", "Своя толщина стен без типа. Наружная — 0,3"),
                        LiveBuilderBuildingSettings.WallThickness);

                LiveBuilderBuildingSettings.SlabThickness = EditorGUILayout.FloatField(new GUIContent("Толщина перекрытий, м", "Пол, перекрытия и крыша"),
                    LiveBuilderBuildingSettings.SlabThickness);

                using (new EditorGUILayout.HorizontalScope())
                {
                    LiveBuilderWindowControls.PrefixLabel(new GUIContent("Есть", "Нажата — есть, бледная — нет"));
                    LiveBuilderBuildingSettings.HasFloor = LiveBuilderWindowControls.IconToggle(LiveBuilderBuildingSettings.HasFloor, LiveBuilderOptionIcon.Floor,
                        "Пол", "Плита пола первого этажа");
                    LiveBuilderBuildingSettings.HasRoof = LiveBuilderWindowControls.IconToggle(LiveBuilderBuildingSettings.HasRoof, LiveBuilderOptionIcon.Roof,
                        "Крыша", "Плоская крыша сверху. Нет — сверху видно этажи");
                }
            }
        }

        // Материалы стен снаружи и изнутри, пола, потолка и крыши.
        private static void DrawMaterials()
        {
            using (LiveBuilderWindowControls.Card("Материалы"))
            {
                if (LiveBuilderBuildingSettings.WallType == null)
                    LiveBuilderBuildingSettings.OuterWallMaterial = LiveBuilderWindowControls.MaterialField("Стены снаружи", "Пусто — серый материал набора",
                        LiveBuilderBuildingSettings.OuterWallMaterial);

                LiveBuilderBuildingSettings.InnerWallMaterial = LiveBuilderWindowControls.MaterialField("Стены изнутри", "Пусто — как снаружи", LiveBuilderBuildingSettings.InnerWallMaterial);
                LiveBuilderBuildingSettings.FloorMaterial = LiveBuilderWindowControls.MaterialField("Пол", "Пусто — как стены снаружи", LiveBuilderBuildingSettings.FloorMaterial);
                LiveBuilderBuildingSettings.CeilingMaterial = LiveBuilderWindowControls.MaterialField("Потолок", "Пусто — как стены снаружи", LiveBuilderBuildingSettings.CeilingMaterial);
                LiveBuilderBuildingSettings.RoofMaterial = LiveBuilderWindowControls.MaterialField("Крыша", "Пусто — как стены снаружи", LiveBuilderBuildingSettings.RoofMaterial);
            }
        }


        // ── Окно сцены: подсказка ────────────────────────────────────────

        // Здание строится новым, не внутри другого — в подсказке этаж уровня.
        public override string BuildingStoreyHint(LiveBuilderBuildingContext context)
        {
            return null;
        }
    }
}
