using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;
using UnityEngine.Rendering;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Проём в стене по линии в её собственных координатах: от и до вдоль пути (u) и от и до по высоте над линией (v), м.</summary>
    public readonly struct SplineWallHole
    {
        public SplineWallHole(float alongFrom, float alongTo, float heightFrom, float heightTo)
        {
            AlongFrom = alongFrom;
            AlongTo = alongTo;
            HeightFrom = heightFrom;
            HeightTo = heightTo;
        }


        public float AlongFrom { get; }
        public float AlongTo { get; }
        public float HeightFrom { get; }
        public float HeightTo { get; }


        public bool Contains(float along, float height)
        {
            return along > AlongFrom && along < AlongTo && height > HeightFrom && height < HeightTo;
        }
    }


    /// <summary>
    /// Строит меш стены по линии. Стена развёрнута в прямоугольник: вдоль пути (u) на высоту (v).
    /// Края проёмов и точки пути режут его на клетки; клетка внутри проёма — дыра, остальные — стена.
    /// Так проёмы, которые накладываются, просто сливаются, а на кривой проём изгибается вместе со стеной.
    /// Из клеток — обе стороны, верх, торцы открытого пути и откосы проёмов: бока, подоконник, верх проёма.
    /// Низа нет — стена всегда на чём-то стоит. Развёртка текстуры — по метрам.
    /// </summary>
    public static class SplineWallMeshBuilder
    {
        private const float BreakTolerance = 0.001f;


        // Заполняет меш стены заново по путям и проёмам каждого пути, с копиями вверх. Один материал на всё.
        public static void Fill(Mesh mesh, SplineWall wall, IReadOnlyList<SplineWallPath> paths, IReadOnlyList<List<SplineWallHole>> holesByPath)
        {
            var geometry = new SplineWallGeometry(1);
            GetSideOffsets(wall, out var leftOffset, out var rightOffset);

            for (var index = 0; index < paths.Count; index++)
                for (var copy = 0; copy < wall.CopiesUp; copy++)
                {
                    var bottom = copy * wall.CopyStep;
                    AddWalls(geometry, paths[index], holesByPath[index], leftOffset, rightOffset, bottom, bottom + wall.Height, SplineWallFaceSubmeshes.Single);
                }

            geometry.WriteTo(mesh);
        }

        // Смещения сторон от линии: по центру — поровну, слева или справа — стена целиком с одной стороны.
        public static void GetSideOffsets(SplineWall wall, out float leftOffset, out float rightOffset)
        {
            var thickness = wall.Thickness;

            switch (wall.Alignment)
            {
                case SplineWallAlignment.Left:
                    leftOffset = -thickness;
                    rightOffset = 0f;
                    break;

                case SplineWallAlignment.Right:
                    leftOffset = 0f;
                    rightOffset = thickness;
                    break;

                default:
                    leftOffset = -thickness / 2f;
                    rightOffset = thickness / 2f;
                    break;
            }
        }


        // ── Один путь ────────────────────────────────────────────────────

        // Стены одного пути от высоты bottom до top над линией: стороны, верх, торцы и откосы проёмов — каждое в свою часть меша.
        public static void AddWalls(SplineWallGeometry geometry, SplineWallPath path, List<SplineWallHole> holes, float leftOffset, float rightOffset,
            float bottom, float top, SplineWallFaceSubmeshes submeshes)
        {
            var alongBreaks = AlongBreaks(path, holes);
            var heightBreaks = HeightBreaks(holes, bottom, top);
            var solid = SolidCells(alongBreaks, heightBreaks, holes);
            var columnCount = alongBreaks.Count - 1;
            var rowCount = heightBreaks.Count - 1;

            for (var column = 0; column < columnCount; column++)
            {
                var alongFrom = alongBreaks[column];
                var alongTo = alongBreaks[column + 1];

                foreach (var (rowFrom, rowTo) in SolidRuns(solid, column, rowCount))
                {
                    var heightFrom = heightBreaks[rowFrom];
                    var heightTo = heightBreaks[rowTo];
                    AddSide(geometry, path, alongFrom, alongTo, heightFrom, heightTo, rightOffset, true, submeshes.Right);
                    AddSide(geometry, path, alongFrom, alongTo, heightFrom, heightTo, leftOffset, false, submeshes.Left);
                }

                if (solid[column, rowCount - 1])
                    AddHorizontal(geometry, path, alongFrom, alongTo, top, leftOffset, rightOffset, true, submeshes.Top);

                for (var row = 0; row < rowCount - 1; row++)
                    if (solid[column, row] != solid[column, row + 1])
                        AddHorizontal(geometry, path, alongFrom, alongTo, heightBreaks[row + 1], leftOffset, rightOffset, solid[column, row], submeshes.Reveal);
            }

            for (var boundary = 0; boundary <= columnCount; boundary++)
                AddJambs(geometry, path, solid, boundary, columnCount, rowCount, alongBreaks, heightBreaks, leftOffset, rightOffset, submeshes.Reveal);
        }

        // Границы клеток вдоль пути: точки пути и края проёмов.
        private static List<float> AlongBreaks(SplineWallPath path, List<SplineWallHole> holes)
        {
            var breaks = new List<float>(path.Distances);

            foreach (var hole in holes)
            {
                breaks.Add(Mathf.Clamp(hole.AlongFrom, 0f, path.Length));
                breaks.Add(Mathf.Clamp(hole.AlongTo, 0f, path.Length));
            }

            return SortedUnique(breaks);
        }

        // Границы клеток по высоте: низ, верх и края проёмов.
        private static List<float> HeightBreaks(List<SplineWallHole> holes, float bottom, float top)
        {
            var breaks = new List<float> { bottom, top };

            foreach (var hole in holes)
            {
                breaks.Add(Mathf.Clamp(hole.HeightFrom, bottom, top));
                breaks.Add(Mathf.Clamp(hole.HeightTo, bottom, top));
            }

            return SortedUnique(breaks);
        }

        private static List<float> SortedUnique(List<float> values)
        {
            values.Sort();
            var unique = new List<float>(values.Count);

            foreach (var value in values)
                if (unique.Count == 0 || value - unique[^1] > BreakTolerance)
                    unique.Add(value);

            return unique;
        }

        // Клетка — стена, если её середина не попала ни в один проём.
        private static bool[,] SolidCells(List<float> alongBreaks, List<float> heightBreaks, List<SplineWallHole> holes)
        {
            var solid = new bool[alongBreaks.Count - 1, heightBreaks.Count - 1];

            for (var column = 0; column < alongBreaks.Count - 1; column++)
                for (var row = 0; row < heightBreaks.Count - 1; row++)
                {
                    var along = (alongBreaks[column] + alongBreaks[column + 1]) / 2f;
                    var height = (heightBreaks[row] + heightBreaks[row + 1]) / 2f;
                    solid[column, row] = !holes.Exists(hole => hole.Contains(along, height));
                }

            return solid;
        }

        // Подряд идущие клетки стены в столбце — одним прямоугольником: меньше граней.
        private static IEnumerable<(int From, int To)> SolidRuns(bool[,] solid, int column, int rowCount)
        {
            var row = 0;

            while (row < rowCount)
            {
                if (!solid[column, row])
                {
                    row++;
                    continue;
                }

                var start = row;

                while (row < rowCount && solid[column, row])
                    row++;

                yield return (start, row);
            }
        }


        // ── Грани ────────────────────────────────────────────────────────

        // Боковая сторона стены — лицом наружу от линии; развёртка — вдоль пути и по высоте.
        private static void AddSide(SplineWallGeometry geometry, SplineWallPath path, float alongFrom, float alongTo, float heightFrom, float heightTo,
            float sideOffset, bool isRightSide, int submesh)
        {
            var sign = isRightSide ? 1f : -1f;
            var normalFrom = path.SideNormalAt(alongFrom + BreakTolerance) * sign;
            var normalTo = path.SideNormalAt(alongTo - BreakTolerance) * sign;

            geometry.AddQuad(
                new SplineWallVertex(PointAt(path, alongFrom, sideOffset, heightFrom), normalFrom, new Vector2(alongFrom * sign, heightFrom)),
                new SplineWallVertex(PointAt(path, alongTo, sideOffset, heightFrom), normalTo, new Vector2(alongTo * sign, heightFrom)),
                new SplineWallVertex(PointAt(path, alongTo, sideOffset, heightTo), normalTo, new Vector2(alongTo * sign, heightTo)),
                new SplineWallVertex(PointAt(path, alongFrom, sideOffset, heightTo), normalFrom, new Vector2(alongFrom * sign, heightTo)), submesh);
        }

        // Горизонтальная полоса поперёк стены: верх стены, подоконник (смотрит вверх) или верх проёма (вниз).
        private static void AddHorizontal(SplineWallGeometry geometry, SplineWallPath path, float alongFrom, float alongTo, float height,
            float leftOffset, float rightOffset, bool facesUp, int submesh)
        {
            var normal = facesUp ? Vector3.up : Vector3.down;

            geometry.AddQuad(
                new SplineWallVertex(PointAt(path, alongFrom, leftOffset, height), normal, new Vector2(alongFrom, leftOffset)),
                new SplineWallVertex(PointAt(path, alongTo, leftOffset, height), normal, new Vector2(alongTo, leftOffset)),
                new SplineWallVertex(PointAt(path, alongTo, rightOffset, height), normal, new Vector2(alongTo, rightOffset)),
                new SplineWallVertex(PointAt(path, alongFrom, rightOffset, height), normal, new Vector2(alongFrom, rightOffset)), submesh);
        }

        // Поперечные грани на границе столбцов: бока проёмов и торцы открытого пути. Лицом туда, где пусто.
        private static void AddJambs(SplineWallGeometry geometry, SplineWallPath path, bool[,] solid, int boundary, int columnCount, int rowCount,
            List<float> alongBreaks, List<float> heightBreaks, float leftOffset, float rightOffset, int submesh)
        {
            var along = alongBreaks[boundary];
            var isPathStart = boundary == 0;
            var isPathEnd = boundary == columnCount;

            if ((isPathStart || isPathEnd) && path.IsClosed)
                return;

            var row = 0;

            while (row < rowCount)
            {
                var solidBefore = !isPathStart && solid[boundary - 1, row];
                var solidAfter = !isPathEnd && solid[boundary, row];

                if (solidBefore == solidAfter)
                {
                    row++;
                    continue;
                }

                var start = row;

                while (row < rowCount && (!isPathStart && solid[boundary - 1, row]) == solidBefore && (!isPathEnd && solid[boundary, row]) == solidAfter)
                    row++;

                var segment = path.SegmentAt(isPathEnd ? along - BreakTolerance : along + BreakTolerance, out _);
                var normal = path.DirectionOf(segment) * (solidBefore ? 1f : -1f);
                var heightFrom = heightBreaks[start];
                var heightTo = heightBreaks[row];

                geometry.AddQuad(
                    new SplineWallVertex(PointAt(path, along, leftOffset, heightFrom), normal, new Vector2(leftOffset, heightFrom)),
                    new SplineWallVertex(PointAt(path, along, rightOffset, heightFrom), normal, new Vector2(rightOffset, heightFrom)),
                    new SplineWallVertex(PointAt(path, along, rightOffset, heightTo), normal, new Vector2(rightOffset, heightTo)),
                    new SplineWallVertex(PointAt(path, along, leftOffset, heightTo), normal, new Vector2(leftOffset, heightTo)), submesh);
            }
        }

        // Точка стены; высота — над линией в этой точке.
        private static Vector3 PointAt(SplineWallPath path, float along, float sideOffset, float height)
        {
            return path.PointAt(along, sideOffset, height);
        }
    }


    /// <summary>
    /// В какую часть меша (под какой материал) идут грани стены: правая и левая сторона по ходу линии, верх, откосы проёмов и торцы.
    /// </summary>
    public readonly struct SplineWallFaceSubmeshes
    {
        public SplineWallFaceSubmeshes(int right, int left, int top, int reveal)
        {
            Right = right;
            Left = left;
            Top = top;
            Reveal = reveal;
        }


        // Всё — в одну часть: стена с одним материалом.
        public static SplineWallFaceSubmeshes Single => new(0, 0, 0, 0);

        public int Right { get; }
        public int Left { get; }
        public int Top { get; }
        public int Reveal { get; }
    }


    /// <summary>Вершина стены по линии: где, куда смотрит и развёртка.</summary>
    public readonly struct SplineWallVertex
    {
        public SplineWallVertex(Vector3 position, Vector3 normal, Vector2 textureCoordinate)
        {
            Position = position;
            Normal = normal;
            TextureCoordinate = textureCoordinate;
        }


        public Vector3 Position { get; }
        public Vector3 Normal { get; }
        public Vector2 TextureCoordinate { get; }
    }


    /// <summary>
    /// Копилка вершин и треугольников меша стены или здания: четырёхугольники и треугольники лицом по нормали своих вершин,
    /// каждый — в свою часть меша под свой материал.
    /// </summary>
    public sealed class SplineWallGeometry
    {
        private const int MaxVerticesFor16BitIndex = 65000;

        private readonly List<int>[] trianglesBySubmesh;


        public SplineWallGeometry(int submeshCount)
        {
            trianglesBySubmesh = new List<int>[submeshCount];

            for (var index = 0; index < submeshCount; index++)
                trianglesBySubmesh[index] = new List<int>();
        }


        public List<Vector3> Vertices { get; } = new();
        public List<Vector3> Normals { get; } = new();
        public List<Vector2> TextureCoordinates { get; } = new();

        // Габарит накопленного; пусто — нулевой.
        public Bounds Bounds
        {
            get
            {
                if (Vertices.Count == 0)
                    return new Bounds();

                var bounds = new Bounds(Vertices[0], Vector3.zero);

                foreach (var vertex in Vertices)
                    bounds.Encapsulate(vertex);

                return bounds;
            }
        }


        public void AddQuad(SplineWallVertex first, SplineWallVertex second, SplineWallVertex third, SplineWallVertex fourth, int submesh)
        {
            AddTriangle(first, second, third, submesh);
            AddTriangle(first, third, fourth, submesh);
        }


        // Треугольник лицом туда, куда смотрят нормали его вершин.
        public void AddTriangle(SplineWallVertex first, SplineWallVertex second, SplineWallVertex third, int submesh)
        {
            var start = Vertices.Count;

            foreach (var vertex in new[] { first, second, third })
            {
                Vertices.Add(vertex.Position);
                Normals.Add(vertex.Normal);
                TextureCoordinates.Add(vertex.TextureCoordinate);
            }

            var faceNormal = Vector3.Cross(second.Position - first.Position, third.Position - first.Position);
            var wantedNormal = first.Normal + second.Normal + third.Normal;

            if (Vector3.Dot(faceNormal, wantedNormal) >= 0f)
                trianglesBySubmesh[submesh].AddRange(new[] { start, start + 1, start + 2 });
            else
                trianglesBySubmesh[submesh].AddRange(new[] { start, start + 2, start + 1 });
        }


        // Всё накопленное — в меш, по части на материал.
        public void WriteTo(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = Vertices.Count > MaxVerticesFor16BitIndex ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, TextureCoordinates);
            mesh.subMeshCount = trianglesBySubmesh.Length;

            for (var index = 0; index < trianglesBySubmesh.Length; index++)
                mesh.SetTriangles(trianglesBySubmesh[index], index);

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }
    }
}
