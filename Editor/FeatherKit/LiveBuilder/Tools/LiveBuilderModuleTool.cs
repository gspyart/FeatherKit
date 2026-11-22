using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderIconRasterizer;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Модуль»: ставит готовые префабы (окно, дверь, балкон) из палитры уровня на стены здания и стены по линии (<see cref="LiveBuilderModulePlacer"/>).
    /// Клик — один модуль, Shift + клик — ряд вдоль стороны стены. Своих построек не правит.
    /// </summary>
    public sealed class LiveBuilderModuleTool : LiveBuilderTool
    {
        private readonly LiveBuilderModulePlacer modulePlacer = new();


        // ── Описание ─────────────────────────────────────────────────────

        public override string Id => "Module";

        public override string DisplayName => "Module";

        public override string Tooltip => "Клик по стене — префаб из палитры уровня встаёт на стену: по нормали, низ — над полом этажа, край — по сетке. " +
                                          "Shift + клик — ряд таких во всю сторону стены";

        public override int PaletteOrder => 6;

        // Стена с окном и дверью.
        public override List<Vector2[]> IconLines => new()
        {
            Points(2, 4, 22, 4, 22, 21, 2, 21, 2, 4),
            Points(5, 8, 10, 8, 10, 13, 5, 13, 5, 8),
            Points(14, 21, 14, 9, 19, 9, 19, 21)
        };


        // ── Что правит ───────────────────────────────────────────────────

        public override bool PlacesIntoBuilding => true;


        // ── Куда встанет новое ───────────────────────────────────────────

        public override string IntoTarget => LiveBuilderIntoTarget.OfStorey();


        // ── Окно сцены ───────────────────────────────────────────────────

        public override string Hint => LiveBuilderModuleSettings.Prefab == null
            ? "Pick a prefab in the Level Palette or in the Module card"
            : modulePlacer.Hint;

        public override string[] KeyHints => new[]
        {
            "Click a wall — +Z outward, bottom above the storey floor",
            LiveBuilderHintKeys.Shift + " + click — a row along the whole side"
        };


        // ── Клавиша ──────────────────────────────────────────────────────

        [Shortcut(LiveBuilderShortcutIds.ToolsPrefix + "Module", typeof(LiveBuilderIdleShortcutContext), KeyCode.Alpha7)]
        private static void SelectByShortcut()
        {
            LiveBuilderToolPalette.SelectToolOrNextMode<LiveBuilderModuleTool>();
        }


        [Shortcut(LiveBuilderShortcutIds.ToolsPrefix + "Module" + LiveBuilderShortcutIds.PreviousModeSuffix, typeof(LiveBuilderIdleShortcutContext),
            KeyCode.Alpha7, ShortcutModifiers.Shift)]
        private static void SelectPreviousModeByShortcut()
        {
            LiveBuilderToolPalette.SelectToolOrPreviousMode<LiveBuilderModuleTool>();
        }


        // ── Что правит: проверка ─────────────────────────────────────────

        // Модули — обычные префабы, их правят инструменты Unity.
        protected override bool CanEditTarget(Component target)
        {
            return false;
        }


        // ── Окно ─────────────────────────────────────────────────────────

        public override void DrawSettings()
        {
            using (LiveBuilderWindowControls.Card("Module"))
            {
                LiveBuilderModuleSettings.Prefab = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Prefab", "Что ставить. Клик по префабу в палитре уровня выбирает его здесь же"),
                    LiveBuilderModuleSettings.Prefab, typeof(GameObject), false);
            }

            using (LiveBuilderWindowControls.Card("Row — Shift + Click"))
            {
                LiveBuilderModuleSettings.RowStep = EditorGUILayout.FloatField(
                    new GUIContent("Step, m", "Шаг модулей не меньше этого: ряд растягивает его, чтобы лечь в сторону ровно"), LiveBuilderModuleSettings.RowStep);
                LiveBuilderModuleSettings.RowMargin = EditorGUILayout.FloatField(
                    new GUIContent("Margin, m", "Отступ ряда от углов стороны"), LiveBuilderModuleSettings.RowMargin);
            }
        }


        // ── Окно сцены: мышь ─────────────────────────────────────────────

        public override bool OnMouseMove(Ray worldRay, Event current)
        {
            modulePlacer.UpdateHover(LiveBuilderSceneHit.Find(worldRay), LiveBuilderModuleSettings.Prefab);
            return true;
        }


        // Клик по стене ставит сразу, на нажатии; мимо стены — окно сцены выделит своё под мышью.
        public override bool OnMouseDown(Ray worldRay, Event current)
        {
            var prefab = LiveBuilderModuleSettings.Prefab;
            modulePlacer.UpdateHover(LiveBuilderSceneHit.Find(worldRay), prefab);
            return modulePlacer.Commit(prefab, current.shift) != null;
        }


        // ── Окно сцены: вид ──────────────────────────────────────────────

        public override void DrawPreview()
        {
            modulePlacer.DrawPreview();
        }
    }
}
