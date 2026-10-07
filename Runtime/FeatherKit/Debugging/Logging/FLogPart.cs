using UnityEngine;

namespace FeatherKit.Logging
{
    /// <summary>
    /// Кусок сообщения со своим цветом. Строка превращается в кусок сама, поэтому обычный
    /// текст и цветной можно мешать в одном вызове:
    ///
    /// <code>FLog.Info("Уровень ", (index.ToString(), Color.cyan), " загружен");</code>
    /// </summary>
    public readonly struct FLogPart
    {
        public readonly string Text;
        public readonly Color Color;
        public readonly bool HasColor;
    

        public FLogPart(string text)
        {
            Text = text;
            Color = UnityEngine.Color.white;
            HasColor = false;
        }

        public FLogPart(string text, Color color)
        {
            Text = text;
            Color = color;
            HasColor = true;
        }


        public static implicit operator FLogPart(string text) => new FLogPart(text);

        public static implicit operator FLogPart((string text, Color color) part) =>
            new FLogPart(part.text, part.color);
    }
}
