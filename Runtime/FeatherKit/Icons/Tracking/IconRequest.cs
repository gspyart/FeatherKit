using System;
using FeatherKit.Entities;
using UnityEngine;

namespace FeatherKit.Icons
{
    /// <summary>
    /// Заявка на иконку над сущностью: что показать, для кого и по какому правилу прятать.
    /// Отдельным объектом, а не шестью аргументами: половину полей оставляют по умолчанию.
    /// </summary>
    public class IconRequest
    {
        /// <summary>Что показывать. Любой UI-объект: картинка, кнопка, панель.</summary>
        public RectTransform Prefab;

        /// <summary>Чем подменить, пока иконка прижата к краю. Пусто — у края останется та же.</summary>
        public RectTransform EdgePrefab;

        /// <summary>Смещение от центра сущности, м. Обычно вверх — иконка над головой.</summary>
        public Vector3 Offset = Vector3.up;

        public OffscreenMode Offscreen = OffscreenMode.Hide;

        /// <summary>
        /// Прижимать по краю САМОЙ иконки, а не по середине: широкая панель встанет внутрь
        /// целиком. Маленькой не нужно — сдвиг уводит её от цели.
        /// </summary>
        public bool ClampByIconEdge;

        /// <summary>Разъезжаться с соседями. Плата — иконка висит над целью не точно.</summary>
        public bool AvoidOverlap;

        /// <summary>Для кого иконка: её кнопки действуют от его имени. Пусто — ни для кого.</summary>
        public FEntityBase Viewer;

        /// <summary>
        /// Показывать ли сейчас. Спрашивается каждый кадр, сущность передаётся внутрь:
        /// «жив», «не подобран», «по карману». Null — показывать всегда.
        /// </summary>
        public Func<FEntityBase, bool> Condition;
    }
}
