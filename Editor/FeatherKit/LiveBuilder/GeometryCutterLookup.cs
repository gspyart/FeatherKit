using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Какие вырезы (<see cref="GeometryCutter"/>) режут постройку: включённые, пересекают её габарит, не её дочерние объекты.
    /// Вырез внутри самой постройки режет её всегда; чужой — если не из одного с ней префаба, а в открытом префабе — никогда:
    /// дверь-префаб режет стену, куда её поставили, а не свою раму.
    /// На время прохода очереди пересборки вырезы сцены собираются один раз. Отсюда же — подпись: двинул вырез — постройка пересоберётся.
    /// </summary>
    public static class GeometryCutterLookup
    {
        // Запас к габаритам, м: вырез, вплотную касающийся постройки, тоже считается.
        private const float BoundsMargin = 0.01f;

        // Вырезы сцены на время прохода; вне прохода — null, ищутся каждый раз.
        private static List<GeometryCutter> passCutters;


        // ── Проход ───────────────────────────────────────────────────────

        public static void BeginPass()
        {
            passCutters = AllCutters();
        }


        public static void EndPass()
        {
            passCutters = null;
        }

        private static List<GeometryCutter> AllCutters()
        {
            var cutters = new List<GeometryCutter>(StageUtility.GetCurrentStageHandle().FindComponentsOfType<GeometryCutter>());

            // Порядок — постоянный: от него зависит подпись, а пересборка из-за перестановки не нужна.
            cutters.Sort((first, second) => string.CompareOrdinal(first.GetEntityId().ToString(), second.GetEntityId().ToString()));
            return cutters;
        }


        // ── Поиск ────────────────────────────────────────────────────────

        // Вырезы, которые режут эту постройку в границах её меша (в мире).
        public static List<GeometryCutter> CuttersFor(Component owner, Bounds worldBounds)
        {
            var cutters = new List<GeometryCutter>();

            if (worldBounds.size == Vector3.zero)
                return cutters;

            worldBounds.Expand(BoundsMargin * 2f);

            foreach (var cutter in passCutters ?? AllCutters())
                if (cutter != null && CanCut(cutter, owner) && WorldBoundsOf(cutter).Intersects(worldBounds))
                    cutters.Add(cutter);

            return cutters;
        }

        // Включён и постройка — не его дочерний объект. Лежит внутри самой постройки — режет её всегда: так проём в здании, и в префабе здания тоже.
        // Иначе не режет то, что из его же префаба (дверь-префаб — свою раму), а в открытом префабе — ничего.
        private static bool CanCut(GeometryCutter cutter, Component owner)
        {
            if (!cutter.isActiveAndEnabled || owner.transform.IsChildOf(cutter.transform))
                return false;

            if (cutter.transform.IsChildOf(ConstructionOf(owner)))
                return true;

            if (PrefabStageUtility.GetPrefabStage(cutter.gameObject) != null)
                return false;

            var prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(cutter.gameObject);
            return prefabRoot == null || !owner.transform.IsChildOf(prefabRoot.transform);
        }

        // Постройка, чей это меш: у части здания — само здание.
        private static Transform ConstructionOf(Component owner)
        {
            var building = owner is SplineBuildingPart ? owner.GetComponentInParent<SplineBuilding>() : null;
            return building != null ? building.transform : owner.transform;
        }


        // Границы меша рендерера в мире; меша нет — пусто.
        public static Bounds WorldBoundsOf(Renderer renderer)
        {
            var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;

            if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
                return new Bounds();

            return WorldBoundsOf(filter.sharedMesh, renderer.transform);
        }


        // Границы меша в мире по его осям.
        public static Bounds WorldBoundsOf(Mesh mesh, Transform owner)
        {
            return WorldBoundsOf(mesh.bounds, owner);
        }


        // Границы в осях объекта — в мир.
        public static Bounds WorldBoundsOf(Bounds local, Transform owner)
        {
            var world = new Bounds(owner.TransformPoint(local.center), Vector3.zero);

            for (var index = 0; index < 8; index++)
            {
                var corner = local.center + Vector3.Scale(local.extents, new Vector3((index & 1) == 0 ? -1f : 1f, (index & 2) == 0 ? -1f : 1f, (index & 4) == 0 ? -1f : 1f));
                world.Encapsulate(owner.TransformPoint(corner));
            }

            return world;
        }


        // Вместе со всеми копиями вверх: от самого выреза до верхней копии.
        public static Bounds WorldBoundsOf(GeometryCutter cutter)
        {
            var bounds = GeometryCutterOutline.BoundsIn(cutter, cutter.transform.localToWorldMatrix);

            if (cutter.CopiesUp > 1)
                bounds.Encapsulate(GeometryCutterOutline.BoundsIn(cutter, GeometryCutterOutline.CopyToWorld(cutter, cutter.CopiesUp - 1)));

            return bounds;
        }


        // ── Подпись ──────────────────────────────────────────────────────

        // Для подписи постройки: где вырезы относительно неё и какой они формы. Нет вырезов — пусто, подпись как была.
        public static string KeyOf(Component owner, List<GeometryCutter> cutters)
        {
            if (cutters.Count == 0)
                return "";

            // Хеш, а не строка с матрицами: подпись сверяют на каждый проход очереди у каждой части здания.
            var key = new Hash128();

            foreach (var cutter in cutters)
            {
                var cutterToOwner = owner.transform.worldToLocalMatrix * cutter.transform.localToWorldMatrix;

                for (var index = 0; index < 16; index++)
                    key.Append(cutterToOwner[index]);

                key.Append((int)cutter.Shape);
                key.Append(cutter.Width);
                key.Append(cutter.Height);
                key.Append(cutter.Depth);
                key.Append(cutter.TopCornerRadius);
                key.Append(cutter.BottomCornerRadius);
                key.Append(cutter.CornerSegments);
                key.Append(cutter.CircleSegments);
                key.Append(cutter.CopiesUp);
                key.Append(cutter.CopyStep);
                key.Append(BoxModelSideMaterials.KeyOf(cutter.RevealMaterial));
            }

            return "|x" + key;
        }
    }
}
