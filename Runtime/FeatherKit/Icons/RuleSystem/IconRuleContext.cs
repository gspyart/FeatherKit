using FeatherKit.Entities;
using UnityEngine;

namespace FeatherKit.Icons
{
    /// <summary>
    /// Для кого правило ищет цели: один наблюдатель, от него и меряют расстояние.
    /// - любая сущность, а не персонаж: игровой тип запер бы систему в одной игре;
    /// - остальное — сумку, досягаемость — правило спрашивает у наблюдателя само.
    /// Структурой: собирается на каждом пересчёте без мусора.
    /// </summary>
    public readonly struct IconRuleContext
    {
        /// <summary>Кому показываем. Null — показывать некому: погиб, ещё не появился.</summary>
        public readonly FEntityBase Viewer;


        public IconRuleContext(FEntityBase viewer)
        {
            Viewer = viewer;
        }


        public bool IsValid => Viewer != null;

        /// <summary>Объект наблюдателя: у него спрашивают компоненты и сервисы сцены.</summary>
        public GameObject ViewerObject => Viewer != null ? Viewer.gameObject : null;

        /// <summary>Откуда меряем расстояние — центр наблюдателя, а не его ноги.</summary>
        public Vector3 Origin => Viewer != null ? Viewer.Center : Vector3.zero;
    }
}
