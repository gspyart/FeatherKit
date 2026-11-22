using System.Collections.Generic;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderIconRasterizer;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Select: клик выделяет что угодно — коробку, комнату, лестницу, стену, здание, проём; у выделенного — все его ручки.
    /// Ничего не создаёт. Esc в других инструментах, когда ничего не начато, ведёт сюда.
    /// </summary>
    public sealed class LiveBuilderSelectTool : LiveBuilderTool
    {
        private LiveBuilderToolModes modes;


        // ── Описание ─────────────────────────────────────────────────────

        public override string Id => "Select";

        public override string DisplayName => "Select";

        public override string Tooltip => "Клик — выделить что угодно: коробку, комнату, лестницу, стену, здание, проём; Shift — добавить или убрать. " +
                                          "У выделенного — все его ручки: грани, точки контура, повтор. Ничего не создаёт. Esc в других инструментах — сюда";

        public override int PaletteOrder => 0;

        // Стрелка курсора.
        public override List<Vector2[]> IconLines => new()
        {
            Points(6, 3, 6, 19, 10, 15.5f, 13, 21, 15.5f, 20, 12.5f, 14, 18, 14, 6, 3)
        };

        // Что выделяет клик: постройку целиком или кусок грани коробки.
        public override LiveBuilderToolModes Modes => modes ??= new LiveBuilderToolModes(
            () => (int)BoxModelSelectionModeSetting.Current, index => BoxModelSelectionModeSetting.Current = (BoxModelSelectionMode)index,
            new LiveBuilderToolModeLabel("Objects", LiveBuilderOptionIcon.Boxes, "Клик выделяет постройку целиком: коробку, стену, здание, проём"),
            new LiveBuilderToolModeLabel("Faces", LiveBuilderOptionIcon.Faces, "Клик выделяет видимый кусок грани коробки, двойной клик — грань целиком, Shift — несколько"));


        // ── Что правит ───────────────────────────────────────────────────

        public override bool CanEditFaces => true;

        public override bool CanEditRepeats => true;

        protected override bool CanEditOtherObjects => true;


        // ── Окно сцены ───────────────────────────────────────────────────

        public override bool DrawsSurfaceHover => true;

        // У выделенного контура — про его точки, в режиме Faces — про грани, у коробки — про выдавливание.
        public override string Hint => LiveBuilderContourHandles.IsShown ? LiveBuilderContourHandles.Hint
            : BoxModelSelectionModeSetting.IsSelectingFaces ? FaceSelectionHint
            : IsBoxSelected ? BoxModelPartExtrusion.Hint
            : SelectHint;

        private static string SelectHint => "Click — select anything " + LiveBuilderHintKeys.Shift + " add / remove " +
                                            (UnityEditor.Selection.activeGameObject != null ? LiveBuilderHintKeys.Esc + " deselect" : "");

        public override bool ShowsDepthClickHint => true;

        public override string[] KeyHints => new[]
        {
            LiveBuilderHintKeys.Key("click again") + " — deeper: the part under the mouse",
            LiveBuilderHintKeys.Shift + " + move handles — copy",
            LiveBuilderHintKeys.Key("Delete") + " — delete " + LiveBuilderHintKeys.Key("Shift+H") + " — isolate"
        };


        // ── Клавиша ──────────────────────────────────────────────────────

        [Shortcut(LiveBuilderShortcutIds.ToolsPrefix + "Select", typeof(LiveBuilderIdleShortcutContext), KeyCode.Alpha1)]
        private static void SelectByShortcut()
        {
            LiveBuilderToolPalette.SelectToolOrNextMode<LiveBuilderSelectTool>();
        }


        [Shortcut(LiveBuilderShortcutIds.ToolsPrefix + "Select" + LiveBuilderShortcutIds.PreviousModeSuffix, typeof(LiveBuilderIdleShortcutContext),
            KeyCode.Alpha1, ShortcutModifiers.Shift)]
        private static void SelectPreviousModeByShortcut()
        {
            LiveBuilderToolPalette.SelectToolOrPreviousMode<LiveBuilderSelectTool>();
        }


        // ── Что правит: проверка ─────────────────────────────────────────

        // Правит всё.
        protected override bool CanEditTarget(Component target)
        {
            return true;
        }
    }
}
