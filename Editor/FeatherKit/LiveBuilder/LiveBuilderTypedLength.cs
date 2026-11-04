using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Точная длина отрезка с клавиатуры, как в SketchUp: цифры, точка или запятая набирают число, Backspace стирает последнюю цифру.
    /// Только набор и разбор числа — куда встанет точка, решает тот, кто рисует (<see cref="LiveBuilderPathGesture"/>).
    /// </summary>
    public sealed class LiveBuilderTypedLength
    {
        // Короче этого длина не считается, м.
        private const float MinLength = 0.001f;

        private string text = "";


        // Набранное как есть: «2,5».
        public string Text => text;

        public bool IsTyping => text.Length > 0;


        // Цифра, точка или запятая — в длину; Backspace — стереть последнюю. Возвращает, забрал ли клавишу.
        public bool TryType(KeyCode key)
        {
            var character = key switch
            {
                >= KeyCode.Alpha0 and <= KeyCode.Alpha9 => (char)('0' + (key - KeyCode.Alpha0)),
                >= KeyCode.Keypad0 and <= KeyCode.Keypad9 => (char)('0' + (key - KeyCode.Keypad0)),
                KeyCode.Period or KeyCode.Comma or KeyCode.KeypadPeriod => ',',
                _ => '\0'
            };

            if (character == ',' && text.Contains(","))
                return true;

            if (character != '\0')
            {
                text += character;
                return true;
            }

            if (key != KeyCode.Backspace || text.Length == 0)
                return false;

            text = text.Substring(0, text.Length - 1);
            return true;
        }


        // Забрать набранную длину, м: набор стирается в любом случае. Не число или почти ноль — false.
        public bool TryTake(out float meters)
        {
            var isNumber = float.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out meters);
            text = "";
            return isNumber && meters > MinLength;
        }


        public void Clear()
        {
            text = "";
        }
    }
}
