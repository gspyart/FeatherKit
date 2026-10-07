using UnityEngine;

namespace FeatherKit.Modifiers
{
    /// <summary>
    /// Множители одной величины: итог — их произведение, база — единица. Скорость хода,
    /// скорость разворота, получаемый урон — всё, что «во столько-то раз».
    ///
    /// Деления отдельным видом нет и не нужно: замедлить вдвое — это множитель 0.5.
    /// </summary>
    public class TimedMultipliers : TimedModifiers<float>
    {
        protected override float Default => 1f;

        protected override float Combine(float accumulated, float value) => accumulated * value;

        // Отрицательный множитель развернул бы величину: персонаж пошёл бы назад, а урон
        // начал лечить. Такого не просят намеренно, это всегда описка в настройке.
        protected override float Sanitize(float value) => Mathf.Max(0f, value);
    }
}
