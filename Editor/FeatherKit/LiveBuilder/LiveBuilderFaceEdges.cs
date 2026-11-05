using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Края плоской грани, на которой рисуют: все треугольники меша в той же плоскости и их наружные рёбра — края и углы стены, края проёмов.
    /// Чтобы угол прямоугольника прилипал к краю грани, а сетка вдоль грани шла от её края, а не от нуля мира: стена, поставленная
    /// по мелкой сетке, иначе с крупной сеткой «не ловится». Годится любой меш — стена по линии, здание, объект из коробок, пропс.
    /// Учитываются рёбра вдоль осей грани («вправо» и «вверх»); координаты — проекции точек мира на эти оси.
    /// </summary>
    public sealed class LiveBuilderFaceEdges
    {
        // Треугольник той же грани: нормаль почти та же (косинус), вершины не дальше этого от плоскости, м.
        private const float SameNormalMinCosine = 0.999f;
        private const float SamePlaneTolerance = 0.002f;

        // Ребро «вдоль оси», если поперёк оси уходит не больше этой доли своей длины.
        private const float AxisParallelTolerance = 0.01f;

        // Шаг от ребра в обе стороны при проверке, наружное ли оно: с одной стороны грань, с другой нет, м.
        private const float EdgeSideProbe = 0.002f;

        // Точность, с которой совпадают вершины соседних треугольников, — миллиметр.
        private const float VertexWeldPrecision = 1000f;

        // Грань из стольких треугольников уже не стена — края не ищем: долго.
        private const int MaxFaceTriangles = 2000;

        // Сетка мелкого шага рядом — к краю всё равно липнет хотя бы на столько точек экрана.
        private const float SnapScreenPixels = 10f;

        // Сколько точек экрана в размере ручки Unity (HandleUtility.GetHandleSize).
        private const float HandleSizePixels = 80f;

        // Сколько помнится последний ответ, с: меш могли пересобрать на месте, тем же объектом с тем же числом вершин.
        private const double RememberedEdgesLifetime = 0.5;

        public static readonly LiveBuilderFaceEdges None = new();

        // Меш и треугольники — общие списки на все поиски: «Проём» ищет края на каждое движение мыши.
        private static readonly List<Vector3> VerticesScratch = new();
        private static readonly List<int> TrianglesScratch = new();

        // Последний поиск: какая грань и что нашлось (<see cref="Find"/>).
        private static FaceEdgesKey lastKey;
        private static LiveBuilderFaceEdges lastEdges;
        private static double lastFindTime;

        // Рёбра вдоль «вверх» (по ним прилипает координата «вправо») и вдоль «вправо» (по ним — «вверх»).
        private readonly List<FaceEdgeLine> edgesAlongUp = new();
        private readonly List<FaceEdgeLine> edgesAlongRight = new();


        // Ребро грани вдоль одной из её осей: где оно поперёк оси и от-до вдоль неё, м.
        private readonly struct FaceEdgeLine
        {
            public FaceEdgeLine(float position, float from, float to)
            {
                Position = position;
                From = Mathf.Min(from, to);
                To = Mathf.Max(from, to);
            }

            public float Position { get; }
            public float From { get; }
            public float To { get; }
        }


        // Какая грань: меш, где он стоит, плоскость с точностью до миллиметра и ось «вправо».
        private readonly struct FaceEdgesKey : System.IEquatable<FaceEdgesKey>
        {
            private readonly Mesh mesh;
            private readonly int vertexCount;
            private readonly Matrix4x4 meshToWorld;
            private readonly Vector3Int planeNormal;
            private readonly int planeDistance;
            private readonly Vector3Int right;

            public FaceEdgesKey(Mesh mesh, Matrix4x4 meshToWorld, Plane plane, Vector3 right)
            {
                this.mesh = mesh;
                vertexCount = mesh.vertexCount;
                this.meshToWorld = meshToWorld;
                planeNormal = Vector3Int.RoundToInt(plane.normal * VertexWeldPrecision);
                planeDistance = Mathf.RoundToInt(plane.distance * VertexWeldPrecision);
                this.right = Vector3Int.RoundToInt(right * VertexWeldPrecision);
            }

            // IEquatable: та же грань того же меша на том же месте.
            public bool Equals(FaceEdgesKey other)
            {
                return ReferenceEquals(mesh, other.mesh) && vertexCount == other.vertexCount && meshToWorld == other.meshToWorld
                       && planeNormal == other.planeNormal && planeDistance == other.planeDistance && right == other.right;
            }
        }


        public bool HasEdges => edgesAlongUp.Count > 0 || edgesAlongRight.Count > 0;


        // ── Поиск ────────────────────────────────────────────────────────

        // Края грани меша под точкой: у коллайдера — его меша, иначе меша на том же объекте. Меша нет или он не читается — None.
        // Та же грань, что в прошлый раз, — прежний ответ: «Проём» спрашивает на каждое движение мыши, а поиск обходит весь меш.
        public static LiveBuilderFaceEdges Find(Component meshOwner, Vector3 point, Vector3 normal, Vector3 right, Vector3 up)
        {
            var mesh = MeshOf(meshOwner);

            if (mesh == null || !mesh.isReadable)
                return None;

            var meshToWorld = meshOwner.transform.localToWorldMatrix;
            var plane = new Plane(normal, point);
            var key = new FaceEdgesKey(mesh, meshToWorld, plane, right);

            if (lastEdges != null && key.Equals(lastKey) && UnityEditor.EditorApplication.timeSinceStartup - lastFindTime < RememberedEdgesLifetime)
                return lastEdges;

            var faceTriangles = CollectFaceTriangles(mesh, meshToWorld, plane, right, up);
            lastEdges = faceTriangles == null ? None : FromTriangles(faceTriangles);
            lastKey = key;
            lastFindTime = UnityEditor.EditorApplication.timeSinceStartup;
            return lastEdges;
        }

        private static Mesh MeshOf(Component meshOwner)
        {
            if (meshOwner == null)
                return null;

            if (meshOwner is MeshCollider meshCollider)
                return meshCollider.sharedMesh;

            return meshOwner.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
        }

        // Треугольники меша в плоскости грани и с той же нормалью — в осях грани. Слишком много — null.
        private static List<Vector2[]> CollectFaceTriangles(Mesh mesh, Matrix4x4 meshToWorld, Plane plane, Vector3 right, Vector3 up)
        {
            var faceTriangles = new List<Vector2[]>();
            mesh.GetVertices(VerticesScratch);

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                mesh.GetTriangles(TrianglesScratch, submesh);

                for (var index = 0; index + 2 < TrianglesScratch.Count; index += 3)
                {
                    var first = meshToWorld.MultiplyPoint3x4(VerticesScratch[TrianglesScratch[index]]);
                    var second = meshToWorld.MultiplyPoint3x4(VerticesScratch[TrianglesScratch[index + 1]]);
                    var third = meshToWorld.MultiplyPoint3x4(VerticesScratch[TrianglesScratch[index + 2]]);

                    if (!IsOnFace(first, second, third, plane))
                        continue;

                    if (faceTriangles.Count >= MaxFaceTriangles)
                        return null;

                    faceTriangles.Add(new[] { OnFace(first, right, up), OnFace(second, right, up), OnFace(third, right, up) });
                }
            }

            return faceTriangles;
        }

        private static bool IsOnFace(Vector3 first, Vector3 second, Vector3 third, Plane plane)
        {
            var cross = Vector3.Cross(second - first, third - first);

            return cross.sqrMagnitude > 1e-12f && Vector3.Dot(cross.normalized, plane.normal) > SameNormalMinCosine
                                               && Mathf.Abs(plane.GetDistanceToPoint(first)) < SamePlaneTolerance;
        }

        private static Vector2 OnFace(Vector3 worldPoint, Vector3 right, Vector3 up)
        {
            return new Vector2(Vector3.Dot(worldPoint, right), Vector3.Dot(worldPoint, up));
        }


        // Наружные рёбра вдоль осей: ребро двух треугольников — внутри грани; ребро одного — наружное, если по одну сторону от него грань,
        // а по другую нет (иначе это стык, где к ребру сбоку примыкает вершина соседнего).
        private static LiveBuilderFaceEdges FromTriangles(List<Vector2[]> faceTriangles)
        {
            var useCounts = new Dictionary<(Vector2Int, Vector2Int), int>();
            var segments = new Dictionary<(Vector2Int, Vector2Int), (Vector2, Vector2)>();

            foreach (var triangle in faceTriangles)
                for (var corner = 0; corner < 3; corner++)
                {
                    var start = triangle[corner];
                    var end = triangle[(corner + 1) % 3];
                    var key = EdgeKey(start, end);
                    useCounts[key] = useCounts.TryGetValue(key, out var count) ? count + 1 : 1;
                    segments[key] = (start, end);
                }

            var edges = new LiveBuilderFaceEdges();

            foreach (var (key, count) in useCounts)
            {
                if (count != 1)
                    continue;

                var (start, end) = segments[key];
                var delta = end - start;
                var length = delta.magnitude;

                if (length < 0.001f)
                    continue;

                var isAlongUp = Mathf.Abs(delta.x) <= length * AxisParallelTolerance;
                var isAlongRight = Mathf.Abs(delta.y) <= length * AxisParallelTolerance;

                if ((!isAlongUp && !isAlongRight) || !IsOutline(start, end, faceTriangles))
                    continue;

                if (isAlongUp)
                    edges.edgesAlongUp.Add(new FaceEdgeLine((start.x + end.x) / 2f, start.y, end.y));
                else
                    edges.edgesAlongRight.Add(new FaceEdgeLine((start.y + end.y) / 2f, start.x, end.x));
            }

            return edges;
        }

        private static (Vector2Int, Vector2Int) EdgeKey(Vector2 start, Vector2 end)
        {
            var first = Vector2Int.RoundToInt(start * VertexWeldPrecision);
            var second = Vector2Int.RoundToInt(end * VertexWeldPrecision);
            return first.x < second.x || (first.x == second.x && first.y < second.y) ? (first, second) : (second, first);
        }

        // Хоть в одном месте ребра (четверть, середина, три четверти) грань только с одной стороны.
        private static bool IsOutline(Vector2 start, Vector2 end, List<Vector2[]> faceTriangles)
        {
            var delta = end - start;
            var side = new Vector2(-delta.y, delta.x).normalized * EdgeSideProbe;

            for (var sample = 1; sample <= 3; sample++)
            {
                var point = Vector2.Lerp(start, end, sample / 4f);

                if (IsInsideFace(point + side, faceTriangles) != IsInsideFace(point - side, faceTriangles))
                    return true;
            }

            return false;
        }

        private static bool IsInsideFace(Vector2 point, List<Vector2[]> faceTriangles)
        {
            foreach (var triangle in faceTriangles)
                if (IsInsideTriangle(point, triangle[0], triangle[1], triangle[2]))
                    return true;

            return false;
        }

        private static bool IsInsideTriangle(Vector2 point, Vector2 first, Vector2 second, Vector2 third)
        {
            var firstSide = Cross(second - first, point - first);
            var secondSide = Cross(third - second, point - second);
            var thirdSide = Cross(first - third, point - third);
            var hasNegative = firstSide < 0f || secondSide < 0f || thirdSide < 0f;
            var hasPositive = firstSide > 0f || secondSide > 0f || thirdSide > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float Cross(Vector2 first, Vector2 second)
        {
            return first.x * second.y - first.y * second.x;
        }


        // ── Прилипание ───────────────────────────────────────────────────

        // Насколько близко к краю угол прилипает к нему в любом случае, м: 10 точек экрана у этой точки. Не в окне сцены (проверки) — 0.
        public static float SnapToleranceAt(Vector3 worldPoint)
        {
            var isInSceneView = Event.current != null && UnityEditor.SceneView.currentDrawingSceneView != null && Camera.current != null;
            return isInSceneView ? UnityEditor.HandleUtility.GetHandleSize(worldPoint) * (SnapScreenPixels / HandleSizePixels) : 0f;
        }


        // Ближайший к точке край поперёк оси (0 — координата «вправо», 1 — «вверх»), если точка по другой оси — в его длине с запасом extentMargin.
        public bool TryFindNearestEdge(int axis, Vector2 point, float extentMargin, out float edge)
        {
            var lines = axis == 0 ? edgesAlongUp : edgesAlongRight;
            var coordinate = point[axis];
            var extentCoordinate = point[1 - axis];
            var nearestGap = float.MaxValue;
            edge = 0f;

            foreach (var line in lines)
            {
                var gap = Mathf.Abs(line.Position - coordinate);

                if (gap >= nearestGap || extentCoordinate < line.From - extentMargin || extentCoordinate > line.To + extentMargin)
                    continue;

                nearestGap = gap;
                edge = line.Position;
            }

            return nearestGap < float.MaxValue;
        }


        // Координата точки по оси, прилипшая к тому, что ближе: к краю грани или к линии сетки (от gridOrigin с шагом step).
        // Край ближе edgeTolerance — всегда к нему.
        public float Snap(int axis, Vector2 point, float gridOrigin, float step, float edgeTolerance, out bool isOnEdge)
        {
            var coordinate = point[axis];
            var onGrid = gridOrigin + Mathf.Round((coordinate - gridOrigin) / step) * step;

            if (TryFindNearestEdge(axis, point, edgeTolerance, out var edge))
            {
                var edgeGap = Mathf.Abs(coordinate - edge);
                isOnEdge = edgeGap <= edgeTolerance || edgeGap <= Mathf.Abs(coordinate - onGrid) + 0.0001f;

                if (isOnEdge)
                    return edge;
            }

            isOnEdge = false;
            return onGrid;
        }
    }
}
