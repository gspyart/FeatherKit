using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Какие вырезы (<see cref="GeometryCutter"/>) режут постройку: включённые, пересекают её габарит, не её дочерние объекты, режут её вид
    /// (всё, только перекрытия, только стены). Где режет: в своей группе — только ближайшую постройку над вырезом и вложенное в неё;
    /// везде — вырез внутри самой постройки режет её всегда, чужую — если не из одного с ней префаба, а в открытом префабе — никогда:
    /// дверь-префаб режет стену, куда её поставили, а не свою раму.
    /// На время прохода очереди пересборки вырезы сцены собираются один раз. Отсюда же — подпись: двинул вырез — постройка пересоберётся.
    /// </summary>
    public static class GeometryCutterLookup
    {
        // Запас к габаритам, м: вырез, вплотную касающийся постройки, тоже считается.
        private const float BoundsMargin = 0.01f;

        // С чего в подписи постройки начинается часть вырезов.
        private const string CutterKeyPrefix = "|x";

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
            return new List<GeometryCutter>(StageUtility.GetCurrentStageHandle().FindComponentsOfType<GeometryCutter>());
        }


        // ── Поиск ────────────────────────────────────────────────────────

        // Вырезы, которые режут эту постройку в границах её меша (в мире).
        // Порядок — по форме и месту выреза, а не по номеру объекта: номер новый в каждой сессии, а от порядка зависят меш и подпись.
        public static List<GeometryCutter> CuttersFor(Component owner, Bounds worldBounds)
        {
            var cutters = new List<GeometryCutter>();

            if (worldBounds.size == Vector3.zero)
                return cutters;

            worldBounds.Expand(BoundsMargin * 2f);
            var keys = new List<(Hash128 Key, GeometryCutter Cutter)>();

            foreach (var cutter in CandidatesFor(owner))
                if (cutter != null && CanCut(cutter, owner) && WorldBoundsOf(cutter).Intersects(worldBounds))
                    keys.Add((ShapeKeyOf(cutter, Matrix4x4.identity), cutter));

            keys.Sort((first, second) => first.Key.CompareTo(second.Key));

            foreach (var (_, cutter) in keys)
                cutters.Add(cutter);

            return cutters;
        }

        // Вырезы сцены постройки. Префаб, загруженный для пересборки, — отдельная сцена: его вырезы — в нём самом.
        private static IEnumerable<GeometryCutter> CandidatesFor(Component owner)
        {
            var scene = owner.gameObject.scene;

            if (!BoxModelMeshStorage.IsLoadedPrefabContents(scene))
                return passCutters ?? AllCutters();

            var cutters = new List<GeometryCutter>();

            foreach (var root in scene.GetRootGameObjects())
                cutters.AddRange(root.GetComponentsInChildren<GeometryCutter>(true));

            return cutters;
        }

        // Включён, постройка — не его дочерний объект и того вида, что он режет. Дальше — где режет:
        // в своей группе — только постройки внутри неё; везде — внутри самой постройки режет её всегда (так проём в здании, и в префабе здания тоже),
        // иначе не режет то, что из его же префаба (дверь-префаб — свою раму), а в открытом префабе — ничего.
        private static bool CanCut(GeometryCutter cutter, Component owner)
        {
            if (!cutter.isActiveAndEnabled || owner.transform.IsChildOf(cutter.transform) || !IsTargetKind(cutter.CutTargets, owner))
                return false;

            var construction = ConstructionOf(owner);

            if (CutsOnlyOwnGroup(cutter))
            {
                var group = OwnGroupOf(cutter);
                return group != null && construction.IsChildOf(group);
            }

            if (cutter.transform.IsChildOf(construction))
                return true;

            if (PrefabStageUtility.GetPrefabStage(cutter.gameObject) != null || BoxModelMeshStorage.IsLoadedPrefabContents(cutter.gameObject.scene))
                return false;

            var prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(cutter.gameObject);
            return prefabRoot == null || !owner.transform.IsChildOf(prefabRoot.transform);
        }

        // Режет ли вырез этот вид постройки: всё — любую; только перекрытия — пол, перекрытия и крышу зданий; только стены — стены по линии и стены зданий.
        private static bool IsTargetKind(LiveBuilderCutTargets targets, Component owner)
        {
            var buildingPart = owner as SplineBuildingPart;

            return targets switch
            {
                LiveBuilderCutTargets.SlabsOnly => buildingPart != null && buildingPart.Kind != SplineBuildingPartKind.Walls,
                LiveBuilderCutTargets.WallsOnly => owner is SplineWall || buildingPart != null && buildingPart.Kind == SplineBuildingPartKind.Walls,
                _ => true
            };
        }

        // Режет только в своей группе: так задано, или «авто» и над вырезом есть постройка.
        private static bool CutsOnlyOwnGroup(GeometryCutter cutter)
        {
            return cutter.Scope switch
            {
                GeometryCutterScope.OwnGroup => true,
                GeometryCutterScope.Everything => false,
                _ => OwnGroupOf(cutter) != null
            };
        }


        // Своя группа выреза — ближайшая постройка над ним: объект из коробок, стена по линии или здание. Нет такой — null.
        public static Transform OwnGroupOf(GeometryCutter cutter)
        {
            for (var node = cutter.transform.parent; node != null; node = node.parent)
                if (node.GetComponent<BoxModel>() != null || node.GetComponent<SplineWall>() != null || node.GetComponent<SplineBuilding>() != null)
                    return node;

            return null;
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
            var bounds = GeometryCutterOutline.BoundsIn(cutter, GeometryCutterOutline.CopyToWorld(cutter, 0));

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
                var cutterKey = ShapeKeyOf(cutter, owner.transform.worldToLocalMatrix);
                key.Append(ref cutterKey);
            }

            return CutterKeyPrefix + key;
        }


        // Подпись без части вырезов — то, что собрано из самой постройки.
        public static string WithoutCutterKey(string signature)
        {
            var index = signature.IndexOf(CutterKeyPrefix, System.StringComparison.Ordinal);
            return index >= 0 ? signature.Substring(0, index) : signature;
        }

        // Форма и место выреза в осях toSpace: сдвинули дыру — ключ другой.
        private static Hash128 ShapeKeyOf(GeometryCutter cutter, Matrix4x4 toSpace)
        {
            var key = new Hash128();
            var cutterToSpace = toSpace * GeometryCutterOutline.ShapeToWorld(cutter);
            key.Append(ref cutterToSpace);
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

            // Что и где режет — только если не по умолчанию: подписи старых построек остаются прежними.
            if (cutter.CutTargets != LiveBuilderCutTargets.Everything || cutter.Scope != GeometryCutterScope.Automatic)
            {
                key.Append((int)cutter.CutTargets);
                key.Append((int)cutter.Scope);
            }

            return key;
        }
    }
}
