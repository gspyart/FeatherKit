using FeatherKit.Entities;
using UnityEngine;

namespace FeatherKit.Icons
{
    /// <summary>
    /// Живая иконка: за кем висит, по какой заявке и что сейчас на экране; по ней заказчик её убирает.
    /// - держится за сущность или за точку мира — у метки эвакуации объекта нет;
    /// - объект на экране хранит сама: у края иконка подменяется, и ссылка заказчика умерла бы.
    /// </summary>
    public class TrackedIcon
    {
        internal TrackedIcon(FEntityBase entity, Vector3 point, IconRequest setup)
        {
            Entity = entity;
            Point = point;
            Request = setup;

            HasEntity = entity != null;
        }


        /// <summary>Над кем висим. Null — иконка привязана к точке.</summary>
        public FEntityBase Entity { get; }

        /// <summary>Где висим, если сущности нет.</summary>
        public Vector3 Point { get; }

        /// <summary>Заводилась ли иконка над сущностью. Отличает «нет сущности» от «она исчезла».</summary>
        public bool HasEntity { get; }

        /// <summary>Объект иконки, пока она на экране. Через него добираются до кнопки внутри.</summary>
        public RectTransform View => Instance;

        public bool IsVisible => Instance != null;

        /// <summary>Снята ли навсегда. Менеджер уберёт её на ближайшем кадре.</summary>
        public bool IsReleased { get; private set; }


        /// <summary>
        /// Заявка, по которой иконку показали. Отдаётся наружу, чтобы заказчик мог сменить
        /// вид уже показанной иконки, а не заводить вторую.
        /// </summary>
        public IconRequest Request { get; }

        internal RectTransform Instance { get; set; }


        /// <summary>Куда смотреть в этом кадре. Center, а не позиция: у персонажа та в ногах.</summary>
        internal Vector3 WorldPoint => HasEntity ? Entity.Center : Point;

        /// <summary>Цель пропала — сущность была, а теперь её нет.</summary>
        internal bool IsLost => HasEntity && Entity == null;


        /// <summary>Убрать насовсем. Звать, когда цель уходит со сцены.</summary>
        public void Release()
        {
            IsReleased = true;
        }
    }
}
