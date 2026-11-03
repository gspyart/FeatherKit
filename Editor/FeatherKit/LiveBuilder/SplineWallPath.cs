using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Путь стены по линии — ломаная по сплайну в осях стены: точки на каждом углу, на плавном изгибе — через шаг;
    /// угол с радиусом (<see cref="LiveBuilderCornerRadii"/>) — дугой, вписанной в него.
    /// Расстояние вдоль пути (u) — главная координата стены: по нему стоят проёмы и идёт развёртка текстуры.
    /// Смещение вбок — по «митре»: на углу обе стороны стены сходятся в одну точку, без щелей и нахлёста.
    /// </summary>
    public sealed class SplineWallPath
    {
        // Изгиб меньше этого — тени плавные, как у кривой; больше — угол, у каждой стороны своя тень, градусы.
        private const float SmoothShadingAngle = 30f;

        // Митра на очень остром углу уходит в бесконечность — дальше этой длины не тянем, во сколько раз от толщины.
        private const float MaxMiterScale = 4f;

        // Ближе этого к точке пути — это сама точка (угол), м.
        private const float CornerTolerance = 0.002f;

        // Из скольких кусков скруглённый угол, когда стена или здание не сказали своё.
        private const int DefaultCornerSegments = 6;

        // Излом меньше этого — угла нет, скруглять нечего, градусы.
        private const float MinRoundedTurnDegrees = 1f;

        private readonly List<Vector3> points = new();
        private readonly List<float> distances = new();


        private SplineWallPath(List<Vector3> pathPoints, bool isClosed)
        {
            IsClosed = isClosed;

            foreach (var point in pathPoints)
            {
                if (points.Count > 0 && Vector3.Distance(points[^1], point) < 0.001f)
                    continue;

                distances.Add(points.Count == 0 ? 0f : distances[^1] + Vector3.Distance(points[^1], point));
                points.Add(point);
            }
        }


        public bool IsClosed { get; }

        public IReadOnlyList<Vector3> Points => points;

        // Расстояние вдоль пути до каждой точки, м.
        public IReadOnlyList<float> Distances => distances;

        public float Length => distances.Count > 0 ? distances[^1] : 0f;

        public int SegmentCount => Mathf.Max(0, points.Count - 1);

        public bool IsUsable => points.Count >= 2 && Length > 0.01f;


        // ── Из сплайна ───────────────────────────────────────────────────

        // Пути всех сплайнов стены: прямой кусок — две точки, изгиб — через шаг curveStep.
        public static List<SplineWallPath> FromWall(SplineWall wall)
        {
            return FromContainer(wall.GetComponent<SplineContainer>(), wall.CurveStep);
        }


        // Пути всех сплайнов контейнера — у стены и у здания по контуру. Скруглённые углы — из скольких кусков у самой стены или здания.
        public static List<SplineWallPath> FromContainer(SplineContainer container, float curveStep)
        {
            var paths = new List<SplineWallPath>();
            var cornerSegments = CornerSegmentsOf(container);

            foreach (var spline in container.Splines)
            {
                var path = FromSpline(spline, curveStep, cornerSegments);

                if (path.IsUsable)
                    paths.Add(path);
            }

            return paths;
        }

        private static int CornerSegmentsOf(SplineContainer container)
        {
            if (container.TryGetComponent<SplineWall>(out var wall))
                return wall.CornerSegments;

            return container.TryGetComponent<SplineBuilding>(out var building) ? building.CornerSegments : DefaultCornerSegments;
        }


        // Путь одного сплайна; у сплайна без длины путь непригодный (IsUsable — false). Угол с радиусом —
        // дуга из cornerSegments кусков.
        public static SplineWallPath FromSpline(Spline spline, float curveStep, int cornerSegments = DefaultCornerSegments)
        {
            var pathPoints = new List<Vector3>();
            var knotPoints = new List<int> { 0 };
            var curveCount = spline.Closed ? spline.Count : spline.Count - 1;

            for (var curveIndex = 0; curveIndex < curveCount; curveIndex++)
            {
                var curve = spline.GetCurve(curveIndex);
                var sampleCount = IsStraight(curve) ? 1 : Mathf.Max(2, Mathf.CeilToInt(CurveUtility.ApproximateLength(curve) / curveStep));

                for (var sample = curveIndex == 0 ? 0 : 1; sample <= sampleCount; sample++)
                    pathPoints.Add((Vector3)CurveUtility.EvaluatePosition(curve, sample / (float)sampleCount));

                // Конец кривой — следующая точка сплайна; у замкнутого последняя кривая кончается в первой точке.
                if (curveIndex + 1 < spline.Count)
                    knotPoints.Add(pathPoints.Count - 1);
            }

            if (LiveBuilderCornerRadii.HashOf(spline) != 0)
                pathPoints = RoundCorners(spline, pathPoints, knotPoints, cornerSegments);

            return new SplineWallPath(pathPoints, spline.Closed);
        }


        // ── Скругление углов ─────────────────────────────────────────────

        // Точки углов с радиусом — дугами. У замкнутого пути последняя точка — та же первая: на время скругления её нет.
        private static List<Vector3> RoundCorners(Spline spline, List<Vector3> pathPoints, List<int> knotPoints, int segments)
        {
            var points = new List<Vector3>(pathPoints);

            if (spline.Closed && points.Count > 1)
                points.RemoveAt(points.Count - 1);

            var count = points.Count;
            var arcs = new Dictionary<int, List<Vector3>>();

            for (var knot = 0; knot < knotPoints.Count; knot++)
            {
                var vertex = knotPoints[knot];
                var radius = LiveBuilderCornerRadii.RadiusOf(spline, knot);
                var isCorner = spline.Closed || vertex > 0 && vertex < count - 1;

                if (radius <= 0f || vertex >= count || !isCorner)
                    continue;

                if (TryBuildArc(points[(vertex - 1 + count) % count], points[vertex], points[(vertex + 1) % count], radius, segments, out var arc))
                    arcs[vertex] = arc;
            }

            var result = new List<Vector3>();

            for (var index = 0; index < count; index++)
            {
                if (arcs.TryGetValue(index, out var arc))
                    result.AddRange(arc);
                else
                    result.Add(points[index]);
            }

            if (spline.Closed && result.Count > 0)
                result.Add(result[0]);

            return result;
        }

        // Дуга, вписанная в угол между отрезками (в плане): касается обоих, радиус — сколько позволяют их половины.
        // Один кусок — срез, фаска. Угол почти прямой (без излома) — дуги нет.
        private static bool TryBuildArc(Vector3 previous, Vector3 corner, Vector3 next, float radius, int segments, out List<Vector3> arc)
        {
            arc = null;
            var incoming = corner - previous;
            var outgoing = next - corner;
            incoming.y = 0f;
            outgoing.y = 0f;

            if (incoming.sqrMagnitude < 1e-8f || outgoing.sqrMagnitude < 1e-8f)
                return false;

            var directionIn = incoming.normalized;
            var directionOut = outgoing.normalized;
            var turn = Vector3.Angle(directionIn, directionOut);

            if (turn < MinRoundedTurnDegrees || turn > 180f - MinRoundedTurnDegrees)
                return false;

            var halfTurnTangent = Mathf.Tan(turn * Mathf.Deg2Rad / 2f);
            var tangentLength = Mathf.Min(radius * halfTurnTangent, incoming.magnitude / 2f, outgoing.magnitude / 2f);
            var fittedRadius = tangentLength / halfTurnTangent;

            // Поворот влево (против часовой сверху) — центр слева от хода, иначе справа.
            var turnSign = Mathf.Sign(Vector3.Cross(directionIn, directionOut).y);
            var start = corner - directionIn * tangentLength;
            var center = start + Vector3.Cross(Vector3.up, directionIn) * (turnSign * fittedRadius);
            var fromCenter = start - center;
            arc = new List<Vector3>();

            for (var step = 0; step <= segments; step++)
            {
                var point = center + Quaternion.AngleAxis(turn * turnSign * step / segments, Vector3.up) * fromCenter;
                point.y = corner.y;
                arc.Add(point);
            }

            return true;
        }

        // Кривая без изгиба: обе ручки лежат на прямой между концами.
        private static bool IsStraight(BezierCurve curve)
        {
            var chord = curve.P3 - curve.P0;
            var chordLength = math.length(chord);

            if (chordLength < 0.001f)
                return true;

            var direction = chord / chordLength;
            return DistanceToLine(curve.P1 - curve.P0, direction) < 0.001f && DistanceToLine(curve.P2 - curve.P0, direction) < 0.001f;
        }

        private static float DistanceToLine(float3 offset, float3 direction)
        {
            return math.length(offset - direction * math.dot(offset, direction));
        }


        // Тот же путь без пропущенных отрезков: подряд идущие оставшиеся — отдельные открытые пути. Ничего не пропущено — он сам.
        public List<SplineWallPath> SplitWithout(System.Predicate<int> isSegmentSkipped)
        {
            var skipped = new bool[SegmentCount];
            var firstSkipped = -1;

            for (var segment = 0; segment < SegmentCount; segment++)
            {
                skipped[segment] = isSegmentSkipped(segment);

                if (skipped[segment] && firstSkipped < 0)
                    firstSkipped = segment;
            }

            if (firstSkipped < 0)
                return new List<SplineWallPath> { this };

            // Замкнутый путь обходим с пропущенного отрезка: цепочка через начало пути не рвётся надвое.
            var parts = new List<SplineWallPath>();
            var partPoints = new List<Vector3>();
            var start = IsClosed ? firstSkipped : 0;

            for (var step = 0; step < SegmentCount; step++)
            {
                var segment = (start + step) % SegmentCount;

                if (skipped[segment])
                {
                    AddPart(parts, partPoints);
                    continue;
                }

                if (partPoints.Count == 0)
                    partPoints.Add(points[segment]);

                partPoints.Add(points[segment + 1]);
            }

            AddPart(parts, partPoints);
            return parts;
        }

        private static void AddPart(List<SplineWallPath> parts, List<Vector3> partPoints)
        {
            if (partPoints.Count >= 2)
            {
                var part = new SplineWallPath(partPoints, false);

                if (part.IsUsable)
                    parts.Add(part);
            }

            partPoints.Clear();
        }


        // ── Точки и стороны ──────────────────────────────────────────────

        // Номер отрезка, на котором лежит расстояние u, и доля внутри него.
        public int SegmentAt(float distance, out float fraction)
        {
            distance = Mathf.Clamp(distance, 0f, Length);
            var segment = 0;

            while (segment < SegmentCount - 1 && distances[segment + 1] < distance)
                segment++;

            var segmentLength = distances[segment + 1] - distances[segment];
            fraction = segmentLength > 0f ? (distance - distances[segment]) / segmentLength : 0f;
            return segment;
        }


        // Точка стены: расстояние вдоль пути, смещение вбок (плюс — вправо по ходу линии), высота над линией.
        // В самой точке пути — по митре, чтобы стороны соседних отрезков сошлись; внутри отрезка — строго поперёк него:
        // иначе на длинной стене стороны съезжали бы друг относительно друга и откос проёма шёл бы наискось.
        public Vector3 PointAt(float distance, float sideOffset, float height)
        {
            var segment = SegmentAt(distance, out var fraction);
            var segmentLength = distances[segment + 1] - distances[segment];
            var isAtStart = fraction * segmentLength < CornerTolerance;
            var isAtEnd = (1f - fraction) * segmentLength < CornerTolerance;

            Vector3 point;

            if (isAtStart)
                point = points[segment] + MiterAt(segment, segment) * sideOffset;
            else if (isAtEnd)
                point = points[segment + 1] + MiterAt(segment + 1, segment) * sideOffset;
            else
                point = Vector3.Lerp(points[segment], points[segment + 1], fraction) + SideOf(segment) * sideOffset;

            return point + Vector3.up * height;
        }


        // Нормаль боковой стороны в точке u: на плавном изгибе — сглаженная, на углу — своего отрезка.
        public Vector3 SideNormalAt(float distance)
        {
            var segment = SegmentAt(distance, out var fraction);
            var start = ShadingNormalAt(segment, segment);
            var end = ShadingNormalAt(segment + 1, segment);
            return Vector3.Lerp(start, end, fraction).normalized;
        }


        // Направление пути на отрезке — вдоль стены, по ходу линии.
        public Vector3 DirectionOf(int segment)
        {
            var direction = points[segment + 1] - points[segment];
            direction.y = 0f;
            return direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
        }


        // Высота линии в точке u — низ стены.
        public float BaseHeightAt(float distance)
        {
            var segment = SegmentAt(distance, out var fraction);
            return Mathf.Lerp(points[segment].y, points[segment + 1].y, fraction);
        }


        // Ближайшая к точке позиция на пути — расстояние u. Высота не важна: ищем по плану.
        public float ClosestDistanceTo(Vector3 localPoint)
        {
            return ClosestDistanceTo(localPoint, out _);
        }


        // То же, и как далеко точка от пути в плане, м.
        public float ClosestDistanceTo(Vector3 localPoint, out float gapToPath)
        {
            var bestDistance = 0f;
            var bestGap = float.MaxValue;

            for (var segment = 0; segment < SegmentCount; segment++)
            {
                var start = Flat(points[segment]);
                var end = Flat(points[segment + 1]);
                var along = end - start;
                var fraction = along.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(Flat(localPoint) - start, along) / along.sqrMagnitude) : 0f;
                var gap = Vector3.Distance(Flat(localPoint), start + along * fraction);

                if (gap >= bestGap)
                    continue;

                bestGap = gap;
                bestDistance = distances[segment] + (distances[segment + 1] - distances[segment]) * fraction;
            }

            gapToPath = bestGap;
            return bestDistance;
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Вправо по ходу отрезка, в плане.
        private Vector3 SideOf(int segment)
        {
            var direction = DirectionOf(segment);
            return new Vector3(direction.z, 0f, -direction.x);
        }

        // Смещение точки вбок на единицу толщины: на углу — по биссектрисе и длиннее, чтобы обе стороны сошлись.
        private Vector3 MiterAt(int pointIndex, int segment)
        {
            if (!TryGetNeighbourSegments(pointIndex, out var previous, out var next))
                return SideOf(segment);

            var average = (SideOf(previous) + SideOf(next)).normalized;
            var cosine = Vector3.Dot(average, SideOf(segment));

            if (average.sqrMagnitude < 0.5f || cosine < 1f / MaxMiterScale)
                return SideOf(segment);

            return average / cosine;
        }

        // Нормаль для теней: на плавном изгибе — средняя соседних отрезков, на углу — нормаль своего отрезка.
        private Vector3 ShadingNormalAt(int pointIndex, int segment)
        {
            if (!TryGetNeighbourSegments(pointIndex, out var previous, out var next))
                return SideOf(segment);

            var isSmooth = Vector3.Angle(DirectionOf(previous), DirectionOf(next)) < SmoothShadingAngle;
            return isSmooth ? (SideOf(previous) + SideOf(next)).normalized : SideOf(segment);
        }

        // Отрезки до и после точки; у концов открытого пути соседа нет.
        private bool TryGetNeighbourSegments(int pointIndex, out int previous, out int next)
        {
            previous = pointIndex - 1;
            next = pointIndex;

            if (IsClosed && (pointIndex == 0 || pointIndex == points.Count - 1))
            {
                previous = SegmentCount - 1;
                next = 0;
                return true;
            }

            return previous >= 0 && next < SegmentCount;
        }

        private static Vector3 Flat(Vector3 point)
        {
            return new Vector3(point.x, 0f, point.z);
        }
    }
}
