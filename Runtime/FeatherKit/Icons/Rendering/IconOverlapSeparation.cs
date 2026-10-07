using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Icons
{
    /// <summary>
    /// Часть <see cref="IconsRenderer"/>: разводит иконки, которые просили не наезжать на соседей.
    /// - считает от честных мест, а не от вчерашних сдвигов;
    /// - наехавшая пара расходится на половину перекрытия по оси, где оно меньше;
    /// - к новому месту иконка едет плавно: рывки у соседних целей читаются как дрожь.
    /// Настройки — отступ и плавность — лежат у владельца.
    /// </summary>
    internal class IconOverlapSeparation
    {
        // Один проход разводит пару, три справляются с кучей; дальше выигрыш не виден.
        private const int Passes = 3;

        private readonly IconsRenderer owner;

        // Полями: разводим каждый кадр, и новые списки были бы мусором на ровном месте.
        private readonly List<IconRenderInfo> crowd = new List<IconRenderInfo>();
        private readonly List<Vector2> shifts = new List<Vector2>();


        public IconOverlapSeparation(IconsRenderer owner)
        {
            this.owner = owner;
        }


        // ── Расталкивание ─────────────────────────────────────────────────

        /// <summary>Развести наехавшие иконки и поставить каждую на её новое место.</summary>
        public void Separate(Dictionary<object, IconRenderInfo>.ValueCollection icons, float delta)
        {
            CollectCrowd(icons);

            if (crowd.Count > 1)
            {
                for (var pass = 0; pass < Passes; pass++)
                    PushApart();
            }

            var smoothing = owner.SeparationSmoothing;

            for (var i = 0; i < crowd.Count; i++)
            {
                var icon = crowd[i];

                icon.Push = smoothing > 0f
                    ? Vector2.Lerp(icon.Push, shifts[i], Mathf.Min(1f, delta / smoothing))
                    : shifts[i];

                icon.ApplyPosition();
            }
        }

        private void CollectCrowd(Dictionary<object, IconRenderInfo>.ValueCollection icons)
        {
            crowd.Clear();
            shifts.Clear();

            foreach (var icon in icons)
            {
                if (!icon.AvoidOverlap || !icon.IsVisible)
                    continue;

                crowd.Add(icon);
                shifts.Add(Vector2.zero);
            }
        }

        // По меньшей оси — так иконки расходятся кратчайшим путём, и куча не тянется в линию.
        private void PushApart()
        {
            var padding = Vector2.one * owner.OverlapPadding;

            for (var i = 0; i < crowd.Count; i++)
            {
                for (var j = i + 1; j < crowd.Count; j++)
                {
                    var first = crowd[i].Anchored + shifts[i];
                    var second = crowd[j].Anchored + shifts[j];

                    var gap = (crowd[i].Instance.rect.size + crowd[j].Instance.rect.size) * 0.5f + padding;
                    var offset = second - first;

                    var overlapX = gap.x - Mathf.Abs(offset.x);
                    var overlapY = gap.y - Mathf.Abs(offset.y);

                    if (overlapX <= 0f || overlapY <= 0f)
                        continue;

                    var push = overlapX < overlapY
                        ? new Vector2(overlapX * 0.5f * SignOf(offset.x, i - j), 0f)
                        : new Vector2(0f, overlapY * 0.5f * SignOf(offset.y, i - j));

                    shifts[i] -= push;
                    shifts[j] += push;
                }
            }
        }

        // Сошедшиеся ровно в точку разводим по порядку в списке, иначе они стояли бы друг в друге.
        private static float SignOf(float offset, int order)
        {
            if (!Mathf.Approximately(offset, 0f))
                return Mathf.Sign(offset);

            return order < 0 ? 1f : -1f;
        }
    }
}
