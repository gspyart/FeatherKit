using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;

namespace FeatherKit.EditorTools.LevelPalette
{
    /// <summary>
    /// Своё превью префабов и моделей для палитры: Unity не рисует меши ProBuilder — в ассете их нет, они строятся в сцене.
    /// - меш ProBuilder собирает из его данных прямо в ассете, без копии в сцене
    /// - рисует по одному превью за тик редактора, готовые хранит до правки ассета
    /// </summary>
    [InitializeOnLoad]
    public static class PrefabPreviewRenderer
    {
        private const int PreviewSize = 256;
        private const float CameraFieldOfView = 30f;

        private static readonly Quaternion CameraRotation = Quaternion.Euler(25f, -135f, 0f);
        private static readonly Quaternion KeyLightRotation = Quaternion.Euler(50f, -110f, 0f);
        private static readonly Quaternion FillLightRotation = Quaternion.Euler(-20f, 60f, 0f);
        private static readonly Color AmbientColor = new Color(0.35f, 0.35f, 0.38f);

        private static readonly Dictionary<string, RenderedPrefabPreview> PreviewsByGuid = new Dictionary<string, RenderedPrefabPreview>();
        private static readonly List<string> QueuedGuids = new List<string>();
        private static PreviewRenderUtility renderUtility;


        static PrefabPreviewRenderer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseAll;
        }


        // ── Выдача превью ────────────────────────────────────────────────

        /// <summary>false — превью ещё в очереди. true — готово; null, если рисовать нечего.</summary>
        public static bool TryGetPreview(GameObject prefab, out Texture2D preview)
        {
            preview = null;

            var path = AssetDatabase.GetAssetPath(prefab);
            var guid = AssetDatabase.AssetPathToGUID(path);

            // Хеш зависимостей меняется при правке префаба и его материалов — тогда рисуем заново.
            if (PreviewsByGuid.TryGetValue(guid, out var rendered) && rendered.SourceHash == AssetDatabase.GetAssetDependencyHash(path))
            {
                preview = rendered.Texture;
                return true;
            }

            Queue(guid);
            return false;
        }

        private static void Queue(string guid)
        {
            if (QueuedGuids.Contains(guid))
                return;

            if (QueuedGuids.Count == 0)
                EditorApplication.update += RenderNextInQueue;

            QueuedGuids.Add(guid);
        }

        private static void RenderNextInQueue()
        {
            var guid = QueuedGuids[0];
            QueuedGuids.RemoveAt(0);

            if (QueuedGuids.Count == 0)
                EditorApplication.update -= RenderNextInQueue;

            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                return;

            if (PreviewsByGuid.TryGetValue(guid, out var outdated) && outdated.Texture != null)
                Object.DestroyImmediate(outdated.Texture);

            PreviewsByGuid[guid] = new RenderedPrefabPreview(AssetDatabase.GetAssetDependencyHash(path), Render(prefab));
        }


        // ── Отрисовка ────────────────────────────────────────────────────

        private static Texture2D Render(GameObject prefab)
        {
            var parts = CollectMeshParts(prefab);

            if (parts.Count == 0)
                return null;

            var utility = renderUtility ??= CreateRenderUtility();
            utility.BeginStaticPreview(new Rect(0f, 0f, PreviewSize, PreviewSize));
            PlaceCamera(utility.camera, BoundsOf(parts));

            foreach (var part in parts)
            {
                for (var submesh = 0; submesh < part.Mesh.subMeshCount; submesh++)
                {
                    var material = part.Materials[Mathf.Min(submesh, part.Materials.Length - 1)];

                    if (material != null)
                        utility.DrawMesh(part.Mesh, part.Matrix, material, submesh);
                }
            }

            utility.Render(true);
            var texture = utility.EndStaticPreview();
            texture.hideFlags = HideFlags.HideAndDontSave;

            foreach (var part in parts)
            {
                if (part.IsTemporaryMesh)
                    Object.DestroyImmediate(part.Mesh);
            }

            return texture;
        }

        private static PreviewRenderUtility CreateRenderUtility()
        {
            var utility = new PreviewRenderUtility();
            utility.cameraFieldOfView = CameraFieldOfView;
            utility.ambientColor = AmbientColor;
            utility.lights[0].intensity = 1.2f;
            utility.lights[0].transform.rotation = KeyLightRotation;
            utility.lights[1].intensity = 0.5f;
            utility.lights[1].transform.rotation = FillLightRotation;

            return utility;
        }

