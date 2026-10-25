using System;

namespace FeatherKit.Attributes
{
    /// <summary>
    /// Начинает рамку с заголовком в инспекторе: это поле и следующие за ним — до новой рамки
    /// или новой секции <see cref="FSectionAttribute"/>. Рамка плоская, с фоном: чем глубже
    /// вложенность, тем он заметнее.
    ///
    /// В отличие от <see cref="FGroupBoxAttribute"/>, ставится один раз на первое поле группы,
    /// а не на каждое, и не сворачивается.
    ///
    /// Наследник может сразу задать и секцию — <see cref="SectionTitle"/>: так одна метка
    /// проекта ставит поле и в секцию, и в группу.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class FGroupStartAttribute : Attribute
    {
        public FGroupStartAttribute(string title)
        {
            Title = title;
        }


        public string Title { get; }

        /// <summary>С этого поля начинается и секция с таким заголовком. null — секция та же, что выше.</summary>
        public string SectionTitle { get; protected set; }

        /// <summary>Место этой секции, как <see cref="FSectionAttribute.Order"/>.</summary>
        public int SectionOrder { get; protected set; }
    }
}
