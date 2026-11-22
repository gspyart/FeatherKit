using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;
using UnityEngine.Rendering;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Меш колонны (<see cref="LiveBuilderColumn"/>) в её осях.
    /// - сечение — многоугольник, описанный вокруг эллипса основания: у 4, 8, 12… сторон грани смотрят по осям и касаются прямоугольника
    /// - кромки — кольца фаски, бок гладкий от <see cref="SmoothFromSides"/> сторон, верх и низ — веером
    /// - развёртка: бок — U по кругу, V — высота; верх и низ — по плану; всё через общую формулу <see cref="LiveBuilderTextureMapping"/>
    /// - копии повтора — в том же меше
    /// </summary>
    public static class LiveBuilderColumnMeshBuilder
    {
        // С этого числа сторон бок гладкий: нормали по эллипсу, а не по граням.
        public const int SmoothFromSides = 12;

        private readonly struct ProfilePoint
        {
            public ProfilePoint(float inset, float height)
            {
                Inset = inset;
                Height = height;
            }


            // Насколько точка внутри бока по радиусу, м.
            public float Inset { get; }
            public float Height { get; }
        }


        // Возвращает материалы частей меша: бок, верх и низ — одинаковые сливаются в одну часть.
        public static Material[] Fill(Mesh mesh, LiveBuilderColumn column, IReadOnlyList<Vector3> localCopyOffsets, Material defaultMaterial,
            LiveBuilderTextureSpace space)
        {
            var sideMaterial = column.Material != null ? column.Material : defaultMaterial;
            var materials = new List<Material>();
            var sideSubmesh = SubmeshOf(materials, sideMaterial);
            var topSubmesh = SubmeshOf(materials, column.TopMaterial != null ? column.TopMaterial : sideMaterial);
            var bottomSubmesh = SubmeshOf(materials, column.BottomMaterial != null ? column.BottomMaterial : sideMaterial);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>[materials.Count];

            for (var index = 0; index < triangles.Length; index++)
                triangles[index] = new List<int>();

            var shape = new ColumnShape(column);
            var sideTexture = new SurfaceTexture(new Vector2(0f, space.ShiftAlongAxis(1)), column.TextureFits.Get(LiveBuilderColumnSurface.Sides), sideMaterial);
            var planWorldShift = new Vector2(space.ShiftAlongAxis(0), space.ShiftAlongAxis(2));
            var topTexture = new SurfaceTexture(planWorldShift, column.TextureFits.Get(LiveBuilderColumnSurface.Top), materials[topSubmesh]);
            var bottomTexture = new SurfaceTexture(planWorldShift, column.TextureFits.Get(LiveBuilderColumnSurface.Bottom), materials[bottomSubmesh]);
            var mesher = new ColumnMesher(shape, vertices, normals, uvs);

            foreach (var offset in localCopyOffsets)
            {
                mesher.AddSides(offset, sideTexture, triangles[sideSubmesh]);
                mesher.AddCap(offset, true, topTexture, triangles[topSubmesh]);
                mesher.AddCap(offset, false, bottomTexture, triangles[bottomSubmesh]);
            }

            mesh.Clear();
            mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = materials.Count;

            for (var submesh = 0; submesh < materials.Count; submesh++)
                mesh.SetTriangles(triangles[submesh], submesh, false);

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return materials.ToArray();
        }

        private static int SubmeshOf(List<Material> materials, Material material)
        {
            var index = materials.IndexOf(material);

            if (index >= 0)
                return index;

            materials.Add(material);
            return materials.Count - 1;
        }


        // ── Форма ────────────────────────────────────────────────────────

        /// <summary>Сечение и профиль колонны: углы сторон, радиусы, точки профиля снизу вверх с кольцами фаски.</summary>
        private sealed class ColumnShape
        {
            public ColumnShape(LiveBuilderColumn column)
            {
                var size = column.Size;
                SideCount = Mathf.Clamp(column.Sides, LiveBuilderColumn.MinSides, LiveBuilderColumn.MaxSides);
                RadiusX = size.x / 2f;
                RadiusZ = size.z / 2f;
                Height = size.y;
                IsSmooth = SideCount >= SmoothFromSides;
                CornerFactor = 1f / Mathf.Cos(Mathf.PI / SideCount);

                var bevel = Mathf.Min(column.BevelSize, Height / 2f, Mathf.Min(RadiusX, RadiusZ) / 2f);
                Profile = ProfileOf(Mathf.Max(0f, bevel), column.BevelSegments, Height);
                OuterU = PerimeterLengths();
            }


            public int SideCount { get; }
            public float RadiusX { get; }
            public float RadiusZ { get; }
            public float Height { get; }
            public bool IsSmooth { get; }

            // Угол многоугольника дальше эллипса во столько раз: грани касаются эллипса серединами.
            public float CornerFactor { get; }

            public List<ProfilePoint> Profile { get; }

            // Длина по кругу от начала до каждого угла наружного кольца, м — U бока.
            public float[] OuterU { get; }


            // Угол стороны: грани смотрят по осям, угол — между ними.
            public float AngleOf(int corner)
            {
                return Mathf.PI / SideCount + 2f * Mathf.PI * corner / SideCount;
            }


            // Точка угла кольца, утопленного внутрь на inset, на высоте height, в осях колонны.
            public Vector3 PointOf(int corner, float inset, float height)
            {
                var angle = AngleOf(corner);
                return new Vector3(RadiusX + (RadiusX - inset) * CornerFactor * Mathf.Cos(angle), height,
                    RadiusZ + (RadiusZ - inset) * CornerFactor * Mathf.Sin(angle));
            }


            // Наружу по горизонтали: у гладкой — по эллипсу в этом углу, у гранёной — поперёк грани между двумя углами.
            public Vector3 OutwardAt(int corner, int faceCorner)
            {
                if (IsSmooth)
                {
                    var angle = AngleOf(corner);
                    return new Vector3(Mathf.Cos(angle) / Mathf.Max(RadiusX, 0.0001f), 0f, Mathf.Sin(angle) / Mathf.Max(RadiusZ, 0.0001f)).normalized;
                }

                var edge = PointOf(faceCorner + 1, 0f, 0f) - PointOf(faceCorner, 0f, 0f);
                var outward = new Vector3(edge.z, 0f, -edge.x).normalized;
                var middle = (PointOf(faceCorner + 1, 0f, 0f) + PointOf(faceCorner, 0f, 0f)) / 2f - new Vector3(RadiusX, 0f, RadiusZ);
                return Vector3.Dot(outward, middle) < 0f ? -outward : outward;
            }


            // Снизу вверх: кромка низа (скругление вокруг центра b, b), бок, кромка верха.
            private static List<ProfilePoint> ProfileOf(float bevel, int segments, float height)
            {
                var profile = new List<ProfilePoint>();

                if (bevel <= 0f)
                {
                    profile.Add(new ProfilePoint(0f, 0f));
                    profile.Add(new ProfilePoint(0f, height));
                    return profile;
                }

                for (var step = 0; step <= segments; step++)
                {
                    var angle = Mathf.PI / 2f * step / segments;
                    profile.Add(new ProfilePoint(bevel - bevel * Mathf.Sin(angle), bevel - bevel * Mathf.Cos(angle)));
                }

                for (var step = 0; step <= segments; step++)
                {
                    var angle = Mathf.PI / 2f * step / segments;
                    profile.Add(new ProfilePoint(bevel - bevel * Mathf.Cos(angle), height - bevel + bevel * Mathf.Sin(angle)));
                }

                return profile;
            }

            private float[] PerimeterLengths()
            {
                var lengths = new float[SideCount + 1];

                for (var corner = 1; corner <= SideCount; corner++)
                    lengths[corner] = lengths[corner - 1] + Vector3.Distance(PointOf(corner - 1, 0f, 0f), PointOf(corner, 0f, 0f));

                return lengths;
            }
        }


        /// <summary>Как ложится текстура на одно место колонны: сдвиг до мира, подгонка, плитка материала.</summary>
        private readonly struct SurfaceTexture
        {
            private readonly Vector2 worldShift;
            private readonly LiveBuilderTextureFit fit;
            private readonly Vector2 metersPerTile;


            public SurfaceTexture(Vector2 worldShift, LiveBuilderTextureFit fit, Material material)
            {
                this.worldShift = worldShift;
                this.fit = fit;
                metersPerTile = LiveBuilderMaterialTiling.SizeOf(material);
            }


            public Vector2 Map(Vector2 planarMeters)
            {
                return LiveBuilderTextureMapping.Map(planarMeters, worldShift, fit, metersPerTile);
            }
        }


        // ── Сборка ───────────────────────────────────────────────────────

        /// <summary>Кладёт полосы бока и крышки одной копии колонны в общие списки меша.</summary>
        private sealed class ColumnMesher
        {
            private readonly ColumnShape shape;
            private readonly List<Vector3> vertices;
            private readonly List<Vector3> normals;
            private readonly List<Vector2> uvs;


            public ColumnMesher(ColumnShape shape, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs)
            {
                this.shape = shape;
                this.vertices = vertices;
                this.normals = normals;
                this.uvs = uvs;
            }


            // Бок и кромки: полоса на каждый отрезок профиля, у полосы — свой наклон нормали.
            public void AddSides(Vector3 offset, SurfaceTexture texture, List<int> triangles)
            {
                var profile = shape.Profile;

                for (var step = 0; step + 1 < profile.Count; step++)
                {
                    var below = profile[step];
                    var above = profile[step + 1];

                    if (Mathf.Approximately(below.Inset, above.Inset) && Mathf.Approximately(below.Height, above.Height))
                        continue;

                    var slope = new Vector2(above.Height - below.Height, above.Inset - below.Inset).normalized;

                    for (var corner = 0; corner < shape.SideCount; corner++)
                    {
                        var start = vertices.Count;
                        AddSideVertex(offset, corner, corner, below, slope, texture);
                        AddSideVertex(offset, corner + 1, corner, below, slope, texture);
                        AddSideVertex(offset, corner + 1, corner, above, slope, texture);
                        AddSideVertex(offset, corner, corner, above, slope, texture);
                        triangles.AddRange(new[] { start, start + 3, start + 2, start, start + 2, start + 1 });
                    }
                }
            }

            private void AddSideVertex(Vector3 offset, int corner, int faceCorner, ProfilePoint point, Vector2 slope, SurfaceTexture texture)
            {
                vertices.Add(shape.PointOf(corner, point.Inset, point.Height) + offset);
                normals.Add((shape.OutwardAt(corner, faceCorner) * slope.x + Vector3.up * slope.y).normalized);
                uvs.Add(texture.Map(new Vector2(shape.OuterU[corner], point.Height + offset.y)));
            }


            // Верх или низ — веером от середины; низ смотрит вниз, обход обратный.
            public void AddCap(Vector3 offset, bool isTop, SurfaceTexture texture, List<int> triangles)
            {
                var point = isTop ? shape.Profile[shape.Profile.Count - 1] : shape.Profile[0];
                var normal = isTop ? Vector3.up : Vector3.down;
                var center = vertices.Count;
                AddCapVertex(new Vector3(shape.RadiusX, point.Height, shape.RadiusZ) + offset, normal, texture);

                for (var corner = 0; corner < shape.SideCount; corner++)
                    AddCapVertex(shape.PointOf(corner, point.Inset, point.Height) + offset, normal, texture);

                for (var corner = 0; corner < shape.SideCount; corner++)
                {
                    var current = center + 1 + corner;
                    var next = center + 1 + (corner + 1) % shape.SideCount;
                    triangles.AddRange(isTop ? new[] { center, next, current } : new[] { center, current, next });
                }
            }

            private void AddCapVertex(Vector3 position, Vector3 normal, SurfaceTexture texture)
            {
                vertices.Add(position);
                normals.Add(normal);
                uvs.Add(texture.Map(new Vector2(position.x, position.z)));
            }
        }
    }
}
