using System.Collections.Generic;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderIconRasterizer;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Линейка»: клик — начало, клик — конец: расстояние и допрыгнет ли игрок (<see cref="LiveBuilderRuler"/>).
    /// Ничего не создаёт и не правит; контур выделенного при ней не виден.
    /// </summary>
    public sealed class LiveBuilderRulerTool : LiveBuilderTool
    {
        private readonly LiveBuilderRuler ruler = new();


        // ── Описание ─────────────────────────────────────────────────────

        public override string Id => "Ruler";

        public override string DisplayName => "Линейка";

        public override string Tooltip => "Клик — начало, клик — конец: расстояние и допрыгнет ли игрок";

        public override int PaletteOrder => 7;

        // Линейка наискось с делениями.
        public override List<Vector2[]> IconLines
        {
            get
            {
                var lines = new List<Vector2[]> { Points(3, 16, 16, 3, 21, 8, 8, 21, 3, 16) };
                var edgeStart = new Vector2(3, 16);
                var edgeEnd = new Vector2(16, 3);
                var inward = new Vector2(1f, 1f).normalized;

                for (var tick = 1; tick <= 5; tick++)
                {
                    var point = Vector2.Lerp(edgeStart, edgeEnd, tick / 6f);
                    lines.Add(new[] { point, point + inward * (tick % 2 == 0 ? 3.5f : 2f) });
                }

                return lines;
            }
        }


        // ── Что правит ───────────────────────────────────────────────────

        public override bool ShowsContour => false;


        // ── Окно сцены ───────────────────────────────────────────────────

        public override bool IsBusy => ruler.IsActive;

        public override string Hint => ruler.Hint;


        // ── Что правит ───────────────────────────────────────────────────

        protected override bool CanEditTarget(Component target)
        {
            return false;
        }


        // ── Окно ─────────────────────────────────────────────────────────

        public override void DrawSettings()
        {
            using (LiveBuilderWindowControls.Card("Как читать"))
                LiveBuilderWindowControls.Note("Зелёным — легко, жёлтым — на пределе или только в вис, красным — не допрыгнуть. " +
                                               "Таблица прыжков — в Project Settings → FeatherKit → Live Builder; там же она включается. " +
                                               "Цифры грубые, на глаз: точный расчёт — у настроек прыжка игры.");
        }


        // ── Окно сцены ───────────────────────────────────────────────────

        public override bool OnMouseMove(Ray worldRay, Event current)
        {
            ruler.UpdateHover(worldRay);
            return true;
        }


        public override bool OnMouseDown(Ray worldRay, Event current)
        {
            ruler.Click(worldRay);
            return true;
        }


        public override void Cancel()
        {
            ruler.Cancel();
        }


        public override void DrawPreview()
        {
            ruler.DrawPreview();
        }
    }
}
