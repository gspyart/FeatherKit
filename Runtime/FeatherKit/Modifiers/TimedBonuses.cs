namespace FeatherKit.Modifiers
{
    /// <summary>
    /// Прибавки к одной величине: итог — их сумма, база — ноль. Плюс к броне от щита,
    /// плюс к урону от бафа.
    ///
    /// Вычитания отдельным видом нет: это отрицательная прибавка.
    /// </summary>
    public class TimedBonuses : TimedModifiers<float>
    {
        protected override float Default => 0f;

        protected override float Combine(float accumulated, float value) => accumulated + value;
    }
}
