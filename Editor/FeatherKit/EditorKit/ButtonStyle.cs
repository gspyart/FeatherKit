using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Набор параметров внешнего вида кнопки для GUIButton — просто данные, никакой отрисовки.
    /// Отдельно от логики рисования специально: стили — это то, что люди будут менять/добавлять
    /// в первую очередь, не трогая сам GUIButton.
    /// </summary>
    public readonly struct ButtonStyle
    {
        public readonly Color BackgroundColor;
        public readonly Color TextColor;
        public readonly Color BorderColor;
        public readonly float BorderThickness;
        public readonly float CornerRadius;

        public ButtonStyle(Color backgroundColor, Color textColor, Color borderColor, float borderThickness = 0f, float cornerRadius = 8f)
        {
            BackgroundColor = backgroundColor;
            TextColor = textColor;
            BorderColor = borderColor;
            BorderThickness = borderThickness;
            CornerRadius = cornerRadius;
        }
    }

    /// <summary>
    /// Готовые пресеты в духе iOS: Primary — залитая акцентная кнопка, Secondary — контурная,
    /// Destructive — залитая красная. Просто стартовый набор, ничего не мешает добавить свои.
    /// </summary>
    public static class GUIButtonStyles
    {
        private static readonly Color AccentBlue = new Color(0f / 255f, 122f / 255f, 255f / 255f);
        private static readonly Color DestructiveRed = new Color(255f / 255f, 59f / 255f, 48f / 255f);

        public static readonly ButtonStyle Primary = new ButtonStyle(
            backgroundColor: AccentBlue,
            textColor: Color.white,
            borderColor: Color.clear);

        public static readonly ButtonStyle Secondary = new ButtonStyle(
            backgroundColor: Color.clear,
            textColor: AccentBlue,
            borderColor: AccentBlue,
            borderThickness: 1.5f);

        public static readonly ButtonStyle Destructive = new ButtonStyle(
            backgroundColor: DestructiveRed,
            textColor: Color.white,
            borderColor: Color.clear);
    }
}
