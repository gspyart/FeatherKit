using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderIconRasterizer;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Cut» (бывший «Проём»): клик по стене — проём выбранного размера (<see cref="LiveBuilderOpeningPlacer"/>), Shift + клик — ряд вдоль стороны,
    /// протянул по любой поверхности — свой размер (<see cref="LiveBuilderCutterGesture"/>). Клик по дыре готового — выделить его.
    /// Режимы — форма: Rect, Arch, Circle. Правит вырезы и вырезающие коробки.
    /// </summary>
    public sealed class LiveBuilderCutTool : LiveBuilderTool
    {
        // Где «низ над полом» действует, а где нет — рядом с числом.
        private const string SillNote = "Только в здании и его перегородках. В прочих стенах низ — где навёл (или по сетке), но не ниже пола";

        private readonly LiveBuilderOpeningPlacer openingPlacer = new();
        private readonly LiveBuilderCutterGesture cutterGesture = new();

        private LiveBuilderToolModes modes;

        // Луч нажатия: отпускание без протягивания — клик в том же месте.
        private Ray mouseDownRay;


        // ── Описание ─────────────────────────────────────────────────────

        public override string Id => "Cut";

        public override string DisplayName => "Cut";

        public override string Tooltip => "Клик по стене — проём готового размера насквозь, Shift + клик — ряд вдоль стороны. Протяни по любой поверхности, хоть по косой, — " +
                                          "свой размер: насквозь или ниша. Режимы — форма: прямоугольник, арка, круг. Клик по дыре — выделить проём";

        public override int PaletteOrder => 2;

        // Окно в стене: рама с переплётом.
        public override List<Vector2[]> IconLines => new()
        {
            Points(3, 4, 21, 4, 21, 20, 3, 20, 3, 4),
            Points(8, 8, 16, 8, 16, 16, 8, 16, 8, 8),
            Points(12, 8, 12, 16), Points(8, 12, 16, 12)
        };

        public override LiveBuilderToolModes Modes => modes ??= new LiveBuilderToolModes(
            () => (int)LiveBuilderCutterSettings.Shape, index => LiveBuilderCutterSettings.Shape = (LiveBuilderCutterShape)index,
            new LiveBuilderToolModeLabel("Rect", LiveBuilderOptionIcon.OpeningRectangle, "Прямоугольник — дверь, окно"),
            new LiveBuilderToolModeLabel("Arch", LiveBuilderOptionIcon.OpeningArch, "Арка — верх полукругом"),
            new LiveBuilderToolModeLabel("Circle", LiveBuilderOptionIcon.OpeningCircle, "Круг — круглое окно; не квадрат — овал"));


        // ── Что правит ───────────────────────────────────────────────────

        public override bool PlacesIntoBuilding => true;


        // ── Куда встанет новое ───────────────────────────────────────────

        public override string IntoTarget => LiveBuilderIntoTarget.OfWall(openingPlacer.TargetWall);


        // ── Окно сцены ───────────────────────────────────────────────────

        public override bool IsBusy => cutterGesture.IsActive;

        public override LiveBuilderDrawingReport DrawingReport => cutterGesture.Report();

        public override string Hint => cutterGesture.IsActive ? cutterGesture.Hint : openingPlacer.Hint;

        public override string[] KeyHints => new[]
        {
            LiveBuilderHintKeys.Shift + " + click a wall — a row along the side",
            LiveBuilderHintKeys.Key("click a hole") + " — select the cut",
            LiveBuilderHintKeys.Shift + " + move handles — a copy next to it"
        };


        // ── Окно: редкие настройки ───────────────────────────────────────

        public override string MoreSettingsSummary => "place, row, niche, hole sides";

        // Ниша вместо «насквозь» — уже изменено, глубину ниши отдельно не сверяем.
        public override bool AreMoreSettingsChanged =>
            LiveBuilderCutterSettings.IsSnappedToGrid || !LiveBuilderCutterSettings.IsThrough || LiveBuilderCutterSettings.RevealMaterial != null
            || !Mathf.Approximately(LiveBuilderCutterSettings.RowStep, LiveBuilderCutterSettings.FactoryRowStep)
            || !Mathf.Approximately(LiveBuilderCutterSettings.RowMargin, LiveBuilderCutterSettings.FactoryRowMargin);


        // ── Клавиша ──────────────────────────────────────────────────────

        [Shortcut(LiveBuilderShortcutIds.ToolsPrefix + "Cut", typeof(LiveBuilderIdleShortcutContext), KeyCode.Alpha3)]
        private static void SelectByShortcut()
        {
            LiveBuilderToolPalette.SelectToolOrNextMode<LiveBuilderCutTool>();
        }


        [Shortcut(LiveBuilderShortcutIds.ToolsPrefix + "Cut" + LiveBuilderShortcutIds.PreviousModeSuffix, typeof(LiveBuilderIdleShortcutContext),
            KeyCode.Alpha3, ShortcutModifiers.Shift)]
        private static void SelectPreviousModeByShortcut()
        {
            LiveBuilderToolPalette.SelectToolOrPreviousMode<LiveBuilderCutTool>();
        }


        // ── Что правит: проверка ─────────────────────────────────────────

        // Вырез — только GeometryCutter, в том числе вырез-коробка.
        protected override bool CanEditTarget(Component target)
        {
            return target is GeometryCutter;
        }


        // ── Окно ─────────────────────────────────────────────────────────

        // Готовый размер для клика — меняют каждый раз; форма — режим инструмента.
        public override void DrawSettings()
        {
            using (LiveBuilderWindowControls.Card("New Cut"))
                DrawPresetSize();
        }

        // Готовый размер списком «Window 1.5 × 1.5» или свой — для проёма кликом. Протянешь — размер свой, какой протянул.
        private static void DrawPresetSize()
        {
            var presets = LiveBuilderOpeningPresets.Presets;
            var names = new GUIContent[presets.Count + 1];

            for (var index = 0; index < presets.Count; index++)
                names[index] = new GUIContent(presets[index].DisplayName + "  " + LiveBuilderSceneText.FormatMeters(presets[index].Width) + " × " +
                                              LiveBuilderSceneText.FormatMeters(presets[index].Height));

            names[presets.Count] = new GUIContent("Custom");

            var selectedIndex = LiveBuilderOpeningPresets.SelectedIndex;
            var popupIndex = selectedIndex == LiveBuilderOpeningPresets.CustomIndex ? presets.Count : selectedIndex;
            var chosen = EditorGUILayout.Popup(new GUIContent("Click size", "Клик по стене ставит проём этого размера; низ — над полом этажа. " +
                                                                            "Протянешь — размер свой. " + SillNote), popupIndex, names);

            if (chosen != popupIndex)
                LiveBuilderOpeningPresets.SelectedIndex = chosen == presets.Count ? LiveBuilderOpeningPresets.CustomIndex : chosen;

            if (LiveBuilderOpeningPresets.SelectedIndex != LiveBuilderOpeningPresets.CustomIndex)
                return;

            LiveBuilderOpeningPresets.CustomWidth = EditorGUILayout.FloatField("Width, m", LiveBuilderOpeningPresets.CustomWidth);
            LiveBuilderOpeningPresets.CustomHeight = EditorGUILayout.FloatField("Height, m", LiveBuilderOpeningPresets.CustomHeight);
            LiveBuilderOpeningPresets.CustomSill = EditorGUILayout.FloatField(new GUIContent("Sill in a building, m", "0 — дверь, 1 — окно. " + SillNote),
                LiveBuilderOpeningPresets.CustomSill);
        }


        // Где встаёт проём кликом, ряд по Shift + клику, насквозь или ниша у протянутого, материал стенок дыры.
        public override void DrawMoreSettings()
        {
            using (LiveBuilderWindowControls.Card("Click"))
                LiveBuilderCutterSettings.IsSnappedToGrid = LiveBuilderWindowControls.Switch(
                    new GUIContent("Place", "At Cursor — середина проёма ровно под мышью. On Grid — низ и край проёма, ближний к краю стены, прилипают к сетке от этого края: проёмы встают ровными рядами. " +
                                            "В здании с этажами низ — всегда от пола его этажа, на Sill in a building"),
                    LiveBuilderCutterSettings.IsSnappedToGrid, ("At Cursor", LiveBuilderOptionIcon.AtCursor), ("On Grid", LiveBuilderOptionIcon.Grid));

            using (LiveBuilderWindowControls.Card("Row — Shift + Click"))
            {
                LiveBuilderCutterSettings.RowStep = EditorGUILayout.FloatField(
                    new GUIContent("Step, m", "Шаг проёмов не меньше этого: ряд растягивает его, чтобы лечь в сторону стены ровно"), LiveBuilderCutterSettings.RowStep);
                LiveBuilderCutterSettings.RowMargin = EditorGUILayout.FloatField(
                    new GUIContent("Margin, m", "Отступ ряда от углов стороны"), LiveBuilderCutterSettings.RowMargin);
            }

            using (LiveBuilderWindowControls.Card("Drag — Own Size"))
            {
                LiveBuilderCutterSettings.IsThrough = !LiveBuilderWindowControls.Switch(
                    new GUIContent("Cut", "Through — через всю толщину стены, пола или перекрытия, на котором рисуешь. Niche — на свою глубину от поверхности"),
                    !LiveBuilderCutterSettings.IsThrough, "Through", "Niche");

                if (!LiveBuilderCutterSettings.IsThrough)
                    LiveBuilderCutterSettings.NicheDepth = EditorGUILayout.FloatField(new GUIContent("Niche Depth, m", "От поверхности внутрь"),
                        LiveBuilderCutterSettings.NicheDepth);
            }

            using (LiveBuilderWindowControls.Card("Look"))
                LiveBuilderCutterSettings.RevealMaterial = LiveBuilderWindowControls.MaterialField("Hole Sides", "Пусто — материал того, что режем",
                    LiveBuilderCutterSettings.RevealMaterial);
        }


        // ── Окно сцены: мышь ─────────────────────────────────────────────

        // Где встанет проём готового размера — следит за мышью всегда.
        public override bool OnMouseMove(Ray worldRay, Event current)
        {
            openingPlacer.UpdateHover(LiveBuilderSceneHit.Find(worldRay));
            return true;
        }


        // Нажатие не забираем: клик это или протягивание, решит отпускание. Луч — для клика.
        public override bool OnMouseDown(Ray worldRay, Event current)
        {
            mouseDownRay = worldRay;
            return false;
        }


        // Протянул — проём своего размера по любой поверхности; просто клик — готовый размер, на отпускании.
        public override void OnMouseDrag(Ray downRay, Ray worldRay, bool canBeginGesture)
        {
            if (canBeginGesture && !cutterGesture.IsActive)
                cutterGesture.TryBegin(downRay);

            if (cutterGesture.IsActive)
                cutterGesture.Update(worldRay);
        }


        // Отпустил: протянутый проём готов; иначе клик по стене ставит проём готового размера, с Shift — ряд.
        public override bool OnMouseUp(Event current)
        {
            if (cutterGesture.IsActive)
                cutterGesture.Commit();
            else if (CanPlaceOpening())
                openingPlacer.Commit(current.shift);
            else
                return false;

            return true;
        }

        // Клик по стене ставит проём; по дыре готового проёма — выделяет его.
        private bool CanPlaceOpening()
        {
            var hit = LiveBuilderSceneHit.Find(mouseDownRay);

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
