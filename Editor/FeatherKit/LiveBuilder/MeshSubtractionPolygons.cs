using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Многоугольники меша, из которого по очереди вычитают тела (<see cref="MeshSubtraction"/>): рёбра фаски, скосы, вырезы.
    /// - помнит габарит каждого и раскладывает их по ячейкам сетки: «кто рядом с телом» — без перебора всего меша
    /// - порядок как у простого списка: уцелевшие стоят на своих местах, куски после вычитания — в конце
    /// </summary>
    public sealed class MeshSubtractionPolygons
    {
        // Запас к габариту тела при отборе многоугольников рядом, м.
        private const float NearMargin = 0.01f;

        // Сторона ячейки сетки, м.
        private const float CellSize = 1f;

        // Многоугольник больше стольких ячеек — мимо сетки, проверяется при каждом отборе.
        private const int MaxCellsPerPolygon = 64;

        // Тело больше стольких ячеек — отбор перебором всего меша: так уже не дольше.
        private const int MaxCellsPerSelection = 1024;

        // Дальше этого от нуля ячейку не посчитать в целых, м.
        private const float MaxCellCoordinate = 1e6f;

        // Номер многоугольника — его место в порядке. Вынутый вычитанием — null.
        private readonly List<MeshBooleanPolygon> polygonsByNumber = new();
        private readonly List<Bounds> boundsByNumber = new();

        // Номера многоугольников по ячейкам сетки; вынутые убираются из ячейки при следующем её просмотре.
        // Сетка — со второго вычитания: на одно вычитание (скос пандуса) хватает перебора, а сетка стоила бы дороже.
        private readonly Dictionary<long, List<int>> numbersByCell = new();
        private readonly List<int> numbersOutsideCells = new();
        private bool hasGrid;

        // Каким по счёту отбором многоугольник уже взят: он лежит в нескольких ячейках, а взять его надо один раз.
        private readonly List<int> lastSelectionByNumber = new();
        private int selectionCount;

        private readonly List<int> nearNumbers = new();


        public MeshSubtractionPolygons(List<MeshBooleanPolygon> polygons)
        {
            foreach (var polygon in polygons)
                Add(polygon);
        }


        // Развёртка новых граней следующих вычитаний; нет — метры на ближайшую плоскость, как раньше.
        public LiveBuilderFaceUnwrapper Unwrapper { get; set; }


        // ── Вычитание ────────────────────────────────────────────────────

        // Тело с габаритом solidBounds (в осях меша) — из многоугольников рядом с ним. solidFor строит тело по многоугольникам рядом:
        // по ним, например, выбирают материал новых граней.
        public void SubtractNear(Bounds solidBounds, System.Func<List<MeshBooleanPolygon>, List<MeshBooleanPolygon>> solidFor)
        {
            solidBounds.Expand(NearMargin * 2f);
            SelectNear(solidBounds);

            if (nearNumbers.Count == 0)
                return;

            var near = new List<MeshBooleanPolygon>(nearNumbers.Count);

            foreach (var number in nearNumbers)
            {
                near.Add(polygonsByNumber[number]);
                polygonsByNumber[number] = null;
            }

            foreach (var polygon in MeshBoolean.Subtract(near, solidFor(near)))
            {
                if (polygon.IsFromCutter && Unwrapper != null)
                    Unwrapper.Unwrap(polygon);
                else if (polygon.IsFromCutter)
                    MeshSubtraction.UnwrapFlat(polygon);

                Add(polygon);
            }
        }

        // Номера многоугольников, чей габарит задевает bounds, — по порядку.
        private void SelectNear(Bounds bounds)
        {
            selectionCount++;
            nearNumbers.Clear();

            if (!hasGrid && selectionCount > 1)
                BuildGrid();

            if (hasGrid && TryGetCells(bounds, MaxCellsPerSelection, out var minCell, out var maxCell))
            {
                for (var x = minCell.x; x <= maxCell.x; x++)
                for (var y = minCell.y; y <= maxCell.y; y++)
                for (var z = minCell.z; z <= maxCell.z; z++)
                    if (numbersByCell.TryGetValue(CellKey(x, y, z), out var numbers))
                        SelectFrom(numbers, bounds);

                SelectFrom(numbersOutsideCells, bounds);
            }
            else
            {
                for (var number = 0; number < polygonsByNumber.Count; number++)
                    if (polygonsByNumber[number] != null && boundsByNumber[number].Intersects(bounds))
                        nearNumbers.Add(number);
            }

            nearNumbers.Sort();
        }

        // Заодно выкидывает из списка вынутые многоугольники.
        private void SelectFrom(List<int> numbers, Bounds bounds)
        {
            var kept = 0;

            for (var index = 0; index < numbers.Count; index++)
            {
                var number = numbers[index];

                if (polygonsByNumber[number] == null)
                    continue;

                numbers[kept++] = number;

                if (lastSelectionByNumber[number] == selectionCount || !boundsByNumber[number].Intersects(bounds))
                    continue;

                lastSelectionByNumber[number] = selectionCount;
                nearNumbers.Add(number);
            }

            numbers.RemoveRange(kept, numbers.Count - kept);
        }


        // Уцелевшие и новые многоугольники по порядку — для записи в меш.
        public List<MeshBooleanPolygon> InOrder()
        {
            var polygons = new List<MeshBooleanPolygon>();

            foreach (var polygon in polygonsByNumber)
                if (polygon != null)
                    polygons.Add(polygon);

            return polygons;
        }


        // ── Сетка ────────────────────────────────────────────────────────

        private void Add(MeshBooleanPolygon polygon)
        {
            polygonsByNumber.Add(polygon);
            boundsByNumber.Add(MeshSubtraction.BoundsOf(polygon));
            lastSelectionByNumber.Add(0);

            if (hasGrid)
                AddToGrid(polygonsByNumber.Count - 1);
        }


        private void BuildGrid()
        {
            hasGrid = true;

            for (var number = 0; number < polygonsByNumber.Count; number++)
                if (polygonsByNumber[number] != null)
                    AddToGrid(number);
        }

        private void AddToGrid(int number)
        {
            if (!TryGetCells(boundsByNumber[number], MaxCellsPerPolygon, out var minCell, out var maxCell))
            {
                numbersOutsideCells.Add(number);
                return;
            }

            for (var x = minCell.x; x <= maxCell.x; x++)
            for (var y = minCell.y; y <= maxCell.y; y++)
            for (var z = minCell.z; z <= maxCell.z; z++)
            {
                var key = CellKey(x, y, z);

                if (!numbersByCell.TryGetValue(key, out var numbers))
                    numbersByCell[key] = numbers = new List<int>();

                numbers.Add(number);
            }
        }


        // Ячейки габарита — по тем же min и max, что сравнивает Bounds.Intersects: задевают габариты друг друга — задевают и их ячейки.
        // Больше maxCellCount ячеек или далеко от нуля — false.
        private static bool TryGetCells(Bounds bounds, int maxCellCount, out Vector3Int minCell, out Vector3Int maxCell)
        {
            var min = bounds.min;
            var max = bounds.max;
            minCell = maxCell = default;

            for (var axis = 0; axis < 3; axis++)
                if (!(Mathf.Abs(min[axis]) < MaxCellCoordinate && Mathf.Abs(max[axis]) < MaxCellCoordinate))
                    return false;

            minCell = new Vector3Int(CellOf(min.x), CellOf(min.y), CellOf(min.z));
            maxCell = new Vector3Int(CellOf(max.x), CellOf(max.y), CellOf(max.z));
            var cellCount = (long)(maxCell.x - minCell.x + 1) * (maxCell.y - minCell.y + 1) * (maxCell.z - minCell.z + 1);
            return cellCount <= maxCellCount;
        }

        private static int CellOf(float coordinate)
        {
            return Mathf.FloorToInt(coordinate / CellSize);
        }

        // Три номера ячейки — в одно число; совпадут разве что ячейки за тысячи километров, и тогда лишний кандидат отсеется по габариту.
        private static long CellKey(int x, int y, int z)
        {
            const long mask = (1L << 21) - 1;
            return ((x & mask) << 42) | ((y & mask) << 21) | (z & mask);
        }
    }
}
