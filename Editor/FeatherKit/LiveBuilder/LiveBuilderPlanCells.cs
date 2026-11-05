using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Точки и рёбра привязки (<see cref="LiveBuilderPointSnap"/>), разложенные по клеткам плана — по X и Z мира, — чтобы у мыши перебирать
    /// только соседние, а не все на уровне. Годится, когда плоскость рисования горизонтальна: точку на неё сносит по Y, и клетка плана та же.
    /// Хранит только номера в списках привязки; что в них лежит, не знает.
    /// </summary>
    public sealed class LiveBuilderPlanCells
    {
        // Сторона клетки плана, м.
        private const float CellSize = 2f;

        // Ребро длиннее стольких клеток не раскладывается, а проверяется всегда: длинная стена не забьёт сотню клеток.
        private const int MaxCellsPerEdge = 32;

        // Круг поиска шире стольких клеток — дешевле перебрать всё: камера далеко, на экране весь уровень.
        private const int MaxCellsPerQuery = 400;

        private readonly Dictionary<Vector2Int, List<int>> pointsByCell = new();
        private readonly Dictionary<Vector2Int, List<int>> edgesByCell = new();
        private readonly List<int> longEdges = new();


        // ── Раскладка ────────────────────────────────────────────────────

        public void Clear()
        {
            pointsByCell.Clear();
            edgesByCell.Clear();
            longEdges.Clear();
        }


        public void AddPoint(int index, Vector3 point)
        {
            ListAt(pointsByCell, CellOf(point.x, point.z)).Add(index);
        }


        // Ребро — во все клетки, которые задевает его габарит на плане; длинное — в общий список.
        public void AddEdge(int index, Vector3 start, Vector3 end)
        {
            var min = CellOf(Mathf.Min(start.x, end.x), Mathf.Min(start.z, end.z));
            var max = CellOf(Mathf.Max(start.x, end.x), Mathf.Max(start.z, end.z));

            if ((max.x - min.x + 1) * (max.y - min.y + 1) > MaxCellsPerEdge)
            {
                longEdges.Add(index);
                return;
            }

            for (var x = min.x; x <= max.x; x++)
                for (var z = min.y; z <= max.y; z++)
                    ListAt(edgesByCell, new Vector2Int(x, z)).Add(index);
        }


        // ── Поиск ────────────────────────────────────────────────────────

        // Номера точек в клетках круга на плане. Круг слишком большой — false: перебирать всё.
        public bool TryCollectPoints(Vector3 center, float radius, List<int> result)
        {
            result.Clear();

            if (!TryGetCellRange(center, radius, out var min, out var max))
                return false;

            for (var x = min.x; x <= max.x; x++)
                for (var z = min.y; z <= max.y; z++)
                    if (pointsByCell.TryGetValue(new Vector2Int(x, z), out var indices))
                        result.AddRange(indices);

            return true;
        }


        // Номера рёбер в клетках круга и все длинные, каждое по разу. Круг слишком большой — false: перебирать всё.
        public bool TryCollectEdges(Vector3 center, float radius, HashSet<int> result)
        {
            result.Clear();

            if (!TryGetCellRange(center, radius, out var min, out var max))
                return false;

            result.UnionWith(longEdges);

            for (var x = min.x; x <= max.x; x++)
                for (var z = min.y; z <= max.y; z++)
                    if (edgesByCell.TryGetValue(new Vector2Int(x, z), out var indices))
                        result.UnionWith(indices);

            return true;
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static bool TryGetCellRange(Vector3 center, float radius, out Vector2Int min, out Vector2Int max)
        {
            min = CellOf(center.x - radius, center.z - radius);
            max = CellOf(center.x + radius, center.z + radius);
            return (max.x - min.x + 1) * (max.y - min.y + 1) <= MaxCellsPerQuery;
        }

        private static Vector2Int CellOf(float x, float z)
        {
            return new Vector2Int(Mathf.FloorToInt(x / CellSize), Mathf.FloorToInt(z / CellSize));
        }

        private static List<int> ListAt(Dictionary<Vector2Int, List<int>> cells, Vector2Int cell)
        {
            if (!cells.TryGetValue(cell, out var list))
            {
                list = new List<int>();
                cells.Add(cell, list);
            }

            return list;
        }
    }
}
