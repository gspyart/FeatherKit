using System;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Подпись поля блока сценария в инспекторе — понятная человеку, вместо имени поля из кода: «Wait Until Done» вместо waitsForEnd.
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
