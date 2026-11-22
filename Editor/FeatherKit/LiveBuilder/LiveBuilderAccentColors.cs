using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Зона интерфейса Live Builder, у каждой свой цвет (<see cref="LiveBuilderAccentColors"/>).</summary>
    public enum LiveBuilderAccentColor
    {
        Tool,
        Selection,
        Level
    }


    /// <summary>
    /// Цвета зон Live Builder — по цвету сразу видно, про что строка или рамка, в светлой и тёмной теме:
    /// - синий — инструмент и новое
    /// - оранжевый — выделенное
    /// - фиолетовый — уровень
    /// </summary>
    public static class LiveBuilderAccentColors
    {
        // ── Цвета ────────────────────────────────────────────────────────

        public static Color ColorOf(LiveBuilderAccentColor zone)
        {
            var isDark = EditorGUIUtility.isProSkin;

            return zone switch
            {
                LiveBuilderAccentColor.Tool => isDark ? new Color(0.23f, 0.46f, 0.78f) : new Color(0.16f, 0.42f, 0.82f),
                LiveBuilderAccentColor.Selection => isDark ? new Color(0.8f, 0.52f, 0.17f) : new Color(0.85f, 0.5f, 0.1f),
                _ => isDark ? new Color(0.46f, 0.4f, 0.76f) : new Color(0.42f, 0.35f, 0.75f)
            };
        }


        // Лёгкая подкраска цветом зоны.
        public static Color TintOf(LiveBuilderAccentColor zone, float alpha = 0.07f)
        {
            var color = ColorOf(zone);
            color.a = alpha;
            return color;
        }
    }
}
