using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Отпечатки мешей построек — сверить, что правка модуля не поменяла геометрию эталонного уровня.
    /// - точный: вершины (место, нормаль, развёртка) и треугольники каждой части меша по порядку — совпадает только байт в байт
    /// - по форме: набор треугольников каждой части меша без порядка, места — с точностью 0,1 мм
    /// Строка на постройку: путь от корня уровня, оба отпечатка, вершины, части меша, отпечаток меша коллайдера.
    /// </summary>
    public static class LiveBuilderMeshDigest
    {
        public const string Header = "# path\texact\tshape\tvertices\tsubmeshes\tcollider";

        private const string ColliderSameAsMesh = "same";
        private const string NoCollider = "none";
        private const string EmptyCollider = "empty";

        // Столбцы строки.
        private const int PathColumn = 0;
        private const int ShapeColumn = 2;
        private const int SubmeshesColumn = 4;

        // Шаг сетки, на которую ложатся вершины в отпечатке по форме: 0,1 мм.
        private const float ShapeUnitsPerMeter = 10000f;


        // ── Отпечатки ────────────────────────────────────────────────────

        public static string ExactOf(Mesh mesh)
        {
            var hash = new Hash128();
            hash.Append(mesh.vertexCount);
            hash.Append(mesh.vertices);
            hash.Append(mesh.normals);
            hash.Append(mesh.uv);
            hash.Append(mesh.subMeshCount);

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
                hash.Append(mesh.GetTriangles(submesh));

            return hash.ToString();
        }


        public static string ShapeOf(Mesh mesh)
        {
            var hash = new Hash128();
            var vertices = mesh.vertices;
            hash.Append(mesh.subMeshCount);

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var triangles = mesh.GetTriangles(submesh);
                var shapes = new List<MeshTriangleShapeKey>(triangles.Length / 3);

                for (var index = 0; index < triangles.Length; index += 3)
                    shapes.Add(new MeshTriangleShapeKey(Snap(vertices[triangles[index]]), Snap(vertices[triangles[index + 1]]), Snap(vertices[triangles[index + 2]])));

                shapes.Sort();
                hash.Append(shapes.Count);

                foreach (var shape in shapes)
                    shape.AppendTo(ref hash);
            }

            return hash.ToString();
        }

        private static Vector3Int Snap(Vector3 point)
        {
            return Vector3Int.RoundToInt(point * ShapeUnitsPerMeter);
        }


        // ── Уровень ──────────────────────────────────────────────────────

        // Строки всех мешей под root — по порядку путей.
        public static List<string> DescribeLevel(Transform root)
        {
            var lines = new List<string>();
            var paths = new HashSet<string>();

            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;

                if (mesh == null)
                    continue;

                var path = PathOf(root, filter.transform);

                // Одноимённые соседи — номером, иначе две постройки слились бы в одну строку.
                for (var copy = 2; !paths.Add(path); copy++)
                    path = PathOf(root, filter.transform) + "#" + copy;

                lines.Add(string.Join("\t", path, ExactOf(mesh), ShapeOf(mesh), mesh.vertexCount, mesh.subMeshCount, ColliderOf(filter, mesh)));
            }

            lines.Sort(string.CompareOrdinal);
            return lines;
        }

        private static string PathOf(Transform root, Transform target)
        {
            var path = target.name;

            for (var current = target.parent; current != null && current != root; current = current.parent)
                path = current.name + "/" + path;

            return path;
        }

        private static string ColliderOf(MeshFilter filter, Mesh mesh)
        {
            if (!filter.TryGetComponent<MeshCollider>(out var meshCollider) || !meshCollider.enabled)
                return NoCollider;

            if (meshCollider.sharedMesh == null)
                return EmptyCollider;

            return meshCollider.sharedMesh == mesh ? ColliderSameAsMesh : ExactOf(meshCollider.sharedMesh);
        }


        // ── Сверка ───────────────────────────────────────────────────────

        // Строки по пути; строки-пояснения с «#» пропускаются.
        public static Dictionary<string, string[]> Parse(IEnumerable<string> lines)
        {
            var columnsByPath = new Dictionary<string, string[]>();

            foreach (var line in lines)
            {
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                var columns = line.Split('\t');
                columnsByPath[columns[PathColumn]] = columns;
            }

            return columnsByPath;
        }


        // Чем actual расходится с baseline: точно — все столбцы, по форме — только форма и число частей меша. Пусто — совпали.
        public static List<string> Compare(Dictionary<string, string[]> baseline, Dictionary<string, string[]> actual, bool isExact)
        {
            var differences = new List<string>();
            var paths = new SortedSet<string>(baseline.Keys, StringComparer.Ordinal);
            paths.UnionWith(actual.Keys);

            foreach (var path in paths)
            {
                if (!actual.TryGetValue(path, out var actualColumns))
                    differences.Add("missing: " + path);
                else if (!baseline.TryGetValue(path, out var baselineColumns))
                    differences.Add("new: " + path);
                else if (!AreSame(baselineColumns, actualColumns, isExact))
                    differences.Add("changed: " + path + "\n  was " + string.Join(" ", baselineColumns, 1, baselineColumns.Length - 1)
                                    + "\n  now " + string.Join(" ", actualColumns, 1, actualColumns.Length - 1));
            }

            return differences;
        }

        private static bool AreSame(string[] baselineColumns, string[] actualColumns, bool isExact)
        {
            if (!isExact)
                return baselineColumns[ShapeColumn] == actualColumns[ShapeColumn] && baselineColumns[SubmeshesColumn] == actualColumns[SubmeshesColumn];

            return string.Join("\t", baselineColumns) == string.Join("\t", actualColumns);
        }
    }


    /// <summary>Треугольник для отпечатка по форме: три вершины на сетке 0,1 мм, первая — наименьшая, обход сохранён.</summary>
    public readonly struct MeshTriangleShapeKey : IComparable<MeshTriangleShapeKey>
    {
        private readonly Vector3Int first;
        private readonly Vector3Int second;
        private readonly Vector3Int third;


        public MeshTriangleShapeKey(Vector3Int a, Vector3Int b, Vector3Int c)
        {
            // Поворот по кругу не меняет треугольник и его сторону — начинаем с наименьшей вершины.
            if (CompareVertices(b, a) < 0 && CompareVertices(b, c) < 0)
                (a, b, c) = (b, c, a);
            else if (CompareVertices(c, a) < 0 && CompareVertices(c, b) < 0)
                (a, b, c) = (c, a, b);

            first = a;
            second = b;
            third = c;
        }


        // IComparable
        public int CompareTo(MeshTriangleShapeKey other)
        {
            var result = CompareVertices(first, other.first);

            if (result == 0)
                result = CompareVertices(second, other.second);

            return result != 0 ? result : CompareVertices(third, other.third);
        }


        public void AppendTo(ref Hash128 hash)
        {
            foreach (var vertex in new[] { first, second, third })
            {
                hash.Append(vertex.x);
                hash.Append(vertex.y);
                hash.Append(vertex.z);
            }
        }

        private static int CompareVertices(Vector3Int a, Vector3Int b)
        {
            if (a.x != b.x)
                return a.x.CompareTo(b.x);

            return a.y != b.y ? a.y.CompareTo(b.y) : a.z.CompareTo(b.z);
        }
    }
}
