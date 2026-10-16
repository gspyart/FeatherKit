using System;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Подпись поля блока сценария в инспекторе — по-русски, вместо имени поля из кода.
    /// Подсказка по-прежнему берётся из Tooltip.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ScenarioLabelAttribute : Attribute
    {
        public ScenarioLabelAttribute(string text)
        {
            Text = text;
        }


        public string Text { get; }
    }
}
