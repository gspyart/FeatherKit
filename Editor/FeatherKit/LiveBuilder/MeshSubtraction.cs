using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Вычитание тел из готового меша постройки — общее у вырезов (<see cref="GeometryCutterApplier"/>) и фаски рёбер (<see cref="BoxModelBevel"/>):
    /// меш ⇄ многоугольники, вычитание (<see cref="MeshBoolean"/>) только для треугольников рядом с телом (<see cref="MeshSubtractionPolygons"/>) — остальные не трогаются,
    /// новые грани (стенки дыры, срез ребра) — с развёрткой в метрах на ближайшую плоскость, как у всех построек.
    /// Само тело обычно призма — контур, протянутый по оси: её тоже строит здесь.
    /// </summary>
    public static class MeshSubtraction
    {
        // Больше вершин — нужен 32-битный индекс.
        private const int MaxVerticesFor16BitIndex = 65000;

        // Буферы на один вызов — общие: их зовут на каждое ребро фаски, а новые списки на каждый раз — работа сборщику мусора.
        private static readonly List<Vector3> SolidPoints = new();
        private static readonly List<Vector2> FanTriangle = new();
        private static int[] countsBySubmesh = new int[8];


        // ── Вычитание ────────────────────────────────────────────────────

        // Часть меша, которой среди многоугольников больше всего: новые грани — как то, что вокруг.
        public static int MostCommonSubmesh(List<MeshBooleanPolygon> polygons)
        {
            var submeshCount = 0;

            foreach (var polygon in polygons)
                submeshCount = Mathf.Max(submeshCount, polygon.Submesh + 1);

            if (countsBySubmesh.Length < submeshCount)
                countsBySubmesh = new int[submeshCount];

            var best = polygons[0].Submesh;

            foreach (var polygon in polygons)
                if (++countsBySubmesh[polygon.Submesh] > countsBySubmesh[best])
                    best = polygon.Submesh;

            System.Array.Clear(countsBySubmesh, 0, submeshCount);
            return best;
        }


        // Новая грань: нормаль — по её плоскости, развёртка — в метрах на плоскость, ближайшую к ней.
        public static void UnwrapFlat(MeshBooleanPolygon polygon)
        {
            var normal = polygon.Plane.Normal;
            var absolute = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));

            for (var index = 0; index < polygon.Vertices.Count; index++)
            {
                var vertex = polygon.Vertices[index];
                var position = vertex.Position;
                vertex.Normal = normal;
                vertex.TextureCoordinate = absolute.x >= absolute.y && absolute.x >= absolute.z ? new Vector2(position.z, position.y)
                    : absolute.y >= absolute.z ? new Vector2(position.x, position.z)
                    : new Vector2(position.x, position.y);
                polygon.Vertices[index] = vertex;
            }
        }

        public static Bounds BoundsOf(MeshBooleanPolygon polygon)
        {
            var bounds = new Bounds(polygon.Vertices[0].Position, Vector3.zero);

            for (var index = 1; index < polygon.Vertices.Count; index++)
                bounds.Encapsulate(polygon.Vertices[index].Position);

            return bounds;
        }


        // ── Тело-призма ──────────────────────────────────────────────────

        // Контур, протянутый по Z от fromZ до toZ, — многоугольниками лицом наружу, в осях меша. Контур — против часовой стрелки с +Z;
        // невыпуклый должен быть виден целиком из первой точки: крышка тогда — веер из неё.
        public static List<MeshBooleanPolygon> PrismPolygons(List<Vector2> outline, float fromZ, float toZ, Matrix4x4 prismToMesh, int submesh)
        {
            var polygons = new List<MeshBooleanPolygon>(outline.Count + 2);
            var isMirrored = prismToMesh.determinant < 0f;

            if (IsConvex(outline))
            {
                AddCaps(polygons, outline, fromZ, toZ, prismToMesh, submesh, isMirrored);
            }
            else
            {
                for (var index = 1; index + 1 < outline.Count; index++)
                {
                    FanTriangle.Clear();
                    FanTriangle.Add(outline[0]);
                    FanTriangle.Add(outline[index]);
                    FanTriangle.Add(outline[index + 1]);
                    AddCaps(polygons, FanTriangle, fromZ, toZ, prismToMesh, submesh, isMirrored);
                }
            }

            for (var index = 0; index < outline.Count; index++)
            {
                var start = outline[index];
                var end = outline[(index + 1) % outline.Count];
                SolidPoints.Clear();
                SolidPoints.Add(new Vector3(start.x, start.y, fromZ));
                SolidPoints.Add(new Vector3(end.x, end.y, fromZ));
                SolidPoints.Add(new Vector3(end.x, end.y, toZ));
                SolidPoints.Add(new Vector3(start.x, start.y, toZ));
                AddSolidPolygon(polygons, SolidPoints, prismToMesh, submesh, isMirrored);
            }

            return polygons;
        }

        // Передняя крышка — по обходу контура, задняя — наоборот.
        private static void AddCaps(List<MeshBooleanPolygon> polygons, List<Vector2> cap, float fromZ, float toZ, Matrix4x4 prismToMesh, int submesh, bool isMirrored)
        {
            SolidPoints.Clear();

            foreach (var point in cap)
                SolidPoints.Add(new Vector3(point.x, point.y, toZ));

            AddSolidPolygon(polygons, SolidPoints, prismToMesh, submesh, isMirrored);
            SolidPoints.Clear();

            foreach (var point in cap)
                SolidPoints.Add(new Vector3(point.x, point.y, fromZ));

            SolidPoints.Reverse();
            AddSolidPolygon(polygons, SolidPoints, prismToMesh, submesh, isMirrored);
        }

        private static bool IsConvex(List<Vector2> outline)
        {
            for (var index = 0; index < outline.Count; index++)
            {
                var previous = outline[(index + outline.Count - 1) % outline.Count];
                var current = outline[index];
                var next = outline[(index + 1) % outline.Count];

                if (PlanPolygons.Cross(current - previous, next - current) < -1e-7f)
                    return false;
            }

            return true;
        }

        // Грань тела: точки — в осях призмы; зеркальный перенос выворачивает обход — разворачиваем обратно. localPoints портятся.
        private static void AddSolidPolygon(List<MeshBooleanPolygon> polygons, List<Vector3> localPoints, Matrix4x4 prismToMesh, int submesh, bool isMirrored)
        {
            if (isMirrored)
                localPoints.Reverse();

            var vertices = new List<MeshBooleanVertex>(localPoints.Count);

            foreach (var point in localPoints)
                vertices.Add(new MeshBooleanVertex(prismToMesh.MultiplyPoint3x4(point), Vector3.zero, Vector2.zero));

            var polygon = MeshBooleanPolygon.Create(vertices, submesh, true);

            if (polygon == null)
                return;

            for (var index = 0; index < vertices.Count; index++)
            {
                var vertex = vertices[index];
                vertex.Normal = polygon.Plane.Normal;
                vertices[index] = vertex;
            }

            polygons.Add(polygon);
        }


        // ── Меш ⇄ многоугольники ─────────────────────────────────────────

        // Каждый треугольник меша — многоугольник со своей частью меша; вырожденные пропускаются.
        public static List<MeshBooleanPolygon> Read(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var textureCoordinates = mesh.uv;
            var polygons = new List<MeshBooleanPolygon>();

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var triangles = mesh.GetTriangles(submesh);

                for (var index = 0; index + 2 < triangles.Length; index += 3)
                {
                    var corners = new List<MeshBooleanVertex>(3);

                    for (var corner = 0; corner < 3; corner++)
                    {
                        var vertex = triangles[index + corner];
                        corners.Add(new MeshBooleanVertex(vertices[vertex], normals.Length > vertex ? normals[vertex] : Vector3.up,
                            textureCoordinates.Length > vertex ? textureCoordinates[vertex] : Vector2.zero));
                    }

                    var polygon = MeshBooleanPolygon.Create(corners, submesh, false);

                    if (polygon != null)
                        polygons.Add(polygon);
                }
            }

            return polygons;
        }


        // Многоугольники — веером в треугольники; у каждого треугольника свои вершины.
        public static void Write(Mesh mesh, List<MeshBooleanPolygon> polygons, int submeshCount)
        {
            var vertexCount = 0;

            foreach (var polygon in polygons)
                vertexCount += polygon.Vertices.Count;

            var vertices = new List<Vector3>(vertexCount);
            var normals = new List<Vector3>(vertexCount);
            var textureCoordinates = new List<Vector2>(vertexCount);
            var trianglesBySubmesh = new List<int>[submeshCount];

            for (var submesh = 0; submesh < submeshCount; submesh++)
                trianglesBySubmesh[submesh] = new List<int>();

            foreach (var polygon in polygons)
            {
                var first = vertices.Count;

                foreach (var vertex in polygon.Vertices)
                {
                    vertices.Add(vertex.Position);
                    normals.Add(vertex.Normal);
                    textureCoordinates.Add(vertex.TextureCoordinate);
                }

                var triangles = trianglesBySubmesh[polygon.Submesh];

                for (var index = 1; index + 1 < polygon.Vertices.Count; index++)
                {
                    triangles.Add(first);
                    triangles.Add(first + index);
                    triangles.Add(first + index + 1);
                }
            }

            mesh.Clear();
            mesh.indexFormat = vertices.Count > MaxVerticesFor16BitIndex ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, textureCoordinates);
            mesh.subMeshCount = submeshCount;

            for (var submesh = 0; submesh < submeshCount; submesh++)
                mesh.SetTriangles(trianglesBySubmesh[submesh], submesh);

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }
    }
}
