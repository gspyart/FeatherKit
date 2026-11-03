using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Фаска рёбер объекта из коробок (<see cref="BoxModel.BevelSize"/>) — не разрушающая, как модификатор Bevel в Blender: считается при каждой сборке.
    /// Выпуклые рёбра наружной оболочки находит по поверхностям расчёта (все грани — по осям объекта): ребро — там, где край одной поверхности
    /// встречает другую, уходящую внутрь объекта. Каждое ребро срезается клином через вычитание (<see cref="MeshSubtraction"/>):
    /// один кусок — плоский срез под 45°, больше — четверть круга. Вогнутые рёбра не трогает; в углу, где сходятся три ребра, срезы встречаются в точке.
    /// Новые грани — материала граней рядом, развёртка в метрах. Края вырезов (<see cref="GeometryCutter"/>) не скругляются — вырезы режут уже готовую фаску.
    /// </summary>
    public static class BoxModelBevel
    {
        // Клин выходит за грани наружу на столько, м: срез вровень с гранью оставил бы плёнку.
        private const float OutsideMargin = 0.01f;

        // Срез меньше этого — уже не фаска, м.
        private const float MinBevel = 0.001f;

        // Ближе этого концы кусков ребра — стоят встык, м.
        private const float JoinTolerance = 0.0005f;


        // Выпуклое ребро оболочки, в осях объекта, м: где угол (поперечные координаты), вдоль какой оси и откуда докуда,
        // куда смотрят две грани ребра и какой срез поместится — не больше половины более узкой из них.
        private readonly struct BoxModelConvexEdge
        {
            public BoxModelConvexEdge(int faceAxis, float faceSign, float faceCoordinate, int sideAxis, float sideSign, float sideCoordinate,
                int alongAxis, float alongFrom, float alongTo, float maxBevel)
            {
                FaceAxis = faceAxis;
                FaceSign = faceSign;
                FaceCoordinate = faceCoordinate;
                SideAxis = sideAxis;
                SideSign = sideSign;
                SideCoordinate = sideCoordinate;
                AlongAxis = alongAxis;
                AlongFrom = alongFrom;
                AlongTo = alongTo;
                MaxBevel = maxBevel;
            }


            // Первая грань ребра: по какой оси смотрит, в плюс или минус, и где её плоскость.
            public int FaceAxis { get; }
            public float FaceSign { get; }
            public float FaceCoordinate { get; }

            // Вторая грань — боком к первой.
            public int SideAxis { get; }
            public float SideSign { get; }
            public float SideCoordinate { get; }

            public int AlongAxis { get; }
            public float AlongFrom { get; }
            public float AlongTo { get; }
            public float MaxBevel { get; }


            // Тот же угол на той же линии — кусок того же ребра.
            public bool IsOnSameLine(BoxModelConvexEdge other)
            {
                return FaceAxis == other.FaceAxis && FaceSign == other.FaceSign && Mathf.Abs(FaceCoordinate - other.FaceCoordinate) < JoinTolerance
                       && SideAxis == other.SideAxis && SideSign == other.SideSign && Mathf.Abs(SideCoordinate - other.SideCoordinate) < JoinTolerance;
            }


            // Ребро, продлённое следующим куском встык.
            public BoxModelConvexEdge JoinedWith(BoxModelConvexEdge next)
            {
                return new BoxModelConvexEdge(FaceAxis, FaceSign, FaceCoordinate, SideAxis, SideSign, SideCoordinate,
                    AlongAxis, AlongFrom, next.AlongTo, Mathf.Min(MaxBevel, next.MaxBevel));
            }
        }


        // Срезать выпуклые рёбра готового меша. size — срез, м; segments — из скольких кусков.
        public static void Apply(Mesh mesh, IReadOnlyList<BoxModelSurface> surfaces, float size, int segments)
        {
            if (size < MinBevel || mesh.vertexCount == 0)
                return;

            var edges = JoinTouching(FindConvexEdges(surfaces));

            if (edges.Count == 0)
                return;

            var polygons = MeshSubtraction.Read(mesh);

            foreach (var edge in edges)
            {
                var bevel = Mathf.Min(size, edge.MaxBevel);

                if (bevel < MinBevel)
                    continue;

                var crossSection = CrossSectionOf(edge, bevel, Mathf.Max(1, segments));
                var edgeToMesh = EdgeToMesh(edge);
                var length = edge.AlongTo - edge.AlongFrom;
                polygons = MeshSubtraction.SubtractNear(polygons, BoundsOf(crossSection, length, edgeToMesh),
                    near => MeshSubtraction.PrismPolygons(crossSection, 0f, length, edgeToMesh, MeshSubtraction.MostCommonSubmesh(near)));
            }

            MeshSubtraction.Write(mesh, polygons, mesh.subMeshCount);
        }


        // ── Рёбра ────────────────────────────────────────────────────────

        // Край поверхности A, к которому вплотную идёт поверхность B, смотрящая вбок от края и уходящая от плоскости A внутрь объекта, — выпуклое ребро.
        // Каждое ребро — один раз: со стороны поверхности с меньшей осью.
        private static List<BoxModelConvexEdge> FindConvexEdges(IReadOnlyList<BoxModelSurface> surfaces)
        {
            var edges = new List<BoxModelConvexEdge>();

            foreach (var surface in surfaces)
            {
                var faceAxis = BoxFaces.Axis(surface.Face);
                var faceSign = BoxFaces.IsPositive(surface.Face) ? 1 : -1;
                var axisU = BoxFaces.SurfaceAxisU(surface.Face);
                var axisV = BoxFaces.SurfaceAxisV(surface.Face);
                var area = surface.Area;

                AddEdges(edges, surfaces, surface, faceAxis, faceSign, axisU, area.xMax, 1, axisV, area.yMin, area.yMax, area.width);
                AddEdges(edges, surfaces, surface, faceAxis, faceSign, axisU, area.xMin, -1, axisV, area.yMin, area.yMax, area.width);
                AddEdges(edges, surfaces, surface, faceAxis, faceSign, axisV, area.yMax, 1, axisU, area.xMin, area.xMax, area.height);
                AddEdges(edges, surfaces, surface, faceAxis, faceSign, axisV, area.yMin, -1, axisU, area.xMin, area.xMax, area.height);
            }

            return edges;
        }

        // Один край поверхности (в миллиметрах): поперёк — по sideAxis на sideValue, вдоль — по alongAxis от alongMin до alongMax.
        private static void AddEdges(List<BoxModelConvexEdge> edges, IReadOnlyList<BoxModelSurface> surfaces, BoxModelSurface surface, int faceAxis, int faceSign,
            int sideAxis, int sideValue, int sideSign, int alongAxis, int alongMin, int alongMax, int surfaceWidth)
        {
            if (faceAxis > sideAxis)
                return;

            foreach (var other in surfaces)
            {
                if (BoxFaces.Axis(other.Face) != sideAxis || BoxFaces.IsPositive(other.Face) != sideSign > 0 || other.Plane != sideValue)
                    continue;

                GetRange(other, faceAxis, out var otherFaceMin, out var otherFaceMax);
                GetRange(other, alongAxis, out var otherAlongMin, out var otherAlongMax);

                var isGoingInside = faceSign > 0 ? otherFaceMax == surface.Plane : otherFaceMin == surface.Plane;
                var from = Mathf.Max(alongMin, otherAlongMin);
                var to = Mathf.Min(alongMax, otherAlongMax);

                if (!isGoingInside || to - from < 1)
                    continue;

                var maxBevel = Mathf.Min(surfaceWidth, otherFaceMax - otherFaceMin) / 2f;
                edges.Add(new BoxModelConvexEdge(faceAxis, faceSign, Meters(surface.Plane), sideAxis, sideSign, Meters(sideValue),
                    alongAxis, Meters(from), Meters(to), Meters(maxBevel)));
            }
        }

        // Протяжённость поверхности по одной из двух её осей, мм.
        private static void GetRange(BoxModelSurface surface, int axis, out int min, out int max)
        {
            var isAxisU = BoxFaces.SurfaceAxisU(surface.Face) == axis;
            min = isAxisU ? surface.Area.xMin : surface.Area.yMin;
            max = isAxisU ? surface.Area.xMax : surface.Area.yMax;
        }


        // Куски одного ребра от разных коробок встык — одно ребро: на стыке двух клиньев вычитание оставило бы щепку.
        private static List<BoxModelConvexEdge> JoinTouching(List<BoxModelConvexEdge> edges)
        {
            edges.Sort((first, second) => first.AlongFrom.CompareTo(second.AlongFrom));
            var joined = new List<BoxModelConvexEdge>();

            foreach (var edge in edges)
            {
                var previous = joined.FindIndex(other => other.IsOnSameLine(edge) && Mathf.Abs(other.AlongTo - edge.AlongFrom) < JoinTolerance);

                if (previous >= 0)
                    joined[previous] = joined[previous].JoinedWith(edge);
                else
                    joined.Add(edge);
            }

            return joined;
        }


        // ── Клин ─────────────────────────────────────────────────────────

        // Сечение клина поперёк ребра: x — вбок (по второй грани), y — по первой, угол ребра в нуле. Обход — против часовой.
        // Внутри объекта — от точки на первой грани до точки на второй: прямо (фаска) или дугой вокруг центра в глубине; снаружи — с запасом.
        // Первая точка — наружный угол: из неё видно всё сечение, крышка клина — веер из неё.
        private static List<Vector2> CrossSectionOf(BoxModelConvexEdge edge, float bevel, int segments)
        {
            var side = edge.SideSign;
            var face = edge.FaceSign;
            var center = new Vector2(-side * bevel, -face * bevel);

            var crossSection = new List<Vector2>
            {
                new(side * OutsideMargin, face * OutsideMargin),
                new(side * OutsideMargin, -face * bevel),
                new(0f, -face * bevel)
            };

            for (var step = 1; step < segments; step++)
            {
                var angle = Mathf.PI / 2f * step / segments;
                crossSection.Add(center + new Vector2(side * bevel * Mathf.Cos(angle), face * bevel * Mathf.Sin(angle)));
            }

            crossSection.Add(new Vector2(-side * bevel, 0f));
            crossSection.Add(new Vector2(-side * bevel, face * OutsideMargin));

            if (PlanPolygons.SignedArea(crossSection) < 0f)
                crossSection.Reverse(1, crossSection.Count - 1);

            return crossSection;
        }


        // Оси сечения в оси объекта: x — по второй грани, y — по первой, z — вдоль ребра от его начала.
        private static Matrix4x4 EdgeToMesh(BoxModelConvexEdge edge)
        {
            var origin = Vector3.zero;
            origin[edge.FaceAxis] = edge.FaceCoordinate;
            origin[edge.SideAxis] = edge.SideCoordinate;
            origin[edge.AlongAxis] = edge.AlongFrom;

            var matrix = Matrix4x4.identity;
            matrix.SetColumn(0, AxisVector(edge.SideAxis));
            matrix.SetColumn(1, AxisVector(edge.FaceAxis));
            matrix.SetColumn(2, AxisVector(edge.AlongAxis));
            matrix.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1f));
            return matrix;
        }

        private static Vector4 AxisVector(int axis)
        {
            var vector = Vector4.zero;
            vector[axis] = 1f;
            return vector;
        }


        private static Bounds BoundsOf(List<Vector2> crossSection, float length, Matrix4x4 edgeToMesh)
        {
            var bounds = new Bounds(edgeToMesh.MultiplyPoint3x4(crossSection[0]), Vector3.zero);

            foreach (var point in crossSection)
            {
                bounds.Encapsulate(edgeToMesh.MultiplyPoint3x4(new Vector3(point.x, point.y, 0f)));
                bounds.Encapsulate(edgeToMesh.MultiplyPoint3x4(new Vector3(point.x, point.y, length)));
            }

            return bounds;
        }


        private static float Meters(float millimeters)
        {
            return millimeters / MillimeterBox.MillimetersPerMeter;
        }
    }
}
