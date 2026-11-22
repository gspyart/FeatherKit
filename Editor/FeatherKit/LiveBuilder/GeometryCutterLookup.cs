using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Какие вырезы (<see cref="GeometryCutter"/>) вычитаются из меша постройки: пересекают её габарит, резать её им разрешает правило (<see cref="LiveBuilderCutRules"/>),
    /// а точным способом её не режут.
    /// - на время прохода очереди пересборки вырезы сцены собираются один раз
    /// - отсюда же подпись: двинул вырез — постройка пересоберётся
    /// </summary>
    public static class GeometryCutterLookup
    {
        // Запас к габаритам, м: вырез, вплотную касающийся постройки, тоже считается.
        private const float BoundsMargin = 0.01f;

        // Запас к габариту, м: копия ближе этого к части здания — задевает её. Больше запаса при вычитании (<see cref="MeshSubtractionPolygons"/>).
        private const float TouchMargin = 0.02f;

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
                if (cutter != null && LiveBuilderCutRules.CanCutterCut(cutter, owner) && WorldBoundsOf(cutter).Intersects(worldBounds) && IsSubtractedFrom(cutter, owner))
                    cutters.Add(cutter);

            var ordered = KeysInOwnerSpace(owner, cutters);
            cutters.Clear();

            foreach (var (_, cutter) in ordered)
                cutters.Add(cutter);

            return cutters;
        }

        // Вырез-коробку своего объекта режут точно — солвер, проём по линии, дыра в плане; в вычитание она не идёт.
        private static bool IsSubtractedFrom(GeometryCutter cutter, Component owner)
        {
            return LiveBuilderCutRules.PreciseMethodOf(cutter, owner) == LiveBuilderCutMethod.MeshSubtraction;
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


        // Вместе со всеми копиями повтора.
        public static Bounds WorldBoundsOf(GeometryCutter cutter)
        {
            var bounds = GeometryCutterOutline.BoundsIn(cutter, GeometryCutterOutline.ShapeToWorld(cutter));
            return LiveBuilderArrayCopyPoses.HasCutterCopies(cutter) ? WithRepeatCopies(bounds, LiveBuilderArrayCopyPoses.CutterCopiesOf(cutter)) : bounds;
        }

        // Габарит одной копии повтора — сдвинутый на каждую копию; все пропущены — точка на месте выреза.
        private static Bounds WithRepeatCopies(Bounds firstCopy, IReadOnlyList<Vector3> repeatOffsets)
        {
            if (repeatOffsets.Count == 0)
                return new Bounds(firstCopy.center, Vector3.zero);

            var bounds = new Bounds(firstCopy.center + repeatOffsets[0], firstCopy.size);

            for (var index = 1; index < repeatOffsets.Count; index++)
                bounds.Encapsulate(new Bounds(firstCopy.center + repeatOffsets[index], firstCopy.size));

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


        // Для подписи части здания: только копии повтора, которые задевают её габарит до вырезов (в её осях), — порядок не важен.
        // Новый этаж добавляет окну копию, а подпись нижних этажей та же: их копии на прежних местах.
        public static string KeyOfTouchingCopies(Component owner, Bounds uncutLocalBounds, List<GeometryCutter> cutters)
        {
            var copyKeys = new List<Hash128>();
            uncutLocalBounds.Expand(TouchMargin * 2f);

            foreach (var cutter in cutters)
            {
                var firstCopy = GeometryCutterOutline.BoundsIn(cutter, owner.transform.worldToLocalMatrix * GeometryCutterOutline.ShapeToWorld(cutter));
                var hasRepeatCopies = LiveBuilderArrayCopyPoses.HasCutterCopies(cutter);
                Hash128? shapeKey = null;

                foreach (var repeatOffset in LiveBuilderArrayCopyPoses.CutterCopiesOf(cutter))
                {
                    var repeatShift = owner.transform.InverseTransformVector(repeatOffset);

                    if (!new Bounds(firstCopy.center + repeatShift, firstCopy.size).Intersects(uncutLocalBounds))
                        continue;

                    var copyKey = shapeKey ??= ShapeKeyOf(cutter, owner.transform, false);

                    if (hasRepeatCopies)
                        LiveBuilderArrayCopyPoses.AppendQuantized(ref copyKey, repeatShift);

                    copyKeys.Add(copyKey);
                }
            }

            if (copyKeys.Count == 0)
                return "";

            copyKeys.Sort();
            var key = new Hash128();

            foreach (var copyKey in copyKeys)
            {
                var appendedKey = copyKey;
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
                keys.Add((ShapeKeyOf(cutter, owner.transform, true), cutter));

            keys.Sort((first, second) => first.Key.CompareTo(second.Key));
            return keys;
        }

        // Форма и место выреза в осях space: сдвинули дыру — ключ другой. Место и размеры — с округлением,
        // место — через общего с постройкой предка (<see cref="LiveBuilderStableHash"/>): двигают постройку с вырезами — ключ тот же.
        private static Hash128 ShapeKeyOf(GeometryCutter cutter, Transform space, bool includesCopies)
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

            if (includesCopies)
                LiveBuilderArrayCopyPoses.AppendCutterKey(ref key, cutter, space);

            key.Append(BoxModelSideMaterials.KeyOf(cutter.RevealMaterial));

            // Материалы сторон — только если заданы: подписи старых построек остаются прежними.
            if (cutter.SideMaterials.HasAny)
                for (var side = 0; side < BoxFaces.Count; side++)
                    key.Append(BoxModelSideMaterials.KeyOf(GeometryCutterSolverBoxes.OwnMaterialOf(cutter, side)));

            // Текстура стенок — подгонки и плитки своих материалов, только если заданы.
            LiveBuilderTextureSignature.AppendFits(ref key, cutter.TextureFits);

            if (LiveBuilderMaterialTiling.HasAnySize)
            {
                LiveBuilderTextureSignature.AppendTile(ref key, cutter.RevealMaterial);

                for (var side = 0; side < BoxFaces.Count; side++)
                    LiveBuilderTextureSignature.AppendTile(ref key, GeometryCutterSolverBoxes.OwnMaterialOf(cutter, side));
            }

            // Что и где режет — только если не по умолчанию: подписи старых построек остаются прежними.
            if (cutter.CutTargets != LiveBuilderCutTargets.Everything || cutter.Scope != GeometryCutterScope.OwnObject)
            {
                key.Append((int)cutter.CutTargets);
                key.Append((int)cutter.Scope);
            }

            return key;
        }
    }
}
