using System;
using System.Collections.Generic;
using System.Globalization;
using FeatherKit.LiveBuilder;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Отпечаток «по виду» построек: для каждого материала — плоскости граней в мире, их площадь и габарит.
    /// - не зависит от того, на какие меши, части мешей и треугольники разбита геометрия: сверка «выглядит так же» после смены устройства
    /// - только видимое: меши объектов из коробок, стен и частей зданий на включённых объектах с включённым рендерером
    /// - разделы — по корням построек (постройка без постройки над ней) и общий раздел «*» на всё; сверка — с допуском
    /// </summary>
    public static class LiveBuilderLookDigest
    {
        public const string Header = "# section\tmaterial\tnormal\tdistance\tarea\tmin\tmax";
        public const string WholeSection = "*";

        // Пометка корня постройки в файле — строка-пояснение: так видно, сколько корней и какие, даже у пустых.
        private const string RootCommentPrefix = "# root\t";
        private const string NoMaterial = "(none)";

        // Шаг сетки, на которую ложатся нормаль и расстояние плоскости при сборке граней в плоскости: 0,0001 и 0,1 мм.
        private const double PlaneUnits = 10000.0;

        // Треугольник меньше — вырожденный, плоскости у него нет, м².
        private const double MinTriangleArea = 1e-9;

        // Допуски сверки: плоскость, площадь (м² плюс доля), габарит (м).
        private const double PlaneTolerance = 3e-4;
        private const double AreaTolerance = 1e-3;
        private const double AreaRelativeTolerance = 1e-4;
        private const double BoundsTolerance = 1e-3;

        // Наклон нормали от точности float у треугольника далеко от нуля: на 5 км — до 3 см по расстоянию.
        private const double FarNormalTolerance = 1e-5;


        // ── Отпечаток ────────────────────────────────────────────────────

        // Отпечаток всех построек сцены: разделы по корням и общий.
        public static List<string> DescribeScene(Scene scene)
        {
            return Describe(ConstructionRootsOf(scene));
        }


        // Отпечаток построек под container (и его самого, если он постройка).
        public static List<string> Describe(Transform container)
        {
            return Describe(ConstructionRootsUnder(container));
        }


        // Отпечаток по списку корней построек: строки по порядку, первыми — пояснения с корнями.
        public static List<string> Describe(IReadOnlyList<Transform> roots)
        {
            return Describe(roots, null);
        }


        // Отпечаток сцены после обновления старых данных: грани, которые вложенный «Kept From Cuts» и его внешний объект прячут друг в друге,
        // не видны и не считаются — до обновления это был один меш и таких граней не было (<see cref="LiveBuilderLookHiddenFaces"/>).
        public static List<string> DescribeSceneAsSeen(Scene scene)
        {
            return Describe(ConstructionRootsOf(scene), LiveBuilderLookHiddenFaces.OccludersOf);
        }


        // То же для построек под container — содержимого префаба.
        public static List<string> DescribeAsSeen(Transform container)
        {
            return Describe(ConstructionRootsUnder(container), LiveBuilderLookHiddenFaces.OccludersOf);
        }


        // occludersOf — тела, внутри которых грани меша не видны; null — видно всё.
        private static List<string> Describe(IReadOnlyList<Transform> roots, Func<MeshFilter, IReadOnlyList<MeshFilter>> occludersOf)
        {
            var whole = new Dictionary<LookPlaneKey, LookPlaneTotals>();
            var comments = new List<string>();
            var lines = new List<string>();
            var sections = new HashSet<string>();

            foreach (var root in roots)
            {
                var section = PathOf(root);

                // Одноимённые корни — номером, иначе два корня слились бы в один раздел.
                for (var copy = 2; !sections.Add(section); copy++)
                    section = PathOf(root) + "#" + copy;

                comments.Add(RootCommentPrefix + section);
                var planes = new Dictionary<LookPlaneKey, LookPlaneTotals>();

                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!IsVisibleConstructionMesh(filter, out var renderer))
                        continue;

                    var occluders = occludersOf?.Invoke(filter);

                    if (occluders == null || occluders.Count == 0)
                        AddMesh(filter.sharedMesh, renderer, filter.transform.localToWorldMatrix, planes, whole);
                    else
                        foreach (var triangle in LiveBuilderLookHiddenFaces.VisibleTrianglesOf(filter, renderer, occluders))
                            AddTriangle(triangle, planes, whole);
                }

                AddLines(lines, section, planes);
            }

            AddLines(lines, WholeSection, whole);
            lines.Sort(string.CompareOrdinal);
            comments.Sort(string.CompareOrdinal);
            comments.AddRange(lines);
            return comments;
        }


        // Корни построек сцены: объект из коробок, стена или здание, над которыми нет другой постройки.
        public static List<Transform> ConstructionRootsOf(Scene scene)
        {
            var roots = new List<Transform>();

            foreach (var sceneRoot in scene.GetRootGameObjects())
                AddConstructionRoots(sceneRoot.transform, roots);

            return roots;
        }


        // То же под container (и он сам, если он постройка).
        public static List<Transform> ConstructionRootsUnder(Transform container)
        {
            var roots = new List<Transform>();
            AddConstructionRoots(container, roots);
            return roots;
        }

        private static void AddConstructionRoots(Transform node, List<Transform> roots)
        {
            if (IsConstruction(node))
            {
                roots.Add(node);
                return;
            }

            foreach (Transform child in node)
                AddConstructionRoots(child, roots);
        }

        private static bool IsConstruction(Transform node)
        {
            return node.GetComponent<BoxModel>() != null || node.GetComponent<SplineWall>() != null || node.GetComponent<SplineBuilding>() != null
                   || node.GetComponent<LiveBuilderColumn>() != null;
        }


        // Меш объекта из коробок, стены, части здания или колонны, который сейчас виден.
        private static bool IsVisibleConstructionMesh(MeshFilter filter, out MeshRenderer renderer)
        {
            renderer = null;

            if (filter.sharedMesh == null || !filter.gameObject.activeInHierarchy || !filter.TryGetComponent(out renderer) || !renderer.enabled)
                return false;

            return filter.GetComponent<BoxModel>() != null || filter.GetComponent<SplineWall>() != null || filter.GetComponent<SplineBuildingPart>() != null
                   || filter.GetComponent<LiveBuilderColumn>() != null;
        }


        private static void AddMesh(Mesh mesh, MeshRenderer renderer, Matrix4x4 localToWorld, Dictionary<LookPlaneKey, LookPlaneTotals> planes,
            Dictionary<LookPlaneKey, LookPlaneTotals> whole)
        {
            var matrix = ToDouble(localToWorld);
            var localVertices = mesh.vertices;
            var vertices = new double3[localVertices.Length];

            for (var index = 0; index < localVertices.Length; index++)
                vertices[index] = math.mul(matrix, new double4(localVertices[index].x, localVertices[index].y, localVertices[index].z, 1.0)).xyz;

            var materials = renderer.sharedMaterials;

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var material = submesh < materials.Length && materials[submesh] != null ? materials[submesh].name : NoMaterial;
                var triangles = mesh.GetTriangles(submesh);

                for (var index = 0; index + 2 < triangles.Length; index += 3)
                {
                    var a = vertices[triangles[index]];
                    var b = vertices[triangles[index + 1]];
                    var c = vertices[triangles[index + 2]];
                    var cross = math.cross(b - a, c - a);
                    var length = math.length(cross);

                    if (length / 2.0 < MinTriangleArea)
                        continue;

                    var normal = cross / length;
                    var key = LookPlaneKey.Of(material, normal, math.dot(normal, a));
                    AddTriangle(planes, key, a, b, c, length / 2.0);
                    AddTriangle(whole, key, a, b, c, length / 2.0);
                }
            }
        }

        private static void AddTriangle(LiveBuilderLookTriangle triangle, Dictionary<LookPlaneKey, LookPlaneTotals> planes, Dictionary<LookPlaneKey, LookPlaneTotals> whole)
        {
            var cross = math.cross(triangle.B - triangle.A, triangle.C - triangle.A);
            var length = math.length(cross);

            if (length / 2.0 < MinTriangleArea)
                return;

            var normal = cross / length;
            var key = LookPlaneKey.Of(triangle.Material, normal, math.dot(normal, triangle.A));
            AddTriangle(planes, key, triangle.A, triangle.B, triangle.C, length / 2.0);
            AddTriangle(whole, key, triangle.A, triangle.B, triangle.C, length / 2.0);
        }

        private static double4x4 ToDouble(Matrix4x4 matrix)
        {
            return new double4x4(
                matrix.m00, matrix.m01, matrix.m02, matrix.m03,
                matrix.m10, matrix.m11, matrix.m12, matrix.m13,
                matrix.m20, matrix.m21, matrix.m22, matrix.m23,
                matrix.m30, matrix.m31, matrix.m32, matrix.m33);
        }

        private static void AddTriangle(Dictionary<LookPlaneKey, LookPlaneTotals> planes, LookPlaneKey key, double3 a, double3 b, double3 c, double area)
        {
            if (!planes.TryGetValue(key, out var totals))
                planes[key] = totals = new LookPlaneTotals();

            totals.Add(a, b, c, area);
        }


        // Плоскости, попавшие на соседние клетки сетки из-за погрешности, — в одну строку: иначе разбиение на треугольники меняло бы число строк.
        private static void AddLines(List<string> lines, string section, Dictionary<LookPlaneKey, LookPlaneTotals> planes)
        {
            var keys = new List<LookPlaneKey>(planes.Keys);
            keys.Sort();
            var merged = new HashSet<LookPlaneKey>();

            foreach (var key in keys)
            {
                if (merged.Contains(key))
                    continue;

                var totals = planes[key];

                foreach (var neighbour in key.Neighbours())
                    if (!merged.Contains(neighbour) && planes.TryGetValue(neighbour, out var neighbourTotals) && neighbour.CompareTo(key) > 0)
                    {
                        totals.Merge(neighbourTotals);
                        merged.Add(neighbour);
                    }

                lines.Add(string.Join("\t", section, key.Material, key.NormalText(), key.DistanceText(), totals.AreaText(), totals.MinText(), totals.MaxText()));
            }
        }

        private static string PathOf(Transform target)
        {
            var path = target.name;

            for (var current = target.parent; current != null; current = current.parent)
                path = current.name + "/" + path;

            return path;
        }


        // ── Сверка ───────────────────────────────────────────────────────

        // Только строки раздела section, без пояснений: сверить одно общее «*», когда корни построек переименованы или переложены.
        public static List<string> SectionOf(IEnumerable<string> lines, string section)
        {
            var sectionLines = new List<string>();
            var prefix = section + "\t";

            foreach (var line in lines)
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                    sectionLines.Add(line);

            return sectionLines;
        }


        // Чем actual расходится с baseline по виду; пусто — выглядят одинаково. Пояснения с «#» не сверяются.
        public static List<string> Compare(IEnumerable<string> baseline, IEnumerable<string> actual)
        {
            var baselineFaces = Parse(baseline);
            var actualFaces = Parse(actual);
            var differences = new List<string>();
            var groups = new SortedSet<string>(baselineFaces.Keys, StringComparer.Ordinal);
            groups.UnionWith(actualFaces.Keys);

            foreach (var group in groups)
            {
                baselineFaces.TryGetValue(group, out var expected);
                actualFaces.TryGetValue(group, out var found);
                CompareGroup(group, expected ?? new List<LookFace>(), found ?? new List<LookFace>(), differences);
            }

            return differences;
        }

        // Пара «раздел, материал» → его плоскости.
        private static Dictionary<string, List<LookFace>> Parse(IEnumerable<string> lines)
        {
            var faces = new Dictionary<string, List<LookFace>>();

            foreach (var line in lines)
            {
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                var face = LookFace.Parse(line);

                if (!faces.TryGetValue(face.Group, out var list))
                    faces[face.Group] = list = new List<LookFace>();

                list.Add(face);
            }

            return faces;
        }

        // Плоскость к плоскости — ближайшая в допуске; не нашлась пара — «пропала» или «новая», нашлась — сверка площади и габарита.
        private static void CompareGroup(string group, List<LookFace> expected, List<LookFace> found, List<string> differences)
        {
            var unmatched = new List<LookFace>(found);

            foreach (var face in expected)
            {
                var match = unmatched.FindIndex(candidate => candidate.IsSamePlane(face));

                if (match < 0)
                {
                    differences.Add("missing: " + group + " " + face.PlaneText);
                    continue;
                }

                var other = unmatched[match];
                unmatched.RemoveAt(match);

                if (!other.HasSameArea(face) || !other.HasSameBounds(face))
                    differences.Add("changed: " + group + " " + face.PlaneText + "\n  was " + face.ExtentText + "\n  now " + other.ExtentText);
            }

            foreach (var face in unmatched)
                differences.Add("new: " + group + " " + face.PlaneText + " " + face.ExtentText);
        }


        // ── Строка отпечатка ─────────────────────────────────────────────

        /// <summary>Плоскость граней одного материала: нормаль и расстояние, округлённые на сетку 0,0001.</summary>
        private readonly struct LookPlaneKey : IComparable<LookPlaneKey>, IEquatable<LookPlaneKey>
        {
            private readonly long normalX;
            private readonly long normalY;
            private readonly long normalZ;
            private readonly long distance;


            private LookPlaneKey(string material, long normalX, long normalY, long normalZ, long distance)
            {
                Material = material;
                this.normalX = normalX;
                this.normalY = normalY;
                this.normalZ = normalZ;
                this.distance = distance;
            }


            public string Material { get; }


            public static LookPlaneKey Of(string material, double3 normal, double distance)
            {
                return new LookPlaneKey(material, Snap(normal.x), Snap(normal.y), Snap(normal.z), Snap(distance));
            }

            private static long Snap(double value)
            {
                return (long)Math.Round(value * PlaneUnits, MidpointRounding.AwayFromZero);
            }


            // Соседние клетки сетки по всем четырём числам.
            public IEnumerable<LookPlaneKey> Neighbours()
            {
                for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                for (var z = -1; z <= 1; z++)
                for (var d = -1; d <= 1; d++)
                    if (x != 0 || y != 0 || z != 0 || d != 0)
                        yield return new LookPlaneKey(Material, normalX + x, normalY + y, normalZ + z, distance + d);
            }


            public string NormalText()
            {
                return Format(normalX / PlaneUnits) + " " + Format(normalY / PlaneUnits) + " " + Format(normalZ / PlaneUnits);
            }

            public string DistanceText()
            {
                return Format(distance / PlaneUnits);
            }


            // IComparable
            public int CompareTo(LookPlaneKey other)
            {
                var result = string.CompareOrdinal(Material, other.Material);

                if (result == 0)
                    result = normalX.CompareTo(other.normalX);

                if (result == 0)
                    result = normalY.CompareTo(other.normalY);

                if (result == 0)
                    result = normalZ.CompareTo(other.normalZ);

                return result != 0 ? result : distance.CompareTo(other.distance);
            }


            // IEquatable
            public bool Equals(LookPlaneKey other)
            {
                return CompareTo(other) == 0;
            }

            public override bool Equals(object other)
            {
                return other is LookPlaneKey key && Equals(key);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Material, normalX, normalY, normalZ, distance);
            }
        }


        /// <summary>Сколько граней на плоскости: площадь и габарит в мире.</summary>
        private sealed class LookPlaneTotals
        {
            private double area;
            private double3 min = new(double.MaxValue);
            private double3 max = new(double.MinValue);


            public void Add(double3 a, double3 b, double3 c, double triangleArea)
            {
                area += triangleArea;
                min = math.min(min, math.min(a, math.min(b, c)));
                max = math.max(max, math.max(a, math.max(b, c)));
            }


            public void Merge(LookPlaneTotals other)
            {
                area += other.area;
                min = math.min(min, other.min);
                max = math.max(max, other.max);
            }


            public string AreaText()
            {
                return Format(area);
            }

            public string MinText()
            {
                return FormatPoint(min);
            }

            public string MaxText()
            {
                return FormatPoint(max);
            }
        }


        /// <summary>Строка отпечатка, прочитанная для сверки.</summary>
        private sealed class LookFace
        {
            private double3 normal;
            private double distance;
            private double area;
            private double3 min;
            private double3 max;


            public string Group { get; private set; }
            public string PlaneText { get; private set; }
            public string ExtentText { get; private set; }


            public static LookFace Parse(string line)
            {
                var columns = line.Split('\t');

                return new LookFace
                {
                    Group = columns[0] + " [" + columns[1] + "]",
                    normal = ParsePoint(columns[2]),
                    distance = ParseNumber(columns[3]),
                    area = ParseNumber(columns[4]),
                    min = ParsePoint(columns[5]),
                    max = ParsePoint(columns[6]),
                    PlaneText = "plane " + columns[2] + " / " + columns[3],
                    ExtentText = "area " + columns[4] + ", min " + columns[5] + ", max " + columns[6]
                };
            }


            // Далеко от нуля мира (5 км) крошечный наклон нормали треугольника сдвигает расстояние на сантиметры, а грани те же:
            // тогда плоскость та же, если совпал габарит, а расстояние — в допуске на наклон.
            public bool IsSamePlane(LookFace other)
            {
                if (math.cmax(math.abs(normal - other.normal)) > PlaneTolerance)
                    return false;

                var distanceDifference = Math.Abs(distance - other.distance);

                if (distanceDifference <= PlaneTolerance)
                    return true;

                var reach = math.cmax(math.max(math.abs(min), math.abs(max)));
                return HasSameBounds(other) && distanceDifference <= PlaneTolerance + reach * FarNormalTolerance;
            }

            public bool HasSameArea(LookFace other)
            {
                return Math.Abs(area - other.area) <= AreaTolerance + AreaRelativeTolerance * Math.Max(area, other.area);
            }

            public bool HasSameBounds(LookFace other)
            {
                return math.cmax(math.abs(min - other.min)) <= BoundsTolerance && math.cmax(math.abs(max - other.max)) <= BoundsTolerance;
            }
        }


        // ── Числа ────────────────────────────────────────────────────────

        private static string Format(double value)
        {
            var text = value.ToString("0.0000", CultureInfo.InvariantCulture);
            return text == "-0.0000" ? "0.0000" : text;
        }

        private static string FormatPoint(double3 point)
        {
            return Format(point.x) + " " + Format(point.y) + " " + Format(point.z);
        }

        private static double ParseNumber(string text)
        {
            return double.Parse(text, CultureInfo.InvariantCulture);
        }

        private static double3 ParsePoint(string text)
        {
            var parts = text.Split(' ');
            return new double3(ParseNumber(parts[0]), ParseNumber(parts[1]), ParseNumber(parts[2]));
        }
    }
}
