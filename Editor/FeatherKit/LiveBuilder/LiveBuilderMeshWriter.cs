using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    /// дальше всё одинаково: свой файл меша, вырезы (<see cref="GeometryCutterApplier"/>), материалы рендерера,
    /// запись файла на диск (<see cref="BoxModelMeshStorage"/>) и коллайдер по мешу.
    /// Чужой файл не перезаписывается никогда, кто бы ни звал пересборку — очередь или инструмент сразу после рисования:
    /// общий с копией (Ctrl+D), меш префаба, файл другой сцены или префаба — постройка получает свой файл.
    /// </summary>
    public static class LiveBuilderMeshWriter
    {
        // Сколько объектов сцены смотрит на каждый меш. На время прохода очереди считается один раз; вне прохода — null, считается заново.
        private static Dictionary<Mesh, int> passMeshUserCounts;
        private static bool isPassRunning;


        // ── Проход очереди ───────────────────────────────────────────────

        public static void BeginPass()
        {
            isPassRunning = true;
            passMeshUserCounts = null;
        }


        public static void EndPass()
        {
            isPassRunning = false;
            passMeshUserCounts = null;
        }


        // ── Запись ───────────────────────────────────────────────────────

        // fill кладёт в меш геометрию и отдаёт материалы его частей. Вырезы — known, а нет их — те, что режут собранную геометрию.
        // Файл — свой у постройки (fileName — имя нового файла; пусто — имя объекта).
        // Новый файл встаёт на объект сразу, до сборки: упала сборка — следующий проход возьмёт этот же файл, а не заведёт ещё один.
        public static LiveBuilderMeshWriteResult Write(Component owner, string fileName, System.Func<Mesh, Material[]> fill, List<GeometryCutter> knownCutters = null)
        {
            var filter = owner.GetComponent<MeshFilter>();
            ReleaseIfNotOwn(owner, filter);
            var mesh = BoxModelMeshStorage.GetOrCreateMeshFile(owner, fileName);
            var isNewMesh = filter.sharedMesh != mesh;

            if (isNewMesh)
            {
                filter.sharedMesh = mesh;
                EditorUtility.SetDirty(filter);
                LiveBuilderSyncEdits.RecordPrefabInstance(filter);
            }

            var materials = fill(mesh);
            var cutters = knownCutters ?? GeometryCutterLookup.CuttersFor(owner, GeometryCutterLookup.WorldBoundsOf(mesh, owner.transform));
            materials = GeometryCutterApplier.Cut(mesh, owner.transform, cutters, materials);

            EditorUtility.SetDirty(mesh);
            BoxModelMeshStorage.RequestSave(mesh);

            var renderer = owner.GetComponent<MeshRenderer>();

            if (!AreSameMaterials(renderer.sharedMaterials, materials))
            {
                renderer.sharedMaterials = materials;
                LiveBuilderSyncEdits.RecordPrefabInstance(renderer);
            }

            return new LiveBuilderMeshWriteResult(mesh, cutters, isNewMesh);
        }

        // Меш не свой — отпустить: соберётся свой файл. Не свой — не файл Live Builder, лежит в папке другой сцены или префаба,
        // это меш префаба, из которого сделана постройка, или на него смотрит ещё кто-то в сцене.
        private static void ReleaseIfNotOwn(Component owner, MeshFilter filter)
        {
            var mesh = filter.sharedMesh;

            if (mesh == null || BoxModelMeshStorage.IsOwnMeshFile(owner, mesh) && !IsPrefabSourceMesh(filter, mesh) && !IsSharedWithOthers(owner, mesh))
                return;

            // Отпущенный меш — на одного пользователя меньше: вторая копия оставит его себе, а не заведёт ещё один файл.
            if (passMeshUserCounts != null && passMeshUserCounts.TryGetValue(mesh, out var count))
                passMeshUserCounts[mesh] = count - 1;

            filter.sharedMesh = null;
        }

        private static bool IsPrefabSourceMesh(MeshFilter filter, Mesh mesh)
        {
            var sourceFilter = PrefabUtility.IsPartOfPrefabInstance(filter) ? PrefabUtility.GetCorrespondingObjectFromSource(filter) : null;
            return sourceFilter != null && sourceFilter.sharedMesh == mesh;
        }

        private static bool IsSharedWithOthers(Component owner, Mesh mesh)
        {
            var isPassScene = isPassRunning && !BoxModelMeshStorage.IsLoadedPrefabContents(owner.gameObject.scene);
            var counts = isPassScene ? passMeshUserCounts ??= CountMeshUsers(owner) : CountMeshUsers(owner);
            return counts.TryGetValue(mesh, out var count) && count > 1;
        }

        // Все фильтры мешей в сцене постройки, а у префаба, загруженного для пересборки, — в нём.
        private static Dictionary<Mesh, int> CountMeshUsers(Component owner)
        {
            var counts = new Dictionary<Mesh, int>();
            var scene = owner.gameObject.scene;
            var filters = BoxModelMeshStorage.IsLoadedPrefabContents(scene) ? FiltersInScene(scene) : StageUtility.GetCurrentStageHandle().FindComponentsOfType<MeshFilter>();

            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;

                if (mesh != null)
                    counts[mesh] = counts.TryGetValue(mesh, out var count) ? count + 1 : 1;
            }

            return counts;
        }

        private static MeshFilter[] FiltersInScene(Scene scene)
        {
            var filters = new List<MeshFilter>();

            foreach (var root in scene.GetRootGameObjects())
                filters.AddRange(root.GetComponentsInChildren<MeshFilter>(true));

            return filters.ToArray();
        }

        private static bool AreSameMaterials(Material[] current, Material[] wanted)
        {
            if (current.Length != wanted.Length)
                return false;

            for (var index = 0; index < current.Length; index++)
                if (current[index] != wanted[index])
                    return false;

            return true;
        }


        // Коллайдер постройки по мешу для столкновений; выключен у постройки — коллайдера нет. Пустой меш — коллайдер без меша.
        public static void UpdateCollider(Component owner, bool hasCollider, Mesh colliderMesh)
        {
            var meshCollider = owner.GetComponent<MeshCollider>();

            if (!hasCollider)
            {
                RemoveCollider(meshCollider);
                return;
            }

            if (meshCollider == null)
                meshCollider = owner.gameObject.AddComponent<MeshCollider>();

            if (!meshCollider.enabled)
                meshCollider.enabled = true;

            // Сброс перед присвоением — иначе Unity не пересчитает коллайдер у того же меша с новыми вершинами.
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = colliderMesh != null && colliderMesh.vertexCount > 0 ? colliderMesh : null;
            LiveBuilderSyncEdits.RecordPrefabInstance(meshCollider);
        }

        // Коллайдер из префаба у экземпляра удалить нельзя — он выключается.
        private static void RemoveCollider(MeshCollider meshCollider)
        {
            if (meshCollider == null)
                return;

            if (!PrefabUtility.IsPartOfPrefabInstance(meshCollider) || PrefabUtility.IsAddedComponentOverride(meshCollider))
            {
                Object.DestroyImmediate(meshCollider);
                return;
            }

            if (!meshCollider.enabled)
                return;

            meshCollider.enabled = false;
            LiveBuilderSyncEdits.RecordPrefabInstance(meshCollider);
        }
    }
}
