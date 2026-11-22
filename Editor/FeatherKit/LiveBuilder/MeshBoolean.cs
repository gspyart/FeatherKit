using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Вершина многоугольника булевой операции: место, нормаль и развёртка текстуры.</summary>
    public struct MeshBooleanVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TextureCoordinate;


        public MeshBooleanVertex(Vector3 position, Vector3 normal, Vector2 textureCoordinate)
        {
            Position = position;
            Normal = normal;
            TextureCoordinate = textureCoordinate;
        }


        // Точка между двумя вершинами — где плоскость режет ребро.
        public MeshBooleanVertex Interpolate(MeshBooleanVertex other, float fraction)
        {
            return new MeshBooleanVertex(Vector3.Lerp(Position, other.Position, fraction), Vector3.Lerp(Normal, other.Normal, fraction).normalized,
                Vector2.Lerp(TextureCoordinate, other.TextureCoordinate, fraction));
        }
    }


    /// <summary>Плоскость многоугольника: нормаль наружу тела и расстояние от нуля вдоль неё.</summary>
    public struct MeshBooleanPlane
    {
        // Ближе этого к плоскости — точка на ней, м. Десятая миллиметра: коробки стоят по целым миллиметрам.
        public const float Tolerance = 0.0001f;

        public Vector3 Normal;
        public float Distance;


        public MeshBooleanPlane(Vector3 normal, float distance)
        {
            Normal = normal;
            Distance = distance;
        }


        // Плоскость по трём точкам; вырожденный треугольник — нет плоскости.
        public static bool TryFromPoints(Vector3 first, Vector3 second, Vector3 third, out MeshBooleanPlane plane)
        {
            var normal = Vector3.Cross(second - first, third - first);
            plane = default;

            if (normal.sqrMagnitude < 1e-12f)
                return false;

            normal.Normalize();
            plane = new MeshBooleanPlane(normal, Vector3.Dot(normal, first));
            return true;
        }


        public MeshBooleanPlane Flipped()
        {
            return new MeshBooleanPlane(-Normal, -Distance);
        }
    }


    /// <summary>
    /// Выпуклый многоугольник тела: вершины по кругу так, что лицо смотрит наружу. Помнит часть меша (материал)
    /// и пришёл ли от выреза — такой станет стенкой дыры.
    /// </summary>
    public sealed class MeshBooleanPolygon
    {
        public MeshBooleanPolygon(List<MeshBooleanVertex> vertices, int submesh, bool isFromCutter, MeshBooleanPlane plane)
        {
            Vertices = vertices;
            Submesh = submesh;
            IsFromCutter = isFromCutter;
            Plane = plane;
        }


        public List<MeshBooleanVertex> Vertices { get; }

        public int Submesh { get; }

        public bool IsFromCutter { get; }

        public MeshBooleanPlane Plane { get; private set; }


        // Многоугольник по вершинам; вырожденный — null.
        public static MeshBooleanPolygon Create(List<MeshBooleanVertex> vertices, int submesh, bool isFromCutter)
        {
            return vertices.Count >= 3 && MeshBooleanPlane.TryFromPoints(vertices[0].Position, vertices[1].Position, vertices[2].Position, out var plane)
                ? new MeshBooleanPolygon(vertices, submesh, isFromCutter, plane)
                : null;
        }


        // Лицом в другую сторону: обход наоборот, нормали наоборот.
        public void Flip()
        {
            Vertices.Reverse();

            for (var index = 0; index < Vertices.Count; index++)
            {
                var vertex = Vertices[index];
                vertex.Normal = -vertex.Normal;
                Vertices[index] = vertex;
            }

            Plane = Plane.Flipped();
        }
    }


    /// <summary>
    /// Дерево разбиения пространства плоскостями многоугольников тела (BSP): по нему видно, что внутри тела, а что снаружи.
    /// Так вычитание и сделано — как в csg.js и Hammer: каждое тело обрезается деревом другого.
    /// </summary>
    public sealed class MeshBooleanTree
    {
        private const int Coplanar = 0;
        private const int Front = 1;
        private const int Back = 2;
        private const int Spanning = 3;

        // Больше стольких свободных узлов и списков не держим — память после очень большого вычитания вернётся.
        private const int MaxFreeTrees = 65536;
        private const int MaxFreeLists = 4096;

        // Список, разросшийся больше этого, в запас не идёт: один огромный разрез иначе держал бы память до перезагрузки скриптов.
        private const int MaxKeptListCapacity = 4096;

        // Узлы и списки шага — повторно: за одну фаску дерево строят и обходят сотни тысяч раз, новые на каждый раз — основная работа сборщика мусора.
        private static readonly Stack<MeshBooleanTree> FreeTrees = new();
        private static readonly Stack<List<MeshBooleanPolygon>> FreeLists = new();

        // Сторона каждой вершины многоугольника при разрезе; разрез не вложенный — один на всех.
        private static int[] vertexTypes = new int[16];

        private readonly List<MeshBooleanPolygon> polygons = new();
        private MeshBooleanPlane? plane;
        private MeshBooleanTree front;
        private MeshBooleanTree back;


        private MeshBooleanTree()
        {
        }


        // ── Свободные узлы ───────────────────────────────────────────────

        // Дерево тела. Когда не нужно — Release.
        public static MeshBooleanTree Rent(List<MeshBooleanPolygon> source)
        {
            var tree = RentNode();
            tree.Build(source);
            return tree;
        }

        private static MeshBooleanTree RentNode()
        {
            return FreeTrees.Count > 0 ? FreeTrees.Pop() : new MeshBooleanTree();
        }


        // Дерево больше не нужно — его узлы пойдут на следующие деревья. Многоугольники остаются у того, кто их забрал.
        public void Release()
        {
            front?.Release();
            back?.Release();
            polygons.Clear();
            plane = null;
            front = null;
            back = null;

            if (FreeTrees.Count < MaxFreeTrees && polygons.Capacity <= MaxKeptListCapacity)
                FreeTrees.Push(this);
        }


        // Разложить многоугольники по дереву: лежащие в плоскости узла — в нём, остальные — вперёд или назад, разрезанные по ней.
        public void Build(List<MeshBooleanPolygon> source)
        {
            if (source.Count == 0)
                return;

            plane ??= source[0].Plane;
            var splitter = plane.Value;
            var frontPolygons = RentList();
            var backPolygons = RentList();

            foreach (var polygon in source)
                Split(splitter, polygon, polygons, polygons, frontPolygons, backPolygons);

            if (frontPolygons.Count > 0)
                (front ??= RentNode()).Build(frontPolygons);

            if (backPolygons.Count > 0)
                (back ??= RentNode()).Build(backPolygons);

            ReturnList(frontPolygons);
            ReturnList(backPolygons);
        }


        // Тело наизнанку: внутри и снаружи меняются местами.
        public void Invert()
        {
            foreach (var polygon in polygons)
                polygon.Flip();

            if (plane.HasValue)
                plane = plane.Value.Flipped();

            front?.Invert();
            back?.Invert();
            (front, back) = (back, front);
        }


        // Оставить от многоугольников только то, что снаружи этого тела.
        public List<MeshBooleanPolygon> ClipPolygons(List<MeshBooleanPolygon> source)
        {
            var clipped = new List<MeshBooleanPolygon>();
            ClipInto(source, clipped);
            return clipped;
        }

        // То, что спереди, — раньше того, что сзади. Спереди пустота — куски сразу в ответ; сзади пустота — куски за ней не нужны, их и не строим.
        private void ClipInto(List<MeshBooleanPolygon> source, List<MeshBooleanPolygon> clipped)
        {
            if (source.Count == 0)
                return;

            if (!plane.HasValue)
            {
                clipped.AddRange(source);
                return;
            }

            var splitter = plane.Value;
            var frontPolygons = front != null ? RentList() : clipped;
            var backPolygons = back != null ? RentList() : null;

            foreach (var polygon in source)
                Split(splitter, polygon, frontPolygons, backPolygons, frontPolygons, backPolygons);

            if (front != null)
            {
                front.ClipInto(frontPolygons, clipped);
                ReturnList(frontPolygons);
            }

            if (back != null)
            {
                back.ClipInto(backPolygons, clipped);
                ReturnList(backPolygons);
            }
        }


        // Обрезать все свои многоугольники деревом другого тела.
        public void ClipTo(MeshBooleanTree other)
        {
            var clipped = RentList();
            other.ClipInto(polygons, clipped);
            polygons.Clear();
            polygons.AddRange(clipped);
            ReturnList(clipped);

            front?.ClipTo(other);
            back?.ClipTo(other);
        }


        // Свои многоугольники, потом всё спереди, потом всё сзади.
        public List<MeshBooleanPolygon> AllPolygons()
        {
            var all = new List<MeshBooleanPolygon>();
            CollectAllInto(all);
            return all;
        }

        private void CollectAllInto(List<MeshBooleanPolygon> all)
        {
            all.AddRange(polygons);
            front?.CollectAllInto(all);
            back?.CollectAllInto(all);
        }


        // ── Разрез ───────────────────────────────────────────────────────

        // Многоугольник по плоскости: целиком спереди, сзади, в ней самой или разрезанный на два. Список сзади null — то, что сзади, не нужно.
        private static void Split(MeshBooleanPlane splitter, MeshBooleanPolygon polygon, List<MeshBooleanPolygon> coplanarFront,
            List<MeshBooleanPolygon> coplanarBack, List<MeshBooleanPolygon> frontPolygons, List<MeshBooleanPolygon> backPolygons)
        {
            var vertices = polygon.Vertices;
            var vertexCount = vertices.Count;

            if (vertexTypes.Length < vertexCount)
                vertexTypes = new int[vertexCount * 2];

            var types = vertexTypes;
            var normal = splitter.Normal;
            var planeDistance = splitter.Distance;
            var polygonType = 0;

            for (var index = 0; index < vertexCount; index++)
            {
                var distance = Vector3.Dot(normal, vertices[index].Position) - planeDistance;
                types[index] = distance < -MeshBooleanPlane.Tolerance ? Back : distance > MeshBooleanPlane.Tolerance ? Front : Coplanar;
                polygonType |= types[index];
            }

            switch (polygonType)
            {
                case Coplanar:
                    (Vector3.Dot(splitter.Normal, polygon.Plane.Normal) > 0f ? coplanarFront : coplanarBack)?.Add(polygon);
                    return;

                case Front:
                    frontPolygons.Add(polygon);
                    return;

                case Back:
                    backPolygons?.Add(polygon);
                    return;
            }

            var isBackNeeded = backPolygons != null;

            var frontVertices = new List<MeshBooleanVertex>(vertices.Count + 1);
            var backVertices = isBackNeeded ? new List<MeshBooleanVertex>(vertices.Count + 1) : null;

            for (var index = 0; index < vertices.Count; index++)
            {
                var next = (index + 1) % vertices.Count;
                var type = types[index];
                var nextType = types[next];
                var vertex = vertices[index];

                if (type != Back)
                    frontVertices.Add(vertex);

                if (type != Front)
                    backVertices?.Add(vertex);

                if ((type | nextType) != Spanning)
                    continue;

                var along = vertices[next].Position - vertex.Position;
                var fraction = (splitter.Distance - Vector3.Dot(splitter.Normal, vertex.Position)) / Vector3.Dot(splitter.Normal, along);
                var cut = vertex.Interpolate(vertices[next], fraction);
                frontVertices.Add(cut);
                backVertices?.Add(cut);
            }

            AddPiece(frontVertices, polygon, frontPolygons);

            if (isBackNeeded)
                AddPiece(backVertices, polygon, backPolygons);
        }

        // Кусок разрезанного многоугольника — с плоскостью целого: у тонкого куска своя посчиталась бы неточно.
        private static void AddPiece(List<MeshBooleanVertex> vertices, MeshBooleanPolygon whole, List<MeshBooleanPolygon> target)
        {
            if (vertices.Count >= 3)
                target.Add(new MeshBooleanPolygon(vertices, whole.Submesh, whole.IsFromCutter, whole.Plane));
        }


        // ── Списки шага ──────────────────────────────────────────────────

        private static List<MeshBooleanPolygon> RentList()
        {
            return FreeLists.Count > 0 ? FreeLists.Pop() : new List<MeshBooleanPolygon>();
        }

        private static void ReturnList(List<MeshBooleanPolygon> list)
        {
            list.Clear();

            if (FreeLists.Count < MaxFreeLists && list.Capacity <= MaxKeptListCapacity)
                FreeLists.Push(list);
        }
    }


    /// <summary>Булево вычитание тел из многоугольников: что останется от первого, если вынуть из него второе. Стенки дыры — от второго.</summary>
    public static class MeshBoolean
    {
        public static List<MeshBooleanPolygon> Subtract(List<MeshBooleanPolygon> solid, List<MeshBooleanPolygon> cutter)
        {
            var solidTree = MeshBooleanTree.Rent(solid);
            var cutterTree = MeshBooleanTree.Rent(cutter);

            solidTree.Invert();
            solidTree.ClipTo(cutterTree);
            cutterTree.ClipTo(solidTree);
            cutterTree.Invert();
            cutterTree.ClipTo(solidTree);
            cutterTree.Invert();
            solidTree.Build(cutterTree.AllPolygons());
            solidTree.Invert();

            var result = solidTree.AllPolygons();
            solidTree.Release();
            cutterTree.Release();
            return result;
        }
    }
}
