using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Точные числа с клавиатуры, как в SketchUp: цифры, точка или запятая набирают число, Backspace стирает последнюю цифру.
    /// Если тому, кто рисует, нужно несколько чисел (стороны прямоугольника и глубина), X, «;» или «*» на цифровой клавиатуре начинают следующее: «4×3×2».
    /// Только набор и разбор чисел — куда встанет точка и что значит каждое число, решает тот, кто рисует (<see cref="LiveBuilderPathGesture"/>, <see cref="BoxModelDrawGesture"/>).
    /// </summary>
    public sealed class LiveBuilderTypedLength
    {
        // Короче этого длина не считается, м.
        private const float MinLength = 0.001f;

        private const char Separator = '×';

        private readonly int maxValues;
        private string text = "";


        // maxValues — сколько чисел можно набрать через «×»; одно — разделитель не набирается.
        public LiveBuilderTypedLength(int maxValues = 1)
        {
            this.maxValues = Mathf.Max(1, maxValues);
        }


        // Набранное как есть: «2,5» или «4×3».
        public string Text => text;

        public bool IsTyping => text.Length > 0;

        private int ValueCount => text.Split(Separator).Length;

        // Число, которое набирается сейчас, — после последнего разделителя.
        private string CurrentValue => text.Substring(text.LastIndexOf(Separator) + 1);


        // ── Набор ────────────────────────────────────────────────────────

        // Цифра, точка или запятая — в число; X, «;» или «*» — следующее число; Backspace — стереть последнее. Возвращает, забрал ли клавишу.
        public bool TryType(KeyCode key)
        {
            var character = key switch
            {
                >= KeyCode.Alpha0 and <= KeyCode.Alpha9 => (char)('0' + (key - KeyCode.Alpha0)),
                >= KeyCode.Keypad0 and <= KeyCode.Keypad9 => (char)('0' + (key - KeyCode.Keypad0)),
                KeyCode.Period or KeyCode.Comma or KeyCode.KeypadPeriod => ',',
                KeyCode.X or KeyCode.Semicolon or KeyCode.KeypadMultiply => Separator,
                _ => '\0'
            };

            if (character == Separator)
                return TryTypeSeparator();

            if (character == ',' && CurrentValue.Contains(","))
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

        // Разделитель — только после набранного числа и пока чисел меньше, чем можно. Ничего не набрано — клавиша не наша: X без набора не трогаем.
        private bool TryTypeSeparator()
        {
            if (maxValues == 1 || text.Length == 0)
                return false;

            if (CurrentValue.Length > 0 && ValueCount < maxValues)
                text += Separator;

            return true;
        }


        // ── Разбор ───────────────────────────────────────────────────────

        // Забрать набранную длину, м: набор стирается в любом случае. Не одно число или почти ноль — false.
        public bool TryTake(out float meters)
        {
            var values = new List<float>();
            var isTaken = TryTakeAll(values) && values.Count == 1;
            meters = isTaken ? values[0] : 0f;
            return isTaken;
        }


        // Посмотреть набранные числа, не стирая набор: можно, пока набирают, — прямоугольник подстраивается на ходу. Недописанное последнее — без него.
        public bool TryPeekAll(List<float> values)
        {
            values.Clear();

            foreach (var part in text.Split(Separator))
            {
                if (!TryParse(part, out var meters))
                    break;

                values.Add(meters);
            }

            return values.Count > 0;
        }


        // Забрать все набранные числа по порядку, м: набор стирается в любом случае. Хоть одно не число или почти ноль — false.
        public bool TryTakeAll(List<float> values)
        {
            values.Clear();
            var parts = text.Split(Separator);
            text = "";

            foreach (var part in parts)
            {
                if (!TryParse(part, out var meters))
                {
                    values.Clear();
                    return false;
                }

                values.Add(meters);
            }

            return values.Count > 0;
        }

        // Одно число из набора, м; не число или почти ноль — false.
        private static bool TryParse(string part, out float meters)
        {
            var isNumber = float.TryParse(part.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out meters);
            return isNumber && meters > MinLength;
        }


        public void Clear()
        {
            text = "";
        }
    }
}
