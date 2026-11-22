using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Грани меша стены или части здания для сверки «как было — как стало»: площадь и габарит по каждой паре «материал, направление».
    /// Не зависит от разбиения на треугольники: проём вычитанием и тот же проём по линии дают одно и то же.
    /// Объём для этого не годится — меши частей зданий не замкнуты.
    /// </summary>
    public sealed class LiveBuilderUpgradeMeshFaces
    {
        // Шаг сетки направлений: 0,001. Площадь — в допуске м² плюс доля, габарит — в мм.
        private const float NormalUnits = 1000f;
        private const double AreaTolerance = 1e-3;
        private const double AreaRelativeTolerance = 1e-4;

        private readonly Dictionary<(string Material, Vector3Int Normal), LiveBuilderUpgradeFaceTotals> totals = new();


        public static LiveBuilderUpgradeMeshFaces Of(Component owner)
        {
            var faces = new LiveBuilderUpgradeMeshFaces();
            var filter = owner.GetComponent<MeshFilter>();
            var renderer = owner.GetComponent<MeshRenderer>();
            var mesh = filter != null ? filter.sharedMesh : null;

            if (mesh == null || renderer == null || !owner.gameObject.activeInHierarchy)
                return faces;

            var matrix = owner.transform.localToWorldMatrix;
            var vertices = mesh.vertices;
            var materials = renderer.sharedMaterials;

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var material = submesh < materials.Length && materials[submesh] != null ? materials[submesh].name : "";
                var triangles = mesh.GetTriangles(submesh);

                for (var index = 0; index + 2 < triangles.Length; index += 3)
                {
                    var a = matrix.MultiplyPoint3x4(vertices[triangles[index]]);
                    var b = matrix.MultiplyPoint3x4(vertices[triangles[index + 1]]);
                    var c = matrix.MultiplyPoint3x4(vertices[triangles[index + 2]]);
                    var cross = Vector3.Cross(b - a, c - a);
                    var area = cross.magnitude / 2f;

                    if (area < 1e-8f)
                        continue;

                    var key = (material, Vector3Int.RoundToInt(cross.normalized * NormalUnits));

                    if (!faces.totals.TryGetValue(key, out var faceTotals))
                        faces.totals[key] = faceTotals = new LiveBuilderUpgradeFaceTotals(a);

                    faceTotals.Add(a, b, c, area);
                }
            }

            return faces;
        }


        // Чем грани after расходятся с этими; совпадают — null.
        public string DifferenceFrom(LiveBuilderUpgradeMeshFaces after)
        {
            var keys = new HashSet<(string Material, Vector3Int Normal)>(totals.Keys);
            keys.UnionWith(after.totals.Keys);

            foreach (var key in keys)
            {
                totals.TryGetValue(key, out var before);
                after.totals.TryGetValue(key, out var now);
                var beforeArea = before?.Area ?? 0.0;
                var nowArea = now?.Area ?? 0.0;

                if (System.Math.Abs(beforeArea - nowArea) > AreaTolerance + AreaRelativeTolerance * System.Math.Max(beforeArea, nowArea))
                    return "faces facing " + (Vector3)key.Normal / NormalUnits + " changed: " + beforeArea.ToString("0.###") + " → " + nowArea.ToString("0.###") + " m²";

                if (before != null && now != null && !before.HasSameBounds(now))
                    return "faces facing " + (Vector3)key.Normal / NormalUnits + " moved";
            }

            return null;
        }
    }


    /// <summary>Сумма граней одного направления: площадь и габарит в мире.</summary>
    public sealed class LiveBuilderUpgradeFaceTotals
    {
        private const float BoundsTolerance = 0.002f;

        private Bounds bounds;


        public LiveBuilderUpgradeFaceTotals(Vector3 firstPoint)
        {
            bounds = new Bounds(firstPoint, Vector3.zero);
        }


        public double Area { get; private set; }


        public void Add(Vector3 a, Vector3 b, Vector3 c, float area)
        {
            Area += area;
            bounds.Encapsulate(a);
            bounds.Encapsulate(b);
            bounds.Encapsulate(c);
        }


        public bool HasSameBounds(LiveBuilderUpgradeFaceTotals other)
        {
            return Vector3.Distance(bounds.min, other.bounds.min) <= BoundsTolerance && Vector3.Distance(bounds.max, other.bounds.max) <= BoundsTolerance;
        }
    }
}
