using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Какие вырезы (<see cref="GeometryCutter"/>) режут постройку: включённые, пересекают её габарит, не её дочерние объекты, режут её вид
    /// (всё, только перекрытия, только стены). Где режет: в своей группе — только ближайшую постройку над вырезом и вложенное в неё,
    /// но не заходит во вложенную группу — другой объект из коробок (<see cref="LiveBuilderGroups"/>);
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
        // Порядок — по форме и месту выреза в осях постройки (тот же ключ, что в подписи), а не по номеру объекта и не по месту в мире:
        // номер новый в каждой сессии, а постройку с её вырезами двигают целиком — от порядка зависят меш и подпись.
        public static List<GeometryCutter> CuttersFor(Component owner, Bounds worldBounds)
        {
            var cutters = new List<GeometryCutter>();

            if (worldBounds.size == Vector3.zero)
                return cutters;

            worldBounds.Expand(BoundsMargin * 2f);

            foreach (var cutter in CandidatesFor(owner))
                if (cutter != null && CanCut(cutter, owner) && WorldBoundsOf(cutter).Intersects(worldBounds))
                    cutters.Add(cutter);

            var ordered = KeysInOwnerSpace(owner, cutters);
            cutters.Clear();

            foreach (var (_, cutter) in ordered)
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
                return group != null && construction.IsChildOf(group) && IsInSameModelGroup(cutter, construction);
            }

            if (cutter.transform.IsChildOf(construction))
                return true;

            if (PrefabStageUtility.GetPrefabStage(cutter.gameObject) != null || BoxModelMeshStorage.IsLoadedPrefabContents(cutter.gameObject.scene))
                return false;

            var prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(cutter.gameObject);
            return prefabRoot == null || !owner.transform.IsChildOf(prefabRoot.transform);
        }

        // Режет ли вырез этот вид постройки: всё — любую; только перекрытия — пол, перекрытия и крышу зданий; только стены — стены по линии и стены зданий.
        // Здание целиком (его спрашивают, когда ищут вырезы снаружи префаба) — и стены, и перекрытия: подходит любой вид.
        private static bool IsTargetKind(LiveBuilderCutTargets targets, Component owner)
        {
            if (owner is SplineBuilding)
                return true;

            var buildingPart = owner as SplineBuildingPart;

            return targets switch
            {
                LiveBuilderCutTargets.SlabsOnly => buildingPart != null && buildingPart.Kind != SplineBuildingPartKind.Walls,
                LiveBuilderCutTargets.WallsOnly => owner is SplineWall || buildingPart != null && buildingPart.Kind == SplineBuildingPartKind.Walls,
                _ => true
            };
        }

        // Постройка в той же группе, что и вырез: ближайший объект из коробок над ней (или она сама) — тот же, что над вырезом.
        // Вложенная группа — граница: вырез снаружи в неё не заходит, а вырез внутри неё режет её саму и всё, что в ней.
        private static bool IsInSameModelGroup(GeometryCutter cutter, Transform construction)
        {
            return LiveBuilderGroups.GroupOf(construction) == LiveBuilderGroups.GroupOf(cutter.transform.parent);
        }

        // Режет только в своей группе: не «Всё вокруг» и над проёмом есть постройка. Старое «Своя группа» — как «Авто»:
        // разница была только без постройки, где оно молча ничего не резало.
        private static bool CutsOnlyOwnGroup(GeometryCutter cutter)
        {
            return cutter.Scope != GeometryCutterScope.Everything && OwnGroupOf(cutter) != null;
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
            // Ключи — по порядку в осях постройки, как у CuttersFor: подпись не зависит от того, в каком порядке пришёл список.
            var key = new Hash128();

            foreach (var (cutterKey, _) in KeysInOwnerSpace(owner, cutters))
            {
                var appendedKey = cutterKey;
                key.Append(ref appendedKey);
            }

            return CutterKeyPrefix + key;
        }


        // Подпись без части вырезов — то, что собрано из самой постройки.
        public static string WithoutCutterKey(string signature)
        {
            var index = signature.IndexOf(CutterKeyPrefix, System.StringComparison.Ordinal);
            return index >= 0 ? signature.Substring(0, index) : signature;
        }

        // Ключ каждого выреза в осях постройки — и вырезы по этим ключам. Один порядок и для вычитания, и для подписи.
        private static List<(Hash128 Key, GeometryCutter Cutter)> KeysInOwnerSpace(Component owner, List<GeometryCutter> cutters)
        {
            var keys = new List<(Hash128 Key, GeometryCutter Cutter)>(cutters.Count);

            foreach (var cutter in cutters)
                keys.Add((ShapeKeyOf(cutter, owner.transform), cutter));

            keys.Sort((first, second) => first.Key.CompareTo(second.Key));
            return keys;
        }

        // Форма и место выреза в осях space: сдвинули дыру — ключ другой. Место и размеры — с округлением,
        // место — через общего с постройкой предка (<see cref="LiveBuilderStableHash"/>): двигают постройку с вырезами — ключ тот же.
        private static Hash128 ShapeKeyOf(GeometryCutter cutter, Transform space)
        {
            var key = new Hash128();
            var shapeToSpace = LiveBuilderStableHash.RelativeMatrix(space, cutter.transform) * Matrix4x4.Translate(cutter.ShapeOffset);
            LiveBuilderStableHash.AppendQuantizedMatrix(ref key, shapeToSpace);
            key.Append((int)cutter.Shape);
            LiveBuilderStableHash.AppendQuantized(ref key, cutter.Width);
            LiveBuilderStableHash.AppendQuantized(ref key, cutter.Height);
            LiveBuilderStableHash.AppendQuantized(ref key, cutter.Depth);
            LiveBuilderStableHash.AppendQuantized(ref key, cutter.TopCornerRadius);
            LiveBuilderStableHash.AppendQuantized(ref key, cutter.BottomCornerRadius);
            key.Append(cutter.CornerSegments);
            key.Append(cutter.CircleSegments);
            key.Append(cutter.CopiesUp);
            LiveBuilderStableHash.AppendQuantized(ref key, cutter.CopyStep);
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
