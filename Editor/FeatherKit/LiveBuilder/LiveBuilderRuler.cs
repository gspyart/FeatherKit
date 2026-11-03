using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инструмент «Линейка»: клик — начало, клик — конец. Видно расстояние по горизонтали и по высоте
    /// и допрыгнет ли игрок (<see cref="LiveBuilderJumpCheck"/>, если проверка включена в настройках проекта): зелёным — легко, жёлтым — на пределе, красным — нет.
    /// Точки — на поверхностях под мышью, по сетке; в пустоте — на полу этажа. Следующий клик — новое измерение, Esc — убрать.
    /// </summary>
    public sealed class LiveBuilderRuler
    {
        private static readonly Color EasyColor = new(0.35f, 0.9f, 0.4f);
        private static readonly Color HardColor = new(1f, 0.85f, 0.2f);
        private static readonly Color ImpossibleColor = new(1f, 0.35f, 0.3f);

        private Vector3? start;
        private Vector3? end;
        private Vector3? hovered;


        public bool IsActive => start != null;

        public string Hint => start == null || end != null
            ? "Клик — откуда прыжок"
            : "Клик — куда прыжок. Esc — убрать";


        // ── Мышь ─────────────────────────────────────────────────────────

        public void UpdateHover(Ray worldRay)
        {
            hovered = TryGetPoint(worldRay, out var point) ? point : null;
        }


        public void Click(Ray worldRay)
        {
            if (!TryGetPoint(worldRay, out var point))
                return;

            if (start == null || end != null)
            {
                start = point;
                end = null;
                return;
            }

            end = point;
        }


        public void Cancel()
        {
            start = null;
            end = null;
        }


        // Точка под мышью: на объекте из коробок или любой видимой геометрии, а в пустоте — на полу этажа. По сетке мира.
        private static bool TryGetPoint(Ray worldRay, out Vector3 point)
        {
            if (BoxModelPicker.TryPickSurface(worldRay, out var surfaceHit))
            {
                point = BoxModelGrid.Snap(surfaceHit.Model.transform.TransformPoint(surfaceHit.LocalPoint));
                return true;
            }

            if (BoxModelPicker.TryRaycastVisible(worldRay, float.MaxValue, out var hit))
            {
                point = BoxModelGrid.Snap(hit.point);
                return true;
            }

            var floor = BoxModelDrawPlaneFinder.StoreyFloorPlane(null);

            if (floor.TryIntersect(worldRay, out point))
            {
                point = floor.SnapOnPlane(point);
                return true;
            }

            return false;
        }


        // ── Рамка ────────────────────────────────────────────────────────

        // Отрезок, катеты по горизонтали и высоте, надпись с вердиктом.
        public void DrawPreview()
        {
            var from = start ?? hovered;
            var to = end ?? (start != null ? hovered : null);

            if (from is not { } a)
                return;

            Handles.color = Color.white;
            Handles.DrawWireDisc(a, Vector3.up, HandleUtility.GetHandleSize(a) * 0.06f);

            if (to is not { } b)
                return;

            var horizontal = new Vector2(b.x - a.x, b.z - a.z).magnitude;
            var rise = b.y - a.y;
            var verdict = LiveBuilderJumpCheck.Judge(horizontal, rise, out var verdictText);
            var color = verdict switch
            {
                LiveBuilderJumpVerdict.Easy => EasyColor,
                LiveBuilderJumpVerdict.Hard => HardColor,
                LiveBuilderJumpVerdict.Impossible => ImpossibleColor,
                _ => Color.white
            };

            var corner = new Vector3(b.x, a.y, b.z);
            Handles.color = new Color(color.r, color.g, color.b, 0.5f);
            Handles.DrawDottedLine(a, corner, 4f);
            Handles.DrawDottedLine(corner, b, 4f);
            Handles.color = color;
            Handles.DrawLine(a, b, 3f);
            Handles.DrawWireDisc(b, Vector3.up, HandleUtility.GetHandleSize(b) * 0.06f);

            var text = "по горизонтали " + BoxModelSceneText.FormatMeters(horizontal) + " м, по высоте " + (rise < 0f ? "−" : "+") +
                       BoxModelSceneText.FormatMeters(rise) + " м" + (verdictText.Length > 0 ? " — " + verdictText : "");
            BoxModelSceneText.DrawLabel((a + b) / 2f, text);
        }
    }
}
