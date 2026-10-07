using FeatherKit.Entities;
using UnityEngine;

namespace FeatherKit.Icons
{
    /// <summary>
    /// Скрипт на иконке, которому нужно знать, над кем она висит и для кого показана.
    /// - получает цель от трекера: сущность или голую точку мира;
    /// - отдаёт наблюдателя, от имени которого действуют кнопки иконки.
    /// Простой картинке он не нужен — она остаётся обычным UI-объектом.
    /// </summary>
    public abstract class IconView : MonoBehaviour
    {
        private IconRequest request;


        /// <summary>Над кем висим. Null — иконка висит над точкой мира.</summary>
        public FEntityBase Entity { get; private set; }

        /// <summary>Где висим в мире: центр сущности либо точка, если сущности нет.</summary>
        public Vector3 Point { get; private set; }

        /// <summary>
        /// Для кого показана. Читается из заявки, а не копией: бегунок меняет наблюдателя
        /// и у висящих иконок. Null — иконку повесили мимо правил.
        /// </summary>
        public FEntityBase Viewer => request != null ? request.Viewer : null;


        // ── Связь с целью ─────────────────────────────────────────────────

        /// <summary>Зовёт трекер, когда иконке достался новый объект на экране.</summary>
        internal void Bind(TrackedIcon icon)
        {
            request = icon.Request;
            Entity = icon.HasEntity ? icon.Entity : null;
            Point = icon.HasEntity ? icon.Entity.Center : icon.Point;

            OnBound();
        }

        /// <summary>Цель известна — можно достать её данные и настроить вид.</summary>
        protected virtual void OnBound() { }
    }
}
