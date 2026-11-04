using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Значки инструментов Live Builder — рисуются кодом из линий инструмента (<see cref="LiveBuilderTool.IconLines"/>, <see cref="LiveBuilderIconRasterizer"/>),
    /// без файлов картинок: чёткие на любом экране, под светлую и тёмную тему. Каждый растеризуется один раз и запоминается.
    /// Белый вариант красят при выводе (выбранная плитка — цветом акцента), цветной под тему — для готовых кнопок Unity.
    /// </summary>
    public static class LiveBuilderToolIcons
    {
        private static readonly Dictionary<(string ToolId, Color Color), Texture2D> Cache = new();


        // Белый значок: цвет задают при выводе через GUI.color.
        public static Texture2D White(LiveBuilderTool tool)
        {
            return Get(tool, Color.white);
        }


        // Значок цвета обычных значков редактора — для кнопок, которые сами красить не умеют.
        public static Texture2D Themed(LiveBuilderTool tool)
        {
            return Get(tool, EditorGUIUtility.isProSkin ? new Color(0.82f, 0.82f, 0.82f) : new Color(0.22f, 0.22f, 0.22f));
        }


        // Значок в прямоугольнике окна, нужного цвета.
        public static void Draw(Rect rect, LiveBuilderTool tool, Color color)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, White(tool), ScaleMode.ScaleToFit);
            GUI.color = previous;
        }


        // ── Кеш ──────────────────────────────────────────────────────────

        private static Texture2D Get(LiveBuilderTool tool, Color color)
        {
            if (Cache.TryGetValue((tool.Id, color), out var cached) && cached != null)
                return cached;

            var texture = LiveBuilderIconRasterizer.Rasterize(tool.IconLines, color);
            Cache[(tool.Id, color)] = texture;
            return texture;
        }
    }
}
