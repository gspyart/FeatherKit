using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Строит меш объекта из коробок по его наружным поверхностям: по четырёхугольнику на прямоугольник, своя часть меша на каждый материал.
    /// Развёртка — по метрам от опоры объекта: объект стоит на сетке — клетки текстуры совпадают с клетками мира.
    /// Ещё собирает меш для столкновений: видимый меш плюс невидимые скаты лестниц.
    /// </summary>
    public static class BoxModelMeshBuilder
    {
        // Четырёхугольник на прямоугольник — по ним видно, собран ли меш по этим поверхностям.
        public const int VerticesPerSurface = 4;

        // Больше вершин — нужен 32-битный индекс.
        private const int MaxVerticesFor16BitIndex = 65000;


        // Заполняет готовый меш заново — тот же объект и тот же файл, окно сцены сразу видит изменение. Возвращает материалы по частям меша.
        // Материал прямоугольника — от стороны коробки, которая его создала; нет у неё своего — материал объекта.
        public static Material[] Fill(Mesh mesh, IReadOnlyList<BoxModelSurface> surfaces, IReadOnlyList<BoxModelSolverBox> boxes, Material defaultMaterial)
        {
            var usedMaterials = new List<Material>();
            var trianglesByMaterial = new List<List<int>>();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var textureCoordinates = new List<Vector2>();

            foreach (var surface in surfaces)
            {
                var material = MaterialOf(surface, boxes, defaultMaterial);
                var materialIndex = usedMaterials.IndexOf(material);

                if (materialIndex < 0)
                {
                    materialIndex = usedMaterials.Count;
                    usedMaterials.Add(material);
                    trianglesByMaterial.Add(new List<int>());
                }

                AddQuad(surface, vertices, normals, textureCoordinates, trianglesByMaterial[materialIndex]);
            }

            mesh.Clear();
            mesh.indexFormat = vertices.Count > MaxVerticesFor16BitIndex ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, textureCoordinates);
            mesh.subMeshCount = trianglesByMaterial.Count;

            for (var index = 0; index < trianglesByMaterial.Count; index++)
                mesh.SetTriangles(trianglesByMaterial[index], index);

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return usedMaterials.ToArray();
        }

        private static Material MaterialOf(BoxModelSurface surface, IReadOnlyList<BoxModelSolverBox> boxes, Material defaultMaterial)
        {
            var hasSource = surface.SourcePart >= 0 && surface.SourcePart < boxes.Count;
            var own = hasSource ? boxes[surface.SourcePart].MaterialForSurface(surface.Face) : null;
            return own != null ? own : defaultMaterial;
        }

        // Четыре угла прямоугольника; порядок обхода — чтобы лицевая сторона смотрела наружу.
        private static void AddQuad(BoxModelSurface surface, List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> textureCoordinates, List<int> triangles)
        {
            var axisU = BoxFaces.SurfaceAxisU(surface.Face);
            var axisV = BoxFaces.SurfaceAxisV(surface.Face);
            var normal = BoxFaces.Normal(surface.Face);
            var firstVertex = vertices.Count;

            foreach (var corner in CornersOf(surface))
            {
                vertices.Add(corner);
                normals.Add(normal);
                textureCoordinates.Add(new Vector2(corner[axisU], corner[axisV]));
            }

            var facesOutward = Vector3.Dot(Vector3.Cross(vertices[firstVertex + 1] - vertices[firstVertex], vertices[firstVertex + 2] - vertices[firstVertex]), normal) > 0f;

            if (facesOutward)
                triangles.AddRange(new[] { firstVertex, firstVertex + 1, firstVertex + 2, firstVertex, firstVertex + 2, firstVertex + 3 });
            else
                triangles.AddRange(new[] { firstVertex, firstVertex + 2, firstVertex + 1, firstVertex, firstVertex + 3, firstVertex + 2 });
        }

        // Четыре угла прямоугольника в осях объекта, м.
        private static Vector3[] CornersOf(BoxModelSurface surface)
        {
            var axisU = BoxFaces.SurfaceAxisU(surface.Face);
            var axisV = BoxFaces.SurfaceAxisV(surface.Face);
            var area = surface.Area;
            var corners = new Vector3[VerticesPerSurface];
            var coordinates = new[] { (area.xMin, area.yMin), (area.xMax, area.yMin), (area.xMax, area.yMax), (area.xMin, area.yMax) };

            for (var index = 0; index < corners.Length; index++)
            {
                corners[index][BoxFaces.Axis(surface.Face)] = MillimeterBox.ToMeters(surface.Plane);
                corners[index][axisU] = MillimeterBox.ToMeters(coordinates[index].Item1);
                corners[index][axisV] = MillimeterBox.ToMeters(coordinates[index].Item2);
            }

            return corners;
        }


        // ── Сверка ───────────────────────────────────────────────────────

        // Меш собран именно по этим поверхностям: то же число вершин и та же их сумма.
        // Ловит файл, который отстал от сцены или префаба, даже если число вершин случайно совпало.
        public static bool IsBuiltFrom(Mesh mesh, IReadOnlyList<BoxModelSurface> surfaces)
        {
            if (mesh == null || mesh.vertexCount != surfaces.Count * VerticesPerSurface)
                return false;

            var expected = Vector3.zero;

            foreach (var surface in surfaces)
                foreach (var corner in CornersOf(surface))
                    expected += corner;

            var actual = Vector3.zero;

            foreach (var vertex in mesh.vertices)
                actual += vertex;

            return (expected - actual).sqrMagnitude < 0.0001f * Mathf.Max(1, mesh.vertexCount);
        }


        // ── Коллайдер со скатами ─────────────────────────────────────────

        // Меш для столкновений: все треугольники видимого меша плюс призмы скатов лестниц. Скаты лицом наружу — по ним ходят.
        public static void FillCollider(Mesh colliderMesh, Mesh renderMesh, IReadOnlyList<BoxModelRampShape> ramps)
        {
            var vertices = new List<Vector3>(renderMesh.vertices);
            var triangles = new List<int>(renderMesh.triangles);

            foreach (var ramp in ramps)
                AddRamp(ramp, vertices, triangles);

            colliderMesh.Clear();
            colliderMesh.indexFormat = vertices.Count > MaxVerticesFor16BitIndex ? IndexFormat.UInt32 : IndexFormat.UInt16;
            colliderMesh.SetVertices(vertices);
            colliderMesh.SetTriangles(triangles, 0);
            colliderMesh.RecalculateBounds();
        }

        // Призма: две боковины из четырёх точек профиля и четыре грани между ними.
        private static void AddRamp(BoxModelRampShape ramp, List<Vector3> vertices, List<int> triangles)
        {
            var corners = ramp.Corners;
            var center = Vector3.zero;

            foreach (var corner in corners)
                center += corner / corners.Length;

            AddOutwardQuad(corners[0], corners[1], corners[2], corners[3], center, vertices, triangles);
            AddOutwardQuad(corners[4], corners[5], corners[6], corners[7], center, vertices, triangles);

            for (var index = 0; index < 4; index++)
            {
                var next = (index + 1) % 4;
                AddOutwardQuad(corners[index], corners[next], corners[4 + next], corners[4 + index], center, vertices, triangles);
            }
        }

        // Выпуклый четырёхугольник лицом от центра призмы.
        private static void AddOutwardQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 center, List<Vector3> vertices, List<int> triangles)
        {
            var first = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });

            var normal = Vector3.Cross(b - a, c - a);
            var facesOutward = Vector3.Dot(normal, (a + b + c + d) / 4f - center) > 0f;

            if (facesOutward)
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            else
                triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
        }
    }
}
