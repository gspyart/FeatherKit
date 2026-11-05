using System.Collections.Generic;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderIconRasterizer;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Выделение»: клик выделяет что угодно — коробку, комнату, лестницу, стену, здание, проём; у выделенного — все его ручки.
    /// Ничего не создаёт. Esc в других инструментах, когда ничего не начато, ведёт сюда.
    /// </summary>
    public sealed class LiveBuilderSelectTool : LiveBuilderTool
    {
        private const string SelectHint = "Клик — выделить что угодно: коробку, комнату, лестницу, стену, здание, проём. Shift — добавить или убрать. " +
                                          "Тяни ручки выделенного. Esc — снять выделение";


        // ── Описание ─────────────────────────────────────────────────────

        public override string Id => "Select";

        public override string DisplayName => "Выделение";

        public override string Tooltip => "Клик — выделить что угодно: коробку, комнату, лестницу, стену, здание, проём; Shift — добавить или убрать. " +
                                          "У выделенного — все его ручки: грани, точки контура, повтор. Ничего не создаёт. Esc в других инструментах — сюда";

        public override int PaletteOrder => 0;

        // Стрелка курсора.
        public override List<Vector2[]> IconLines => new()
        {
            Points(6, 3, 6, 19, 10, 15.5f, 13, 21, 15.5f, 20, 12.5f, 14, 18, 14, 6, 3)
        };


        // ── Что правит ───────────────────────────────────────────────────

        public override bool CanEditFaces => true;

        public override bool CanEditRepeats => true;

        protected override bool CanEditOtherObjects => true;


        // ── Окно сцены ───────────────────────────────────────────────────

        public override bool DrawsSurfaceHover => true;

        // У выделенного контура — про его точки, в режиме «Грани» — про грани, у коробки — про выдавливание.
        public override string Hint => LiveBuilderContourHandles.IsShown ? LiveBuilderContourHandles.Hint
            : BoxModelSelectionModeSetting.IsSelectingFaces ? FaceSelectionHint
            : IsBoxSelected ? BoxModelPartExtrusion.Hint
            : SelectHint;

        public override bool ShowsDepthClickHint => true;


        // ── Что правит: проверка ─────────────────────────────────────────

        // Правит всё.
        protected override bool CanEditTarget(Component target)
        {
            return true;
        }


        // ── Окно ─────────────────────────────────────────────────────────

        // Что выделяет клик и пояснение.
        public override void DrawSettings()
        {
            using (LiveBuilderWindowControls.Card("Клик"))
                LiveBuilderToolSettingsFields.DrawSelectionModeSwitch();

            LiveBuilderWindowControls.Explanation("Select", "Клик выделяет что угодно: коробку, комнату, лестницу, стену, здание, проём; Shift — добавить или убрать. " +
                                           "У выделенного — все его ручки: грани коробок (с Ctrl — выдавить новую), точки стен и контуров, ручки проёмов, повтор. " +
                                           "Остальные инструменты создают своё и правят только своё. Esc в них — сюда.");
        }
    }
}