        // Камера сверху-сбоку и так далеко, чтобы весь префаб влез в кадр.
        private static void PlaceCamera(Camera camera, Bounds bounds)
        {
            var radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
            var distance = radius / Mathf.Sin(CameraFieldOfView * 0.5f * Mathf.Deg2Rad);

            camera.transform.rotation = CameraRotation;
            camera.transform.position = bounds.center - CameraRotation * Vector3.forward * distance;
            camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 1.1f);
            camera.farClipPlane = distance + radius * 1.1f;
        }

        private static Bounds BoundsOf(List<PrefabMeshPart> parts)
        {
            var bounds = TransformBounds(parts[0].Mesh.bounds, parts[0].Matrix);

            foreach (var part in parts)
                bounds.Encapsulate(TransformBounds(part.Mesh.bounds, part.Matrix));

            return bounds;
        }

        private static Bounds TransformBounds(Bounds local, Matrix4x4 matrix)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);

            for (var corner = 0; corner < 8; corner++)
            {
                var offset = new Vector3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1) * 2f - Vector3.one;
                result.Encapsulate(matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, offset)));
            }

            return result;
        }


        // ── Меши префаба ─────────────────────────────────────────────────

        private static List<PrefabMeshPart> CollectMeshParts(GameObject prefab)
        {
            var parts = new List<PrefabMeshPart>();

            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer.sharedMaterials.Length == 0)
                    continue;

                var proBuilderMesh = renderer.GetComponent<ProBuilderMesh>();
                var mesh = proBuilderMesh != null ? BuildProBuilderMesh(proBuilderMesh) : SharedMeshOf(renderer);

                if (mesh != null)
                    parts.Add(new PrefabMeshPart(mesh, renderer.transform.localToWorldMatrix, renderer.sharedMaterials, proBuilderMesh != null));
            }

            return parts;
        }

        private static Mesh SharedMeshOf(Renderer renderer)
        {
            switch (renderer)
            {
                case MeshRenderer:
                    var filter = renderer.GetComponent<MeshFilter>();
                    return filter != null ? filter.sharedMesh : null;

                case SkinnedMeshRenderer skinned:
                    return skinned.sharedMesh;

                default:
                    return null;
            }
        }

        // Только то, что нужно для картинки: вершины, UV, цвета, треугольники по материалам. Нормали — пересчётом.
        private static Mesh BuildProBuilderMesh(ProBuilderMesh source)
        {
            if (source.vertexCount == 0 || source.faces.Count == 0)
                return null;

            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(new List<Vector3>(source.positions));

            if (source.textures != null && source.textures.Count == source.vertexCount)
                mesh.SetUVs(0, new List<Vector2>(source.textures));

            if (source.colors != null && source.colors.Count == source.vertexCount)
                mesh.SetColors(new List<Color>(source.colors));

            var trianglesBySubmesh = new List<List<int>>();

            foreach (var face in source.faces)
            {
                while (trianglesBySubmesh.Count <= face.submeshIndex)
                    trianglesBySubmesh.Add(new List<int>());

                trianglesBySubmesh[face.submeshIndex].AddRange(face.indexes);
            }

            mesh.subMeshCount = trianglesBySubmesh.Count;

            for (var submesh = 0; submesh < trianglesBySubmesh.Count; submesh++)
                mesh.SetTriangles(trianglesBySubmesh[submesh], submesh);

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }


        // ── Очистка ──────────────────────────────────────────────────────

        private static void ReleaseAll()
        {
            foreach (var rendered in PreviewsByGuid.Values)
            {
                if (rendered.Texture != null)
                    Object.DestroyImmediate(rendered.Texture);
            }

            PreviewsByGuid.Clear();
            renderUtility?.Cleanup();
            renderUtility = null;
        }


        // ── Вспомогательные классы ───────────────────────────────────────

        /// <summary>Готовое превью и хеш ассета, с которого оно снято.</summary>
        private class RenderedPrefabPreview
        {
            public RenderedPrefabPreview(Hash128 sourceHash, Texture2D texture)
            {
                SourceHash = sourceHash;
                Texture = texture;
            }


            public Hash128 SourceHash { get; }

            public Texture2D Texture { get; }
        }


        /// <summary>Один меш префаба на месте: где стоит и чем покрашен. Временный — собран из ProBuilder, удалить после отрисовки.</summary>
        private class PrefabMeshPart
        {
            public PrefabMeshPart(Mesh mesh, Matrix4x4 matrix, Material[] materials, bool isTemporaryMesh)
            {
                Mesh = mesh;
                Matrix = matrix;
                Materials = materials;
                IsTemporaryMesh = isTemporaryMesh;
            }


            public Mesh Mesh { get; }

            public Matrix4x4 Matrix { get; }

            public Material[] Materials { get; }

            public bool IsTemporaryMesh { get; }
        }
    }
}
