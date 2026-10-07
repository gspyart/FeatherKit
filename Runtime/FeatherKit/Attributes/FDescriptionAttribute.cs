using System;

namespace FeatherKit.Attributes
{
    /// <summary>
    /// Короткое описание класса, которое видно прямо в инспекторе.
    ///
    /// Нужно затем, что монобехов без полей много: связка, наблюдатель, модуль-переключатель.
    /// В коде их роль описана в шапке класса, а в инспекторе от неё видно только имя —
    /// и понять, зачем компонент висит на объекте, можно лишь открыв файл.
    ///
    /// Пишем ЗАЧЕМ он тут и что делает: одна-две фразы. Границы класса остаются в его
    /// комментарии — тот их задаёт, а этот напоминает о них на месте.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class FDescriptionAttribute : Attribute
    {
        public FDescriptionAttribute(string text)
        {
            Text = text;
        }


        public string Text { get; }
    }
}
