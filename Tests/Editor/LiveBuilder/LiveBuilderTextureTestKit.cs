using System.Collections.Generic;
using FeatherKit.EditorTools.LiveBuilder;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Общее у проверок текстуры: материалы на время проверки (с плиткой в таблице проекта — только в памяти, убирается в конце)
    /// и чтение развёртки готового меша у точки мира.
    /// </summary>
    public sealed class LiveBuilderTextureTestKit : System.IDisposable
    {
        private readonly List<Material> materials = new();


        // Новый материал; плитка не 1 × 1 — в таблицу проекта до конца проверки.
        public Material CreateMaterial(string name, Vector2 metersPerTile)
        {
            var material = new Material(BoxModelFactory.LoadDefaultMaterial()) { name = name };
            materials.Add(material);

            if (metersPerTile != Vector2.one)
                LiveBuilderProjectSettings.instance.SetMaterialTileSize(material, metersPerTile);

            return material;
        }


        // Развёртка вершины меша в точке мира, на грани с этой нормалью мира; нет такой — false.
        public static bool TryGetUvAt(MeshFilter filter, Vector3 worldPoint, Vector3 worldNormal, out Vector2 uv)
        {
            var mesh = filter.sharedMesh;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uvs = mesh.uv;

            for (var index = 0; index < vertices.Length; index++)
            {
                var position = filter.transform.TransformPoint(vertices[index]);
                var normal = filter.transform.TransformDirection(normals[index]);

                if ((position - worldPoint).sqrMagnitude < 0.0001f && Vector3.Dot(normal, worldNormal) > 0.9f)
                {
                    uv = uvs[index];
                    return true;
                }
            }

            uv = default;
            return false;
        }


        // Числа отличаются на целое — та же точка рисунка.
        public static bool IsSameTilePoint(float first, float second, float tolerance = 0.002f)
        {
            var difference = Mathf.Repeat(first - second, 1f);
            return difference < tolerance || difference > 1f - tolerance;
        }


        public void Dispose()
        {
            foreach (var material in materials)
            {
                LiveBuilderProjectSettings.instance.SetMaterialTileSize(material, Vector2.one);
                Object.DestroyImmediate(material);
            }
        }
    }
}
