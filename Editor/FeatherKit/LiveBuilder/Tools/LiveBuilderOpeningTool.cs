using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderIconRasterizer;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Проём»: клик по стене — проём выбранного в окне размера (<see cref="LiveBuilderOpeningPlacer"/>),
    /// протянул по любой поверхности — свой размер (<see cref="LiveBuilderCutterGesture"/>). Клик по дыре готового — выделить его.
    /// Правит вырезы и вырезающие коробки.
    /// </summary>
    public sealed class LiveBuilderOpeningTool : LiveBuilderTool
    {
        // Где «низ над полом» действует, а где нет — рядом с числом.
        private const string SillNote = "Только в здании и его перегородках. В прочих стенах низ — где навёл (или по сетке), но не ниже пола";

        private readonly LiveBuilderOpeningPlacer openingPlacer = new();
        private readonly LiveBuilderCutterGesture cutterGesture = new();


        // ── Описание ─────────────────────────────────────────────────────

        public override string Id => "Opening";

        public override string DisplayName => "Проём";

        public override string Tooltip => "Клик по стене — проём выбранного в окне размера насквозь. Протяни по любой поверхности, хоть по косой, — свой размер: " +
                                          "насквозь или ниша. Форма — прямоугольник, арка, круг. Клик по дыре — выделить проём";

        public override int PaletteOrder => 6;

        // Окно в стене: рама с переплётом.
        public override List<Vector2[]> IconLines => new()
        {
            Points(3, 4, 21, 4, 21, 20, 3, 20, 3, 4),
            Points(8, 8, 16, 8, 16, 16, 8, 16, 8, 8),
            Points(12, 8, 12, 16), Points(8, 12, 16, 12)
        };


        // ── Что правит ───────────────────────────────────────────────────

        public override bool PlacesIntoBuilding => true;


        // ── Окно сцены ───────────────────────────────────────────────────

        public override bool IsBusy => cutterGesture.IsActive;

        public override string Hint => cutterGesture.IsActive ? cutterGesture.Hint : openingPlacer.Hint;


        // ── Окно: редкие настройки ───────────────────────────────────────

        public override string MoreSettingsSummary => "место, ниша, стенки дыры";

        // Ниша вместо «насквозь» — уже изменено, глубину ниши отдельно не сверяем.
        public override bool AreMoreSettingsChanged =>
            LiveBuilderCutterSettings.IsSnappedToGrid || !LiveBuilderCutterSettings.IsThrough || LiveBuilderCutterSettings.RevealMaterial != null;


        // ── Что правит: проверка ─────────────────────────────────────────

        // Вырезы и вырезающие коробки.
        protected override bool CanEditTarget(Component target)
        {
            return target is GeometryCutter || target is BoxModelPart { Mode: BoxModelPartMode.Cut };
        }


        // ── Окно ─────────────────────────────────────────────────────────

        // Форма — общая для клика и протягивания, и готовый размер для клика — меняют каждый раз.
        public override void DrawSettings()
        {
            using (LiveBuilderWindowControls.Card("Новый проём"))
            {
                LiveBuilderCutterSettings.Shape = (LiveBuilderCutterShape)LiveBuilderWindowControls.Switch(
                    new GUIContent("Форма", "Прямоугольник — дверь, окно. Арка — верх полукругом. Круг — круглое окно; не квадрат — овал. " +
                                            "Потом всё меняется у самого проёма: размер, скругление углов, форма"),
                    (int)LiveBuilderCutterSettings.Shape,
                    ("Прямоугольник", LiveBuilderOptionIcon.OpeningRectangle), ("Арка", LiveBuilderOptionIcon.OpeningArch), ("Круг", LiveBuilderOptionIcon.OpeningCircle));

                DrawPresetSize();
            }

            LiveBuilderWindowControls.Explanation("Клик по стене — проём этого размера. Протяни по любой поверхности, хоть по косой стене, — свой размер. " +
                                                  "Проём — отдельный объект: двигай и поворачивай как угодно, дыра едет за ним; в стене по линии и здании — ещё и за их точками. " +
                                                  "Режет коробки, стены и здания, но не свой префаб: дверь-префаб с проёмом внутри сама прорезает стену, куда её поставил.");
        }

        // Готовый размер или свой — для проёма кликом.
        private static void DrawPresetSize()
        {
            EditorGUILayout.LabelField(new GUIContent("Клик — готовый размер", "Клик по стене ставит проём этого размера. Протянешь — размер свой, какой протянул"),
                EditorStyles.miniBoldLabel);

            var presets = LiveBuilderOpeningPresets.Presets;
            var names = new string[presets.Count + 1];

            for (var index = 0; index < presets.Count; index++)
                names[index] = presets[index].DisplayName;

            names[presets.Count] = "Свой размер";

            var selectedIndex = LiveBuilderOpeningPresets.SelectedIndex;
            var gridIndex = selectedIndex == LiveBuilderOpeningPresets.CustomIndex ? presets.Count : selectedIndex;
            var chosen = GUILayout.SelectionGrid(gridIndex, names, 2, EditorStyles.miniButton);

            if (chosen != gridIndex)
                LiveBuilderOpeningPresets.SelectedIndex = chosen == presets.Count ? LiveBuilderOpeningPresets.CustomIndex : chosen;

            EditorGUILayout.Space(2f);

            if (LiveBuilderOpeningPresets.SelectedIndex != LiveBuilderOpeningPresets.CustomIndex)
            {
                var size = LiveBuilderOpeningPresets.Selected;
                LiveBuilderWindowControls.Computed("Ширина × высота", LiveBuilderSceneText.FormatMeters(size.Width) + " × " + LiveBuilderSceneText.FormatMeters(size.Height) + " м");
                LiveBuilderWindowControls.Computed("Низ над полом в здании", LiveBuilderSceneText.FormatMeters(size.Sill) + " м");
                LiveBuilderWindowControls.Note(SillNote);
                return;
            }

            LiveBuilderOpeningPresets.CustomWidth = EditorGUILayout.FloatField("Ширина, м", LiveBuilderOpeningPresets.CustomWidth);
            LiveBuilderOpeningPresets.CustomHeight = EditorGUILayout.FloatField("Высота, м", LiveBuilderOpeningPresets.CustomHeight);
            LiveBuilderOpeningPresets.CustomSill = EditorGUILayout.FloatField(new GUIContent("Низ над полом в здании, м", "0 — дверь, 1 — окно. " + SillNote),
                LiveBuilderOpeningPresets.CustomSill);
        }


        // Где встаёт проём кликом, насквозь или ниша у протянутого, материал стенок дыры.
        public override void DrawMoreSettings()
        {
            using (LiveBuilderWindowControls.Card("Клик"))
                LiveBuilderCutterSettings.IsSnappedToGrid = LiveBuilderWindowControls.Switch(
                    new GUIContent("Место", "Где навёл — середина проёма ровно под мышью. По сетке — левый край и низ прилипают к сетке: проёмы встают ровными рядами. " +
                                            "В здании с этажами низ — всегда от пола его этажа, на «Низ над полом»"),
                    LiveBuilderCutterSettings.IsSnappedToGrid, ("Где навёл", LiveBuilderOptionIcon.AtCursor), ("По сетке", LiveBuilderOptionIcon.Grid));

            using (LiveBuilderWindowControls.Card("Протянуть — свой размер"))
            {
                LiveBuilderCutterSettings.IsThrough = !LiveBuilderWindowControls.Switch(
                    new GUIContent("Резать", "Насквозь — через всю толщину стены, пола или перекрытия, на котором рисуешь. Ниша — на свою глубину от поверхности"),
                    !LiveBuilderCutterSettings.IsThrough, "Насквозь", "Нишу");

                if (!LiveBuilderCutterSettings.IsThrough)
                    LiveBuilderCutterSettings.NicheDepth = EditorGUILayout.FloatField(new GUIContent("Глубина ниши, м", "От поверхности внутрь"),
                        LiveBuilderCutterSettings.NicheDepth);
            }

            using (LiveBuilderWindowControls.Card("Вид"))
                LiveBuilderCutterSettings.RevealMaterial = LiveBuilderWindowControls.MaterialField("Стенки дыры", "Пусто — материал того, что режем",
                    LiveBuilderCutterSettings.RevealMaterial);
        }


        // ── Окно сцены: мышь ─────────────────────────────────────────────

        // Где встанет проём готового размера — следит за мышью всегда.
        public override bool OnMouseMove(Ray worldRay, Event current)
        {
            openingPlacer.UpdateHover(LiveBuilderSceneHit.Find(worldRay));
            return true;
        }


        // Протянул — проём своего размера по любой поверхности; просто клик — готовый размер, на отпускании.
        public override void OnMouseDrag(Ray downRay, Ray worldRay, bool canBeginGesture)
        {
            if (canBeginGesture && !cutterGesture.IsActive)
                cutterGesture.TryBegin(downRay);

            if (cutterGesture.IsActive)
                cutterGesture.Update(worldRay);
        }


        // Отпустил: протянутый проём готов; иначе клик по стене ставит проём готового размера.
        public override bool OnMouseUp(Event current)
        {
            if (cutterGesture.IsActive)
                cutterGesture.Commit();
            else if (CanPlaceOpening(current))
                openingPlacer.Commit();
            else
                return false;

            return true;
        }

        // Клик по стене ставит проём; по дыре готового проёма — выделяет его.
        private bool CanPlaceOpening(Event current)
        {
            var hit = LiveBuilderSceneHit.Find(HandleUtility.GUIPointToWorldRay(current.mousePosition));

            if (hit.IsGeometryCutterInFront)
                return false;

            openingPlacer.UpdateHover(hit);
            return openingPlacer.HasTarget;
        }


        // ── Окно сцены: отмена и вид ─────────────────────────────────────

        // Бросить протянутый проём.
        public override void Cancel()
        {
            cutterGesture.Cancel();
        }


        // Протянутый проём или где встанет проём готового размера.
        public override void DrawPreview()
        {
            if (cutterGesture.IsActive)
                cutterGesture.DrawPreview();
            else
                openingPlacer.DrawPreview();
        }
    }
}
