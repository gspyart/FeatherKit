using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Вычитание тел из готового меша постройки — общее у вырезов (<see cref="GeometryCutterApplier"/>) и фаски рёбер (<see cref="BoxModelBevel"/>):
    /// меш ⇄ многоугольники, вычитание (<see cref="MeshBoolean"/>) только для треугольников рядом с телом — остальные не трогаются,
    /// новые грани (стенки дыры, срез ребра) — с развёрткой в метрах на ближайшую плоскость, как у всех построек.
    /// Само тело обычно призма — контур, протянутый по оси: её тоже строит здесь.
    /// </summary>
    public static class MeshSubtraction
    {
        // Больше вершин — нужен 32-битный индекс.
        private const int MaxVerticesFor16BitIndex = 65000;

        // Запас к габариту тела при отборе треугольников рядом, м.
        private const float NearMargin = 0.01f;


        // ── Вычитание ────────────────────────────────────────────────────

        // Тело с габаритом solidBounds (в осях меша) — из треугольников рядом с ним. solidFor строит тело по треугольникам рядом:
        // по ним, например, выбирают материал новых граней.
        public static List<MeshBooleanPolygon> SubtractNear(List<MeshBooleanPolygon> polygons, Bounds solidBounds,
            System.Func<List<MeshBooleanPolygon>, List<MeshBooleanPolygon>> solidFor)
        {
            solidBounds.Expand(NearMargin * 2f);

            var near = new List<MeshBooleanPolygon>();
            var result = new List<MeshBooleanPolygon>();

            foreach (var polygon in polygons)
                (BoundsOf(polygon).Intersects(solidBounds) ? near : result).Add(polygon);

            if (near.Count == 0)
                return polygons;

            var cut = MeshBoolean.Subtract(near, solidFor(near));

            foreach (var polygon in cut)
                if (polygon.IsFromCutter)
                    UnwrapFlat(polygon);

            result.AddRange(cut);
            return result;
        }


        // Часть меша, которой среди многоугольников больше всего: новые грани — как то, что вокруг.
        public static int MostCommonSubmesh(List<MeshBooleanPolygon> polygons)
        {
            var counts = new Dictionary<int, int>();
            var best = polygons[0].Submesh;

            foreach (var polygon in polygons)
            {
                counts[polygon.Submesh] = counts.TryGetValue(polygon.Submesh, out var count) ? count + 1 : 1;

                if (counts[polygon.Submesh] > counts[best])
                    best = polygon.Submesh;
            }

            return best;
        }


        // Новая грань: нормаль — по её плоскости, развёртка — в метрах на плоскость, ближайшую к ней.
        private static void UnwrapFlat(MeshBooleanPolygon polygon)
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

        private static Bounds BoundsOf(MeshBooleanPolygon polygon)
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
            var polygons = new List<MeshBooleanPolygon>();
            var isMirrored = prismToMesh.determinant < 0f;

            foreach (var cap in CapsOf(outline))
            {
                var frontCap = cap.ConvertAll(point => new Vector3(point.x, point.y, toZ));
                var backCap = cap.ConvertAll(point => new Vector3(point.x, point.y, fromZ));
                backCap.Reverse();
                AddSolidPolygon(polygons, frontCap, prismToMesh, submesh, isMirrored);
                AddSolidPolygon(polygons, backCap, prismToMesh, submesh, isMirrored);
            }

            for (var index = 0; index < outline.Count; index++)
            {
                var start = outline[index];
                var end = outline[(index + 1) % outline.Count];
                AddSolidPolygon(polygons, new List<Vector3>
                {
                    new(start.x, start.y, fromZ), new(end.x, end.y, fromZ), new(end.x, end.y, toZ), new(start.x, start.y, toZ)
                }, prismToMesh, submesh, isMirrored);
            }

            return polygons;
        }

        // Крышка выпуклого контура — он сам, невыпуклого — веер треугольников из первой точки.
        private static List<List<Vector2>> CapsOf(List<Vector2> outline)
        {
            if (IsConvex(outline))
                return new List<List<Vector2>> { outline };

            var caps = new List<List<Vector2>>();

            for (var index = 1; index + 1 < outline.Count; index++)
                caps.Add(new List<Vector2> { outline[0], outline[index], outline[index + 1] });

            return caps;
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

        // Грань тела: точки — в осях призмы; зеркальный перенос выворачивает обход — разворачиваем обратно.
        private static void AddSolidPolygon(List<MeshBooleanPolygon> polygons, List<Vector3> localPoints, Matrix4x4 prismToMesh, int submesh, bool isMirrored)
        {
            if (isMirrored)
                localPoints.Reverse();

            var vertices = new List<MeshBooleanVertex>();

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
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var textureCoordinates = new List<Vector2>();
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

                for (var index = 1; index + 1 < polygon.Vertices.Count; index++)
                    trianglesBySubmesh[polygon.Submesh].AddRange(new[] { first, first + index, first + index + 1 });
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
