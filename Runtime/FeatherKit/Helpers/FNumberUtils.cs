using System.Globalization;
using UnityEngine;

namespace FeatherKit.Helpers
{
    /// <summary>
    /// Числа для подписей в интерфейсе.
    /// - Сокращённая запись для счётчиков: 101000 → 101K, 1250000 → 1.25M.
    /// - Разброс «от и до»: 8-12, а одинаковые концы — одним числом.
    /// - Количество штук: x12.
    /// - Прибавка со знаком: +12.
    /// </summary>
    public static class FNumberUtils
    {
        // До этого числа пишем целиком: «9999» ещё влезает в плашку и читается сразу.
        private const long ShortenFrom = 10_000;

        private static readonly string[] Suffixes = { "K", "M", "B", "T" };


        // ── Сокращение ────────────────────────────────────────────────────

        /// <summary>
        /// Три значащие цифры и буква разряда. Считаем в целых и отбрасываем хвост:
        /// иначе 12 300 выходит «12.2K», а 99 999 — «100K».
        /// </summary>
        public static string ToShortText(this long value)
        {
            if (value > -ShortenFrom && value < ShortenFrom)
                return value.ToString(CultureInfo.InvariantCulture);

            var sign = value < 0 ? "-" : string.Empty;
            var rest = value == long.MinValue ? long.MaxValue : System.Math.Abs(value);
            var unit = 1000L;
            var suffix = 0;

            while (rest / unit >= 1000 && suffix < Suffixes.Length - 1)
            {
                unit *= 1000;
                suffix++;
            }

            var whole = rest / unit;
            var fraction = string.Empty;

            if (whole < 10)
                fraction = ((rest % unit) * 100 / unit).ToString("00", CultureInfo.InvariantCulture);
            else if (whole < 100)
                fraction = ((rest % unit) * 10 / unit).ToString(CultureInfo.InvariantCulture);

            fraction = fraction.TrimEnd('0');

            return sign + whole.ToString(CultureInfo.InvariantCulture)
                   + (fraction.Length > 0 ? "." + fraction : string.Empty)
                   + Suffixes[suffix];
        }


        // ── Разброс ───────────────────────────────────────────────────────

        /// <summary>
        /// «8-8» игрок читает как поломку, поэтому одинаковые концы — одно число.
        /// Концы в любом порядке: меньший всегда слева.
        /// </summary>
        public static string ToRangeText(this Vector2Int range)
        {
            var min = Mathf.Min(range.x, range.y);
            var max = Mathf.Max(range.x, range.y);

            return min == max
                ? max.ToString(CultureInfo.InvariantCulture)
                : $"{min.ToString(CultureInfo.InvariantCulture)}-{max.ToString(CultureInfo.InvariantCulture)}";
        }


        // ── Количество ────────────────────────────────────────────────────

        /// <summary>
        /// Со знаком «12» читается как количество, а не характеристика. Латинская x, а не «×»:
        /// знака умножения в шрифте интерфейса может не быть.
        /// </summary>
        public static string ToCountText(this int count)
        {
            return "x" + count.ToString(CultureInfo.InvariantCulture);
        }


        // ── Прибавка ──────────────────────────────────────────────────────

        /// <summary>Прибавка со знаком: «+12», «-5», «0». Без плюса рост не отличить от суммы.</summary>
        public static string ToSignedText(this int value)
        {
            var text = value.ToString(CultureInfo.InvariantCulture);

            return value > 0 ? "+" + text : text;
        }
    }
}
