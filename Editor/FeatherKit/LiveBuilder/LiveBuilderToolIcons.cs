using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderIconRasterizer;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Значки инструментов Live Builder — рисуются кодом из линий (<see cref="LiveBuilderIconRasterizer"/>), без файлов картинок: чёткие на любом экране,
    /// под светлую и тёмную тему. Каждый растеризуется один раз и запоминается.
    /// Белый вариант красят при выводе (выбранная плитка — цветом акцента), цветной под тему — для готовых кнопок Unity.
    /// </summary>
    public static class LiveBuilderToolIcons
    {
        private static readonly Dictionary<(LiveBuilderTool Tool, Color Color), Texture2D> Cache = new();


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
            if (Cache.TryGetValue((tool, color), out var cached) && cached != null)
                return cached;

            var texture = LiveBuilderIconRasterizer.Rasterize(LinesOf(tool), color);
            Cache[(tool, color)] = texture;
            return texture;
        }


        // ── Рисунки ──────────────────────────────────────────────────────

        // Линии значка инструмента — ими же рисуют похожие значки настроек (<see cref="LiveBuilderOptionIcons"/>).
        public static List<Vector2[]> LinesOf(LiveBuilderTool tool)
        {
            return tool switch
            {
                LiveBuilderTool.Room => RoomLines(),
                LiveBuilderTool.Wall => WallLines(),
                LiveBuilderTool.Building => BuildingLines(),
                LiveBuilderTool.Stairs => StairsLines(),
                LiveBuilderTool.Opening => OpeningLines(),
                LiveBuilderTool.Ruler => RulerLines(),
                LiveBuilderTool.Select => SelectLines(),
                _ => BoxLines()
            };
        }

        // Стрелка курсора.
        private static List<Vector2[]> SelectLines()
        {
            return new List<Vector2[]>
            {
                Points(6, 3, 6, 19, 10, 15.5f, 13, 21, 15.5f, 20, 12.5f, 14, 18, 14, 6, 3)
            };
        }

        // Кубик в изометрии.
        private static List<Vector2[]> BoxLines()
        {
            return new List<Vector2[]>
            {
                Points(12, 3, 20, 7.5f, 20, 16.5f, 12, 21, 4, 16.5f, 4, 7.5f, 12, 3),
                Points(4, 7.5f, 12, 12, 20, 7.5f),
                Points(12, 12, 12, 21)
            };
        }

        // План комнаты с дверью: проём в стене и дуга створки.
        private static List<Vector2[]> RoomLines()
        {
            return new List<Vector2[]>
            {
                Points(13, 4, 20, 4, 20, 20, 4, 20, 4, 4, 8, 4),
                Points(8, 4, 8, 9),
                Arc(new Vector2(8, 4), 5f, 90f, 0f)
            };
        }

        // Кладка: два ряда кирпичей со сдвигом.
        private static List<Vector2[]> WallLines()
        {
            return new List<Vector2[]>
            {
                Points(3, 6, 21, 6, 21, 18, 3, 18, 3, 6),
                Points(3, 12, 21, 12),
                Points(9, 6, 9, 12), Points(15, 6, 15, 12),
                Points(6, 12, 6, 18), Points(12, 12, 12, 18), Points(18, 12, 18, 18)
            };
        }

        // Дом с окнами и дверью.
        private static List<Vector2[]> BuildingLines()
        {
            var lines = new List<Vector2[]> { Points(6, 21, 6, 3, 18, 3, 18, 21, 4, 21, 20, 21), Points(10.5f, 21, 10.5f, 17, 13.5f, 17, 13.5f, 21) };

            foreach (var y in new[] { 6.5f, 11.5f })
                foreach (var x in new[] { 9f, 13f })
                    lines.Add(Points(x, y, x + 2, y, x + 2, y + 2.5f, x, y + 2.5f, x, y));

            return lines;
        }

        // Профиль лестницы.
        private static List<Vector2[]> StairsLines()
        {
            return new List<Vector2[]> { Points(3, 20, 3, 16, 8, 16, 8, 12, 13, 12, 13, 8, 18, 8, 18, 4, 21, 4, 21, 20, 3, 20) };
        }

        // Окно в стене: рама с переплётом.
        private static List<Vector2[]> OpeningLines()
        {
            return new List<Vector2[]>
            {
                Points(3, 4, 21, 4, 21, 20, 3, 20, 3, 4),
                Points(8, 8, 16, 8, 16, 16, 8, 16, 8, 8),
                Points(12, 8, 12, 16), Points(8, 12, 16, 12)
            };
        }

        // Линейка наискось с делениями.
        private static List<Vector2[]> RulerLines()
        {
            var lines = new List<Vector2[]> { Points(3, 16, 16, 3, 21, 8, 8, 21, 3, 16) };
            var edgeStart = new Vector2(3, 16);
            var edgeEnd = new Vector2(16, 3);
            var inward = new Vector2(1f, 1f).normalized;

            for (var tick = 1; tick <= 5; tick++)
            {
                var point = Vector2.Lerp(edgeStart, edgeEnd, tick / 6f);
                lines.Add(new[] { point, point + inward * (tick % 2 == 0 ? 3.5f : 2f) });
            }

            return lines;
        }
    }
}
