using System;

namespace FeatherKit.Attributes
{
    /// <summary>
    /// Поле-класс в инспекторе как блок: своё название вместо имени поля, внутри — рамка
    /// с серым описанием сверху и полями под ним.
    ///
    /// Нужно для ассетов из многих одинаковых блоков, например настроек действий персонажа:
    /// имя поля «hangHop» ничего не скажет, а «Подскок в висе» с фразой, когда он срабатывает, — да.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class FBlockTitleAttribute : Attribute
    {
        public FBlockTitleAttribute(string title, string description = null)
        {
            Title = title;
            Description = description;
        }


        public string Title { get; }

        /// <summary>Пояснение под заголовком внутри рамки. null — без него.</summary>
        public string Description { get; }
    }
}
