using UnityEngine;
using HealthComponent = FeatherKit.Health.Health;

// События, которые шлёт и слушает сам FeatherKit. Список короткий намеренно: сюда попадает
// только то, что нужно самой библиотеке. Свои события игра объявляет у себя — шина
// принимает любую структуру.

namespace FeatherKit.Events
{
    /// <summary>
    /// Здоровье изменилось. Минус — сняли, плюс — восстановили; подписчик решает по знаку,
    /// какую цифру показать.
    ///
    /// Точка показа приходит готовой: её берёт сам <see cref="HealthComponent"/> у своего
    /// объекта через <see cref="Entities.IObjectHeight"/>. Так «над головой» посчитано
    /// в одном месте, а не у каждого подписчика заново, и подписчику не нужно знать,
    /// кого он показывает — персонажа, сундук или дверь.
    /// </summary>
    public struct HealthChangedEvent
    {
        /// <summary>Чьё здоровье. Отсюда же берутся Current и Max, если они нужны.</summary>
        public HealthComponent Target;

        /// <summary>Где показывать: макушка цели, а без роста — её позиция.</summary>
        public Vector3 Point;

        /// <summary>На сколько изменилось: отрицательное — урон, положительное — лечение.</summary>
        public int Delta;
    }
}
