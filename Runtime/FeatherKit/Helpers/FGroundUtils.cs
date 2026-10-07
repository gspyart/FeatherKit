using System.Collections.Generic;
using FeatherKit.Entities;
using UnityEngine;

namespace FeatherKit.Helpers
{
    /// <summary>
    /// Земля под объектом: где она, насколько высоко над ней дно модели и как на неё встать.
    ///
    /// Луч бьёт сверху вниз, а не из объекта вниз, — так находится опора и НАД ним:
    /// провалившееся под пол поднимается обратно, а не остаётся там навсегда.
    ///
    /// Опорой считается только поверхность, глядящая вверх, и ближайшая по высоте:
    /// на изнанке потолка не лежат, а этажом выше объекту делать нечего.
    ///
    /// Поиск и замер — обычные методы: ни точка, ни список рендереров ничего не ищут,
    /// ищет физика. А вот <see cref="PlaceOnGround"/> оставлен расширением: там приёмник
    /// честный — трансформ и есть тот, кого ставят.
    /// </summary>
    public static class FGroundUtils
    {
        /// <summary>Насколько далеко искать опору вверх и вниз по умолчанию, м.</summary>
        public const float DefaultSearch = 3f;

        // Буфер один на всех: ищут по очереди, в одном кадре, и держать по массиву
        // на каждый объект незачем.
        private static readonly RaycastHit[] Hits = new RaycastHit[8];


        // ── Поиск опоры ───────────────────────────────────────────────────

        /// <summary>
        /// Где под точкой <paramref name="from"/> земля.
        ///
        /// <paramref name="search"/> — насколько далеко смотреть вверх и вниз, м.
        /// <paramref name="ignore"/> — чьи коллайдеры опорой не считать; обычно сам объект,
        /// иначе он находит опорой себя: маска земли в настройках сплошь и рядом
        /// «всё подряд».
        ///
        /// Не нашли — точка остаётся исходной, и false об этом честно скажет.
        /// </summary>
        public static bool TryFindGround(Vector3 from, LayerMask mask, float search,
            out Vector3 point, Transform ignore = null)
        {
            point = from;

            var count = Physics.RaycastNonAlloc(from + Vector3.up * search, Vector3.down, Hits,
                search * 2f, mask, QueryTriggerInteraction.Ignore);

            var gap = float.PositiveInfinity;

            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];

                if (hit.collider == null)
                    continue;

                // Потолок и своё же тело опорой не считаем.
                if (hit.normal.y <= 0.1f || (ignore != null && hit.collider.transform.IsChildOf(ignore)))
                    continue;

                var distance = Mathf.Abs(hit.point.y - from.y);

                if (distance >= gap)
                    continue;

                gap = distance;
                point = hit.point;
            }

            return !float.IsPositiveInfinity(gap);
        }


        // ── Дно модели ────────────────────────────────────────────────────

        /// <summary>
        /// Насколько дно видимой модели ниже точки объекта — ровно тот подъём, с которым
        /// объект встаёт на землю дном.
        ///
        /// По рендерерам, а не по коллайдерам: коллайдер бывает заметно больше модели,
        /// и объект повис бы над полом.
        ///
        /// Эффекты при этом не в счёт — см. <see cref="IsModel"/>.
        /// </summary>
        public static float BottomOffset(IReadOnlyList<Renderer> renderers, Vector3 origin)
        {
            if (renderers == null)
                return 0f;

            var bottom = float.PositiveInfinity;

            for (var i = 0; i < renderers.Count; i++)
            {
                if (IsModel(renderers[i]))
                    bottom = Mathf.Min(bottom, renderers[i].bounds.min.y);
            }

            // Начало координат ниже модели — поднимать нечего, иначе объект уедет вверх.
            return float.IsPositiveInfinity(bottom) ? 0f : Mathf.Max(0f, origin.y - bottom);
        }

        // Границы эффекта живут в МИРЕ, а не в объекте: пока частиц нет, они лежат в нуле
        // сцены, и дно модели уезжало бы туда же — объект поднимало на всю высоту рельефа.
        private static bool IsModel(Renderer renderer)
        {
            return renderer != null
                   && !(renderer is ParticleSystemRenderer)
                   && !(renderer is TrailRenderer)
                   && !(renderer is LineRenderer);
        }


        // ── Посадка ───────────────────────────────────────────────────────

        /// <summary>
        /// Опустить объект на землю под ним.
        ///
        /// <paramref name="lift"/> — на сколько поднять над точкой опоры. Обычно это
        /// расстояние от начала координат до дна модели: без него объект встаёт на землю
        /// серединой и уходит в пол наполовину.
        ///
        /// Не нашли опоры — объект остаётся там, где был.
        /// </summary>
        public static bool PlaceOnGround(this Transform transform, LayerMask mask, float search = DefaultSearch, float lift = 0f)
        {
            if (transform == null)
                return false;

            var position = transform.position;

            if (!TryFindGround(position, mask, search, out var point, transform))
                return false;

            // Через Teleport, а не прямой записью: на землю садятся и предметы с физикой,
            // а им поза ставится через тело.
            transform.Teleport(new Vector3(position.x, point.y + lift, position.z));

            return true;
        }

        /// <summary>
        /// То же для сущности, но ДНОМ модели, а не точкой объекта: начало координат
        /// у половины префабов в середине, и точкой опоры они уходят в пол наполовину.
        /// Где у неё дно, сущность знает сама — по своим рендерерам.
        ///
        /// Ради этого перегрузка и нужна: иначе подъём считал бы каждый вызывающий,
        /// и однажды кто-нибудь забыл бы.
        /// </summary>
        public static bool PlaceOnGround(this FEntityBase entity, LayerMask mask, float search = DefaultSearch)
        {
            if (entity == null)
                return false;

            var body = entity.transform;

            return body.PlaceOnGround(mask, search, BottomOffset(entity.Renderers, body.position));
        }
    }
}
