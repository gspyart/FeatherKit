using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Запомненные значки Live Builder — и инструментов (<see cref="LiveBuilderToolIcons"/>), и настроек (<see cref="LiveBuilderOptionIcons"/>):
    /// каждый рисунок каждого цвета растеризуется один раз (<see cref="LiveBuilderIconRasterizer"/>). Здесь же цвет значка под тему
    /// и вывод белого значка нужным цветом. Сами рисунки — не здесь.
    /// </summary>
    public static class LiveBuilderIconCache
    {
        // Ключ — рисунок (имя инструмента или значок настройки) и цвет.
        private static readonly Dictionary<(object IconKey, Color Color), Texture2D> Cache = new();


        // Серый обычных значков редактора: светлый в тёмной теме, тёмный в светлой.
        public static Color ThemedColor => EditorGUIUtility.isProSkin ? new Color(0.78f, 0.78f, 0.78f) : new Color(0.25f, 0.25f, 0.25f);


        // Значок цвета color: из памяти, а нет — растеризуется из линий рисунка.
        public static Texture2D Get(object iconKey, Color color, System.Func<List<Vector2[]>> linesOf)
        {
            if (Cache.TryGetValue((iconKey, color), out var cached) && cached != null)
                return cached;

            var texture = LiveBuilderIconRasterizer.Rasterize(linesOf(), color);
            Cache[(iconKey, color)] = texture;
            return texture;
        }


        // Белый значок в прямоугольнике окна, нужного цвета.
        public static void Draw(Rect rect, Texture2D whiteIcon, Color color)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, whiteIcon, ScaleMode.ScaleToFit);
            GUI.color = previous;
        }
    }
}
