using System;

namespace FeatherKit.Attributes
{
    /// <summary>
    /// Начинает секцию полей в инспекторе: синяя подпись заглавными над этим полем и следующими —
    /// до новой секции. Внутри секции поля можно разложить по рамкам <see cref="FGroupStartAttribute"/>.
    ///
    /// Секции выстраиваются по номеру <see cref="Order"/>, а не по месту в коде. Так поле из
    /// класса-наследника встаёт в свою секцию, а не в конец: у всех наследников один порядок
    /// секций, и общее от родителя внутри секции идёт раньше своего.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class FSectionAttribute : Attribute
    {
        public FSectionAttribute(string title, int order = 0)
        {
            Title = title;
            Order = order;
        }


        /// <summary>Подпись секции. В инспекторе — заглавными.</summary>
        public string Title { get; }

        /// <summary>Место секции: меньше — выше. Одинаковый у секций — идут, как в коде.</summary>
        public int Order { get; }
    }
}
