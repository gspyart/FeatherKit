using System.Collections.Generic;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderIconRasterizer;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Значки настроек окна Live Builder (<see cref="LiveBuilderOptionIcon"/>): на кнопках переключателей, у подписей строк, на корешках вкладок.
    /// Рисуются кодом из линий, как значки инструментов, и запоминаются (<see cref="LiveBuilderIconCache"/>). «Добавить» и «Вырезать» — цветами каркасов в окне сцены.
    /// </summary>
    public static class LiveBuilderOptionIcons
    {
        // ── Значки ───────────────────────────────────────────────────────

        // Белый значок: цвет задают при выводе через GUI.color.
        public static Texture2D White(LiveBuilderOptionIcon icon)
        {
            return Get(icon, Color.white);
        }


        // Цвета обычных значков редактора; «Добавить» и «Вырезать» — цветами каркасов.
        public static Texture2D Themed(LiveBuilderOptionIcon icon)
        {
            return Get(icon, ThemedColorOf(icon));
        }

        private static Color ThemedColorOf(LiveBuilderOptionIcon icon)
        {
            return icon switch
            {
                LiveBuilderOptionIcon.BoxAdd => BoxModelPartHandles.AddColor,
                LiveBuilderOptionIcon.BoxCut => BoxModelPartHandles.CutColor,
                _ => LiveBuilderIconCache.ThemedColor
            };
        }


        // Значок в прямоугольнике окна, нужного цвета.
        public static void Draw(Rect rect, LiveBuilderOptionIcon icon, Color color)
        {
            LiveBuilderIconCache.Draw(rect, White(icon), color);
        }


        private static Texture2D Get(LiveBuilderOptionIcon icon, Color color)
        {
            return LiveBuilderIconCache.Get(icon, color, () => LinesOf(icon));
        }


        // ── Рисунки ──────────────────────────────────────────────────────

        private static List<Vector2[]> LinesOf(LiveBuilderOptionIcon icon)
        {
            return icon switch
            {
                LiveBuilderOptionIcon.Power => PowerLines(),
                LiveBuilderOptionIcon.Keyboard => KeyboardLines(),
                LiveBuilderOptionIcon.Level => LevelLines(),
                LiveBuilderOptionIcon.Storey => StoreyLines(),
                LiveBuilderOptionIcon.Grid => GridLines(),
                LiveBuilderOptionIcon.SnapPoints => SnapPointsLines(),
                LiveBuilderOptionIcon.Wireframe => WireframeLines(),
                LiveBuilderOptionIcon.ViewAll => ViewAllLines(),
                LiveBuilderOptionIcon.ViewRooms => SectionLines(10f),
                LiveBuilderOptionIcon.ViewWallsDown => SectionLines(15f),
                LiveBuilderOptionIcon.BoxAuto => BoxAutoLines(),
                LiveBuilderOptionIcon.BoxAdd => BoxAddLines(),
                LiveBuilderOptionIcon.BoxCut => BoxCutLines(),
                LiveBuilderOptionIcon.Boxes => LiveBuilderToolPalette.Get<LiveBuilderBoxTool>().IconLines,
                LiveBuilderOptionIcon.Faces => FacesLines(),
                LiveBuilderOptionIcon.NewObject => NewObjectLines(),
                LiveBuilderOptionIcon.Corner => CornerLines(),
                LiveBuilderOptionIcon.Smooth => SmoothLines(),
                LiveBuilderOptionIcon.Bezier => BezierLines(),
                LiveBuilderOptionIcon.Rectangle => new List<Vector2[]> { Rectangle(4, 6, 20, 18) },
                LiveBuilderOptionIcon.Contour => ContourLines(),
                LiveBuilderOptionIcon.OpeningRectangle => new List<Vector2[]> { Rectangle(7, 4, 17, 20) },
                LiveBuilderOptionIcon.OpeningArch => OpeningArchLines(),
                LiveBuilderOptionIcon.OpeningCircle => new List<Vector2[]> { Arc(new Vector2(12, 12), 7.5f, 0f, 360f) },
                LiveBuilderOptionIcon.AtCursor => AtCursorLines(),
                LiveBuilderOptionIcon.SeparateSteps => SeparateStepsLines(),
                LiveBuilderOptionIcon.SolidSteps => LiveBuilderToolPalette.Get<LiveBuilderStairsTool>().IconLines,
                LiveBuilderOptionIcon.ThisStorey => ThisStoreyLines(),
                LiveBuilderOptionIcon.AllStoreysAbove => AllStoreysAboveLines(),
                LiveBuilderOptionIcon.Floor => FloorLines(),
                LiveBuilderOptionIcon.Roof => RoofLines(),
                _ => new List<Vector2[]>()
            };
        }


        // ── Рисунки: окно ────────────────────────────────────────────────

        // Кнопка питания: кольцо с разрывом сверху и черта.
        private static List<Vector2[]> PowerLines()
        {
            return new List<Vector2[]> { Arc(new Vector2(12, 13), 7.5f, -55f, 235f), Points(12, 3, 12, 11) };
        }

        // Клавиатура: рамка, ряд клавиш и пробел.
        private static List<Vector2[]> KeyboardLines()
        {
            var lines = new List<Vector2[]> { Rectangle(2.5f, 6.5f, 21.5f, 17.5f), Points(8, 14, 16, 14) };

            foreach (var x in new[] { 6f, 10f, 14f, 18f })
                lines.Add(Dot(x, 10));

            return lines;
        }

        // Сложенная карта — весь уровень.
        private static List<Vector2[]> LevelLines()
        {
            return new List<Vector2[]>
            {
                Points(3, 6, 9, 4, 15, 6, 21, 4, 21, 18, 15, 20, 9, 18, 3, 20, 3, 6),
                Points(9, 4, 9, 18), Points(15, 6, 15, 20)
            };
        }


        // ── Рисунки: общее ───────────────────────────────────────────────

        // Слои один над другим — этажи.
        private static List<Vector2[]> StoreyLines()
        {
            return new List<Vector2[]>
            {
                Points(12, 4, 20, 8, 12, 12, 4, 8, 12, 4),
                Points(4, 12, 12, 16, 20, 12),
                Points(4, 16, 12, 20, 20, 16)
            };
        }

        private static List<Vector2[]> GridLines()
        {
            return new List<Vector2[]>
            {
                Rectangle(4, 4, 20, 20),
                Points(9.33f, 4, 9.33f, 20), Points(14.67f, 4, 14.67f, 20),
                Points(4, 9.33f, 20, 9.33f), Points(4, 14.67f, 20, 14.67f)
            };
        }

        // Угол двух рёбер и кольцо прицела на нём — прилипание к точкам.
        private static List<Vector2[]> SnapPointsLines()
        {
            return new List<Vector2[]> { Points(9, 21, 9, 9, 21, 9), Arc(new Vector2(9, 9), 4.5f, 0f, 360f) };
        }

        // Кубик со всеми рёбрами, и видимыми, и задними.
        private static List<Vector2[]> WireframeLines()
        {
            var lines = LiveBuilderToolPalette.Get<LiveBuilderBoxTool>().IconLines;
            lines.Add(Points(12, 12, 12, 3));
            lines.Add(Points(12, 12, 4, 16.5f));
            lines.Add(Points(12, 12, 20, 16.5f));
            return lines;
        }

        // Дом в разрезе целиком.
        private static List<Vector2[]> ViewAllLines()
        {
            return new List<Vector2[]> { Points(4, 20, 4, 9, 12, 3, 20, 9, 20, 20, 4, 20), Points(4, 14.5f, 20, 14.5f) };
        }

        // Стены до высоты среза и пунктир среза над ними.
        private static List<Vector2[]> SectionLines(float cutY)
        {
            var lines = new List<Vector2[]> { Points(4, cutY, 4, 20, 20, 20, 20, cutY), Points(12, cutY, 12, 20) };

            for (var x = 2f; x < 22f; x += 4f)
                lines.Add(Points(x, cutY - 3f, x + 2f, cutY - 3f));

            return lines;
        }


        // ── Рисунки: коробка ─────────────────────────────────────────────

        // Плюс над минусом: смотря куда тянешь.
        private static List<Vector2[]> BoxAutoLines()
        {
            return new List<Vector2[]> { Points(12, 3.5f, 12, 12.5f), Points(7.5f, 8, 16.5f, 8), Points(7.5f, 18, 16.5f, 18) };
        }

        private static List<Vector2[]> BoxAddLines()
        {
            return new List<Vector2[]> { Rectangle(4, 4, 20, 20), Points(12, 8, 12, 16), Points(8, 12, 16, 12) };
        }

        private static List<Vector2[]> BoxCutLines()
        {
            return new List<Vector2[]> { Rectangle(4, 4, 20, 20), Points(8, 12, 16, 12) };
        }

        // Кубик со штриховкой на одной грани.
        private static List<Vector2[]> FacesLines()
        {
            var lines = LiveBuilderToolPalette.Get<LiveBuilderBoxTool>().IconLines;

            foreach (var t in new[] { 0.25f, 0.5f, 0.75f })
                lines.Add(Points(12, 12 + 9f * t, 20, 7.5f + 9f * t));

            return lines;
        }

        private static List<Vector2[]> NewObjectLines()
        {
            return new List<Vector2[]> { Points(12, 5, 12, 19), Points(5, 12, 19, 12) };
        }


        // ── Рисунки: точки ───────────────────────────────────────────────

        private static List<Vector2[]> CornerLines()
        {
            return new List<Vector2[]> { Points(3, 19, 10, 6, 21, 16) };
        }

        private static List<Vector2[]> SmoothLines()
        {
            return new List<Vector2[]> { Curve(new Vector2(3, 19), new Vector2(7, 2), new Vector2(15, 4), new Vector2(21, 16)) };
        }

        // Кривая и ус касательной с концами.
        private static List<Vector2[]> BezierLines()
        {
            return new List<Vector2[]>
            {
                Curve(new Vector2(3, 20), new Vector2(5, 7), new Vector2(19, 7), new Vector2(21, 20)),
                Points(4, 10.25f, 20, 10.25f), Rectangle(2.5f, 8.75f, 5.5f, 11.75f), Rectangle(18.5f, 8.75f, 21.5f, 11.75f)
            };
        }


        // ── Рисунки: форма ───────────────────────────────────────────────

        // Г-образный контур — любая форма кликами.
        private static List<Vector2[]> ContourLines()
        {
            return new List<Vector2[]> { Points(4, 5, 13, 5, 13, 11, 20, 11, 20, 19, 4, 19, 4, 5) };
        }

        private static List<Vector2[]> OpeningArchLines()
        {
            return new List<Vector2[]> { Points(7, 11, 7, 20, 17, 20, 17, 11), Arc(new Vector2(12, 11), 5f, 180f, 360f) };
        }

        // Прицел — середина под мышью.
        private static List<Vector2[]> AtCursorLines()
        {
            return new List<Vector2[]>
            {
                Arc(new Vector2(12, 12), 5.5f, 0f, 360f),
                Points(12, 2, 12, 8), Points(12, 16, 12, 22), Points(2, 12, 8, 12), Points(16, 12, 22, 12)
            };
        }


        // ── Рисунки: лестница ────────────────────────────────────────────

        // Плиты ступеней по отдельности, под ними пусто.
        private static List<Vector2[]> SeparateStepsLines()
        {
            return new List<Vector2[]> { Rectangle(3, 17, 9, 20), Rectangle(9, 12, 15, 15), Rectangle(15, 7, 21, 10) };
        }


        // ── Рисунки: этажи здания ────────────────────────────────────────

        // Плита — этаж, куда встаёт поставленное; черта — этаж, куда не встаёт.
        private static List<Vector2[]> ThisStoreyLines()
        {
            return new List<Vector2[]> { Points(4, 5, 20, 5), Rectangle(4, 10.5f, 20, 13.5f), Points(4, 19, 20, 19) };
        }

        private static List<Vector2[]> AllStoreysAboveLines()
        {
            return new List<Vector2[]> { Rectangle(4, 3.5f, 20, 6.5f), Rectangle(4, 10.5f, 20, 13.5f), Points(4, 19, 20, 19) };
        }

        // Стены и плита снизу.
        private static List<Vector2[]> FloorLines()
        {
            return new List<Vector2[]> { Points(6, 4, 6, 16), Points(18, 4, 18, 16), Rectangle(3, 16, 21, 20) };
        }

        // Стены и плита сверху.
        private static List<Vector2[]> RoofLines()
        {
            return new List<Vector2[]> { Rectangle(3, 4, 21, 8), Points(6, 8, 6, 20), Points(18, 8, 18, 20) };
        }
    }
}
