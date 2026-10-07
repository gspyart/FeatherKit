using FeatherKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace FeatherKit.Helpers
{
    /// <summary>
    /// Сколько ячеек показать в окошке прокрутки: по этому числу сетка добивает себя пустыми
    /// до края — полупустая сетка читается как «остальное не загрузилось».
    /// - считает по самой раскладке: ячейка, отступы, окошко, а не число из инспектора;
    /// - ряды берёт целые или вместе с неполным у края, плюс запасные — как попросят;
    /// - держит запас снизу под полосу, лежащую поверх низа окошка.
    /// </summary>
    public static class FScrollGridUtils
    {
        // Зазор между последним рядом и полосой поверх списка, когда он докручен до конца.
        private const int RoomAboveBarGap = 16;

        private static readonly Vector3[] Corners = new Vector3[4];


        // ── Запас под полосу ──────────────────────────────────────────────

        /// <summary>
        /// Докрученный до конца список встаёт над полосой, а не под ней. Звать до досчёта
        /// пустых: ряд, закрытый полосой, пустыми не добивается.
        /// </summary>
        public static void ReserveRoomAbove(ScrollList list, RectTransform bar, bool barShown, int ownBottom)
        {
            var grid = list != null ? list.Items.GetComponent<GridLayoutGroup>() : null;

            if (grid == null)
                return;

            var bottom = ownBottom + (barShown && bar != null ? CoveredFromBelow(list, bar) + RoomAboveBarGap : 0);

            if (grid.padding.bottom == bottom)
                return;

            var padding = grid.padding;

            // Новым отступом, а не правкой старого: так сетка узнаёт, что её надо пересобрать.
            grid.padding = new RectOffset(padding.left, padding.right, padding.top, bottom);
        }

        private static int CoveredFromBelow(ScrollList list, RectTransform bar)
        {
            var window = WindowOf(list);

            bar.GetWorldCorners(Corners);

            var barTop = window.InverseTransformPoint(Corners[1]).y;

            return Mathf.Max(0, Mathf.CeilToInt(barTop - window.rect.yMin));
        }


        /// <summary>Нижний отступ сетки как он есть: его помнят, чтобы вернуть, когда запас не нужен.</summary>
        public static int BottomPadding(ScrollList list)
        {
            var grid = list != null ? list.Items.GetComponent<GridLayoutGroup>() : null;

            return grid != null ? grid.padding.bottom : 0;
        }


        // ── Число ячеек ───────────────────────────────────────────────────

        /// <summary>
        /// Сколько ячеек показать под столько вещей: окошко целыми рядами и последний ряд
        /// до конца. Раскладка не сеточная — ровно по вещам.
        /// </summary>
        public static int FilledCellCount(ScrollList list, int itemCount)
        {
            if (list == null)
                return itemCount;

            return FilledCellCount(list.Items as RectTransform, WindowOf(list), ScrollOf(list), itemCount,
                false, 0);
        }


        /// <summary>
        /// То же для сетки без <see cref="ScrollList"/>: окошко — у ближайшей прокрутки выше.
        /// Неполный ряд у края и запасные ряды — по просьбе.
        /// </summary>
        public static int FilledCellCount(RectTransform items, int itemCount, bool countsPartialLine,
            int spareLines)
        {
            var scroll = items != null ? items.GetComponentInParent<ScrollRect>(true) : null;
            var window = scroll != null && scroll.viewport != null
                ? scroll.viewport
                : items != null ? items.parent as RectTransform : null;

            return FilledCellCount(items, window, scroll, itemCount, countsPartialLine, spareLines);
        }

        // Ряд — это поперёк прокрутки: у ленты он вертикальный.
        private static int FilledCellCount(RectTransform items, RectTransform window, ScrollRect scroll,
            int itemCount, bool countsPartialLine, int spareLines)
        {
            var horizontal = IsHorizontal(scroll);

            if (!Measure(items, window, horizontal, countsPartialLine, out var columns, out var rows))
                return itemCount;

            var line = horizontal ? rows : columns;

            if (line <= 0)
                return Mathf.Max(itemCount, columns * rows);

            var lines = Mathf.CeilToInt(itemCount / (float)line) + spareLines;

            return Mathf.Max(columns * rows, lines * line);
        }


        /// <summary>
        /// Сколько ячеек сетки видно целиком. Ноль — раскладка не сеточная или мерить
        /// нечего: тогда добивать нечем и не нужно.
        /// </summary>
        public static int VisibleCellCount(ScrollList list)
        {
            if (list == null)
                return 0;

            var measured = Measure(list.Items as RectTransform, WindowOf(list), IsHorizontal(ScrollOf(list)),
                false, out var columns, out var rows);

            return measured ? columns * rows : 0;
        }


        // ── Замер ─────────────────────────────────────────────────────────

        // Неполный ряд у края считают те, у кого под последним рядом не должно оставаться
        // пустоты; целые — те, у кого список из одних пустых ячеек не должен листаться.
        private static bool Measure(RectTransform items, RectTransform window, bool horizontal,
            bool countsPartialLine, out int columns, out int rows)
        {
            columns = rows = 0;

            var grid = items != null ? items.GetComponent<GridLayoutGroup>() : null;

            if (grid == null || window == null)
                return false;

            // Окно зовут в тот же кадр, что включили: без досчёта раскладки окошко ещё
            // без размера, и при первом открытии пустых ячеек не было бы вовсе.
            Canvas.ForceUpdateCanvases();

            columns = CountAlong(window.rect.width, grid.cellSize.x, grid.spacing.x,
                grid.padding.left + grid.padding.right, countsPartialLine && horizontal);

            rows = CountAlong(window.rect.height, grid.cellSize.y, grid.spacing.y,
                grid.padding.top + grid.padding.bottom, countsPartialLine && !horizontal);

            // Жёсткое число рядов или столбцов задано самой сеткой — оно и главнее
            // того, что влезло бы по месту.
            if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                columns = grid.constraintCount;
            else if (grid.constraint == GridLayoutGroup.Constraint.FixedRowCount)
                rows = grid.constraintCount;

            if (countsPartialLine)
            {
                columns = Mathf.Max(1, columns);
                rows = Mathf.Max(1, rows);
            }

            return true;
        }

        // Последней ячейке отступ за ней не нужен, поэтому его добавляем к длине. Допуск —
        // против дробной ошибки: окошко ровно в три ряда не должно считаться двумя.
        private static int CountAlong(float length, float cell, float spacing, float padding, bool partial)
        {
            var step = cell + spacing;

            if (step <= 0f)
                return 0;

            var fits = (length - padding + spacing) / step;

            return partial
                ? Mathf.Max(1, Mathf.CeilToInt(fits))
                : Mathf.Max(0, Mathf.FloorToInt(fits + 0.001f));
        }


        // ── Окошко ────────────────────────────────────────────────────────

        private static ScrollRect ScrollOf(ScrollList list)
        {
            return list.GetComponent<ScrollRect>();
        }

        // Окошко с маской, а не сама прокрутка: содержимое видно ровно в нём.
        private static RectTransform WindowOf(ScrollList list)
        {
            var scroll = ScrollOf(list);

            if (scroll != null && scroll.viewport != null)
                return scroll.viewport;

            return list.transform as RectTransform;
        }

        private static bool IsHorizontal(ScrollRect scroll)
        {
            return scroll != null && scroll.horizontal && !scroll.vertical;
        }
    }
}
