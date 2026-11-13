using System;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Поле блока или ноды рисуется только в инспекторе ноды, а не на самой ноде на холсте.
    /// Для редких и громоздких настроек: нода на холсте остаётся короткой.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ScenarioInspectorOnlyAttribute : Attribute
    {
    }
}
