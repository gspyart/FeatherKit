using System.Collections.Generic;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using Unity.Mathematics;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Грани, которых не видно снаружи, — для отпечатка «по виду» после обновления старых данных (<see cref="LiveBuilderLookDigest"/>).
    /// - обновление кладёт кусок во вложенный «Kept From Cuts»: его грани внутри тела внешнего объекта и грани внешнего внутри него
    ///   (и касания вплотную) — внутри, до обновления их не было в одном меше
    /// - меш обрезается телом другого — то же дерево разбиения, что у вычитания (<see cref="MeshBooleanTree"/>); прочие пары не трогаются
    /// </summary>
    public static class LiveBuilderLookHiddenFaces
    {
        private const string NoMaterial = "(none)";


        // Тела, внутри которых грани меша не видны: у «Kept From Cuts» — его внешний объект, у объекта — его «Kept From Cuts».
        public static IReadOnlyList<MeshFilter> OccludersOf(MeshFilter filter)
        {
            var occluders = new List<MeshFilter>();

            if (!filter.TryGetComponent<BoxModel>(out var model))
                return occluders;

            if (IsKeptGroup(model))
            {
                var outer = LiveBuilderGroups.GroupOf(model.transform.parent);

                if (outer != null && outer.TryGetComponent<MeshFilter>(out var outerFilter) && outerFilter.sharedMesh != null)
                    occluders.Add(outerFilter);

                return occluders;
            }

            foreach (var nested in model.GetComponentsInChildren<BoxModel>())
                if (IsKeptGroup(nested) && LiveBuilderGroups.GroupOf(nested.transform.parent) == model && nested.TryGetComponent<MeshFilter>(out var nestedFilter)
                    && nestedFilter.sharedMesh != null)
                    occluders.Add(nestedFilter);

            return occluders;
        }

        private static bool IsKeptGroup(BoxModel model)
        {
            return model.name == LiveBuilderKeptFromCuts.GroupName && model.transform.parent != null;
        }


        // Треугольники меша в мире, оставшиеся снаружи всех тел occluders, — с именем материала.
        public static List<LiveBuilderLookTriangle> VisibleTrianglesOf(MeshFilter filter, MeshRenderer renderer, IReadOnlyList<MeshFilter> occluders)
        {
            var polygons = WorldPolygonsOf(filter);

            foreach (var occluder in occluders)
            {
                var tree = MeshBooleanTree.Rent(WorldPolygonsOf(occluder));
                polygons = tree.ClipPolygons(polygons);
                tree.Release();
            }

            var materials = renderer.sharedMaterials;
            var triangles = new List<LiveBuilderLookTriangle>();

            foreach (var polygon in polygons)
            {
                var material = polygon.Submesh < materials.Length && materials[polygon.Submesh] != null ? materials[polygon.Submesh].name : NoMaterial;
                var vertices = polygon.Vertices;

                for (var index = 1; index + 1 < vertices.Count; index++)
                    triangles.Add(new LiveBuilderLookTriangle(material, ToDouble(vertices[0].Position), ToDouble(vertices[index].Position), ToDouble(vertices[index + 1].Position)));
            }

            return triangles;
        }

        private static List<MeshBooleanPolygon> WorldPolygonsOf(MeshFilter filter)
        {
            var mesh = filter.sharedMesh;
            var matrix = filter.transform.localToWorldMatrix;
            var vertices = mesh.vertices;
            var polygons = new List<MeshBooleanPolygon>();

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var triangles = mesh.GetTriangles(submesh);

                for (var index = 0; index + 2 < triangles.Length; index += 3)
                {
                    var corners = new List<MeshBooleanVertex>(3);

                    for (var corner = 0; corner < 3; corner++)
                        corners.Add(new MeshBooleanVertex(matrix.MultiplyPoint3x4(vertices[triangles[index + corner]]), Vector3.up, Vector2.zero));

                    var polygon = MeshBooleanPolygon.Create(corners, submesh, false);

                    if (polygon != null)
                        polygons.Add(polygon);
                }
            }

            return polygons;
        }

        private static double3 ToDouble(Vector3 point)
        {
            return new double3(point.x, point.y, point.z);
        }
    }


    /// <summary>Видимый треугольник меша в мире и имя его материала.</summary>
    public readonly struct LiveBuilderLookTriangle
    {
        public LiveBuilderLookTriangle(string material, double3 a, double3 b, double3 c)
        {
            Material = material;
            A = a;
            B = b;
            C = c;
        }


        public string Material { get; }
        public double3 A { get; }
        public double3 B { get; }
        public double3 C { get; }
    }
}
