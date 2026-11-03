using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Что вышло из записи меша постройки: сам меш, вырезы, что его прорезали, и поменялся ли файл меша у объекта.</summary>
    public readonly struct LiveBuilderMeshWriteResult
    {
        public LiveBuilderMeshWriteResult(Mesh mesh, List<GeometryCutter> cutters, bool isNewMesh)
        {
            Mesh = mesh;
            Cutters = cutters;
            IsNewMesh = isNewMesh;
        }


        public Mesh Mesh { get; }
        public List<GeometryCutter> Cutters { get; }
        public bool IsNewMesh { get; }
    }


    /// <summary>
    /// Общий конец сборки у всех построек Live Builder — объекта из коробок, стены по линии, части здания. Геометрию строит пересборщик,
    /// дальше всё одинаково: свой файл меша (общий с копией или префабом не перезаписывается), вырезы (<see cref="GeometryCutterApplier"/>),
    /// материалы рендерера, запись файла на диск (<see cref="BoxModelMeshStorage"/>) и коллайдер по мешу.
    /// </summary>
    public static class LiveBuilderMeshWriter
    {
        // ── Общие меши ───────────────────────────────────────────────────

        // Сколько построек смотрит на каждый меш: больше одной — копии, файл общий.
        public static Dictionary<Mesh, int> CountMeshUsers<T>(IEnumerable<T> owners) where T : Component
        {
            var counts = new Dictionary<Mesh, int>();

            foreach (var owner in owners)
            {
                var mesh = owner.GetComponent<MeshFilter>().sharedMesh;

                if (mesh != null)
                    counts[mesh] = counts.TryGetValue(mesh, out var count) ? count + 1 : 1;
            }

            return counts;
        }


        // Меш делят несколько построек или это меш префаба, из которого сделана постройка, — отпустить его: соберётся свой файл.
        public static void ReleaseIfShared(Component owner, Dictionary<Mesh, int> meshUserCounts)
        {
            var filter = owner.GetComponent<MeshFilter>();
            var mesh = filter.sharedMesh;

            if (mesh == null)
                return;

            var isCopied = meshUserCounts.TryGetValue(mesh, out var count) && count > 1;
            var sourceFilter = PrefabUtility.IsPartOfPrefabInstance(filter) ? PrefabUtility.GetCorrespondingObjectFromSource(filter) : null;

            if (isCopied || sourceFilter != null && sourceFilter.sharedMesh == mesh)
                filter.sharedMesh = null;
        }


        // ── Запись ───────────────────────────────────────────────────────

        // fill кладёт в меш геометрию и отдаёт материалы его частей. Вырезы — known, а нет их — те, что режут собранную геометрию.
        // Файл — свой у постройки (fileName — имя нового файла; пусто — имя объекта).
        public static LiveBuilderMeshWriteResult Write(Component owner, string fileName, System.Func<Mesh, Material[]> fill, List<GeometryCutter> knownCutters = null)
        {
            var filter = owner.GetComponent<MeshFilter>();
            var mesh = BoxModelMeshStorage.GetOrCreateMeshFile(owner, fileName);
            var materials = fill(mesh);

            var cutters = knownCutters ?? GeometryCutterLookup.CuttersFor(owner, GeometryCutterLookup.WorldBoundsOf(mesh, owner.transform));
            materials = GeometryCutterApplier.Cut(mesh, owner.transform, cutters, materials);

            EditorUtility.SetDirty(mesh);
            BoxModelMeshStorage.RequestSave(mesh);

            var isNewMesh = filter.sharedMesh != mesh;

            if (isNewMesh)
            {
                filter.sharedMesh = mesh;
                EditorUtility.SetDirty(filter);
            }

            owner.GetComponent<MeshRenderer>().sharedMaterials = materials;
            return new LiveBuilderMeshWriteResult(mesh, cutters, isNewMesh);
        }


        // Коллайдер постройки по мешу для столкновений; выключен у постройки — коллайдера нет. Пустой меш — коллайдер без меша.
        public static void UpdateCollider(Component owner, bool hasCollider, Mesh colliderMesh)
        {
            var meshCollider = owner.GetComponent<MeshCollider>();

            if (!hasCollider)
            {
                if (meshCollider != null)
                    Object.DestroyImmediate(meshCollider);

                return;
            }

            if (meshCollider == null)
                meshCollider = owner.gameObject.AddComponent<MeshCollider>();

            // Сброс перед присвоением — иначе Unity не пересчитает коллайдер у того же меша с новыми вершинами.
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = colliderMesh != null && colliderMesh.vertexCount > 0 ? colliderMesh : null;
        }
    }
}
