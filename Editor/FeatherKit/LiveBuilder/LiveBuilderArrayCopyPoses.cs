using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Копии узла с учётом всех повторов (<see cref="LiveBuilderArray"/>) на нём и над ним, перемноженных: сдвиги в мире от оригинала.
    /// - одно место для сборщика коробок, вычитания, стен, зданий, подписей, рамок и ручек; копии одного повтора — Fill, вдоль стены, узор
    /// - вариант узора — только копии своей буквы; настоящая часть (<see cref="LiveBuilderArrayRealParts"/>) — граница расчёта
    /// - на время прохода очереди пересборки — помнятся; предел копий на цепочку повторов — сверх не строятся, с предупреждением
    /// </summary>
    public static class LiveBuilderArrayCopyPoses
    {
        // Узел без повторов — одна копия: он сам на своём месте. Один общий список: по нему видно, что повторов нет.
        private static readonly IReadOnlyList<Vector3> OriginalOnly = new[] { Vector3.zero };

        // Повторы, о пределе которых уже предупредили: одно предупреждение, а не на каждую пересборку.
        private static readonly HashSet<EntityId> ArraysWarnedAboutLimit = new();

        // Копии узлов на время прохода; вне прохода — null.
        private static Dictionary<Transform, IReadOnlyList<Vector3>> passCopies;


        // ── Проход ───────────────────────────────────────────────────────

        public static void BeginPass()
        {
            passCopies = new Dictionary<Transform, IReadOnlyList<Vector3>>();
        }


        public static void EndPass()
        {
            passCopies = null;
        }


        // ── Копии узла ───────────────────────────────────────────────────

        // Сдвиги копий в мире: внешний повтор — медленнее всего. Без повторов — один нулевой сдвиг; всё пропущено — пусто.
        public static IReadOnlyList<Vector3> CopiesOf(Transform node)
        {
            if (node == null)
                return OriginalOnly;

            if (passCopies != null && passCopies.TryGetValue(node, out var cached))
                return cached;

            var copies = node.GetComponentInParent<LiveBuilderArray>(true) == null ? OriginalOnly : CopiesUnderArraysOf(node);

            if (passCopies != null)
                passCopies[node] = copies;

            return copies;
        }

        // Настоящая часть и контейнер её копий — граница: их копирует LiveBuilderArraySync объектами, расчётом над ними повторов нет.
        private static IReadOnlyList<Vector3> CopiesUnderArraysOf(Transform node)
        {
            if (LiveBuilderArrayRealParts.IsBoundary(node))
                return OriginalOnly;

            var copiesAbove = ArraysAboveOf(node);
            var array = node.GetComponent<LiveBuilderArray>();
            return array == null ? copiesAbove : Multiply(copiesAbove, OwnCopiesOf(array, copiesAbove.Count));
        }


        // Копии узла без его своего повтора — только повторы над ним. Настоящая часть — одна, на месте.
        public static IReadOnlyList<Vector3> CopiesAboveOf(Transform node)
        {
            return node == null || LiveBuilderArrayRealParts.IsBoundary(node) ? OriginalOnly : ArraysAboveOf(node);
        }

        // Вариант узора берёт только копии со своей буквой.
        private static IReadOnlyList<Vector3> ArraysAboveOf(Transform node)
        {
            var parent = node.parent;
            var parentArray = parent != null ? parent.GetComponent<LiveBuilderArray>() : null;
            var variant = parentArray != null ? LiveBuilderArrayPattern.VariantIndexOf(parentArray, node) : LiveBuilderArrayPattern.AllVariants;
            return variant == LiveBuilderArrayPattern.AllVariants ? CopiesOf(parent) : VariantCopiesOf(parentArray, variant);
        }

        // Повторы над повтором — и его копии с буквой этого варианта.
        private static IReadOnlyList<Vector3> VariantCopiesOf(LiveBuilderArray array, int variant)
        {
            if (LiveBuilderArrayRealParts.IsBoundary(array.transform))
                return OriginalOnly;

            var copiesAbove = CopiesOf(array.transform.parent);
            return Multiply(copiesAbove, OwnCopiesOf(array, copiesAbove.Count, variant));
        }

        // Каждая копия сверху — со всеми своими копиями этого повтора.
        private static IReadOnlyList<Vector3> Multiply(IReadOnlyList<Vector3> copiesAbove, List<Vector3> ownCopies)
        {
            var copies = new List<Vector3>(copiesAbove.Count * ownCopies.Count);

            foreach (var above in copiesAbove)
                foreach (var own in ownCopies)
                    copies.Add(above + own);

            return copies;
        }


        // Над узлом или на нём есть повтор — у него копии из повторов.
        public static bool HasArrayCopies(Transform node)
        {
            return !ReferenceEquals(CopiesOf(node), OriginalOnly);
        }


        // Сдвиги копий выреза — повторы над ним.
        public static IReadOnlyList<Vector3> CutterCopiesOf(GeometryCutter cutter)
        {
            return CopiesOf(cutter.transform);
        }


        public static bool HasCutterCopies(GeometryCutter cutter)
        {
            return !ReferenceEquals(CutterCopiesOf(cutter), OriginalOnly);
        }


        // Копия узла в мире; без сдвига — матрица самого узла, до бита.
        public static Matrix4x4 LocalToWorldOfCopy(Transform node, Vector3 copyOffset)
        {
            return copyOffset == Vector3.zero ? node.localToWorldMatrix : Matrix4x4.Translate(copyOffset) * node.localToWorldMatrix;
        }


        public static Matrix4x4 WorldToLocalOfCopy(Transform node, Vector3 copyOffset)
        {
            return copyOffset == Vector3.zero ? node.worldToLocalMatrix : node.worldToLocalMatrix * Matrix4x4.Translate(-copyOffset);
        }


        // Сдвиг копии в осях самого узла: так копию строят постройки, у которых меш в своих осях.
        public static Vector3 LocalOffsetOf(Transform node, Vector3 copyOffset)
        {
            return copyOffset == Vector3.zero ? Vector3.zero : node.InverseTransformVector(copyOffset);
        }


        // Сдвиги копий одного повтора в мире, сколько влезает в предел вместе с copiesAbove копиями повторов над ним.
        // variant — только копии с этой буквой узора; повёрнутые на изгибе стены расчётом не строятся.
        public static List<Vector3> OwnCopiesOf(LiveBuilderArray array, int copiesAbove, int variant = LiveBuilderArrayPattern.AllVariants)
        {
            var copies = new List<Vector3>();

            foreach (var copy in PlacedCopiesOf(array, copiesAbove))
                if (!copy.IsTurned && copy.Shows(variant))
                    copies.Add(copy.Shift);

            return copies;
        }


        // Копии одного повтора: перенос в мире и вариант узора. По осям — сдвиг в осях повтора; вдоль стены — по её линии, угол пропущен.
        public static List<LiveBuilderArrayCopy> PlacedCopiesOf(LiveBuilderArray array, int copiesAbove)
        {
            var wallPath = array.Along == LiveBuilderArrayAlong.Wall ? LiveBuilderArrayWallPath.Of(array) : null;
            var context = FilledContextOf(array, wallPath);
            var limit = Mathf.Max(1, LiveBuilderArray.MaxCopies / Mathf.Max(1, copiesAbove));
            var poses = LiveBuilderArrayLayout.PosesOf(array, context, limit);
            WarnIfOverLimit(array, (long)copiesAbove * LiveBuilderArrayLayout.TotalCountOf(array, context));

            var rotation = array.transform.rotation;
            var copies = new List<LiveBuilderArrayCopy>(poses.Count);

            foreach (var pose in poses)
            {
                if (wallPath == null)
                    copies.Add(LiveBuilderArrayCopy.Shifted(rotation * pose.Offset, pose.Variant));
                else if (wallPath.TryGetCopy(array, pose.Offset, pose.Variant, out var copy))
                    copies.Add(copy);
            }

            return copies;
        }

        // Упёрлись в предел — предупредить один раз; стало меньше — снова предупредим, если упрётся опять.
        private static void WarnIfOverLimit(LiveBuilderArray array, long chainCount)
        {
            var arrayId = array.GetEntityId();

            if (chainCount <= LiveBuilderArray.MaxCopies)
            {
                ArraysWarnedAboutLimit.Remove(arrayId);
                return;
            }

            if (ArraysWarnedAboutLimit.Add(arrayId))
                Debug.LogWarning("Live Builder: '" + array.name + "' repeats to " + chainCount + " copies together with the repeats above it — only " +
                                 LiveBuilderArray.MaxCopies + " are built. Lower the counts.", array);
        }


        // Здание, в котором стоит повтор: его этаж и высота этажа; в этаже здания — этаж контейнера, на крыше — до верха нечего.
        // Не в здании — высота этажа уровня.
        public static LiveBuilderArrayContext ContextOf(LiveBuilderArray array)
        {
            var container = SplineBuildingStoreys.StoreyOf(array.transform);
            var containerBuilding = container != null ? container.Building : null;

            if (containerBuilding != null)
                return new LiveBuilderArrayContext(containerBuilding.StoreyHeight,
                    container.IsRoof ? 0 : Mathf.Max(1, containerBuilding.StoreyCount - container.Storey), !container.IsRoof);

            var building = BuildingAround(array);

            if (building != null)
            {
                var storey = building.StoreyAtHeight(array.transform.position.y);
                return new LiveBuilderArrayContext(building.StoreyHeight, building.StoreyCount - storey);
            }

            var needsLevelHeight = array.StepYIsStoreyHeight && array.CountY > 1;
            return new LiveBuilderArrayContext(needsLevelHeight ? LiveBuilderStoreys.StoreyHeight : 0f, 0);
        }

        // То же с длиной ряда и шириной копии для Fill: вдоль стены «вся сторона» — от её начала, по осям — от края оригинала.
        public static LiveBuilderArrayContext FilledContextOf(LiveBuilderArray array)
        {
            return FilledContextOf(array, array.Along == LiveBuilderArrayAlong.Wall ? LiveBuilderArrayWallPath.Of(array) : null);
        }

        private static LiveBuilderArrayContext FilledContextOf(LiveBuilderArray array, LiveBuilderArrayWallPath wallPath)
        {
            var context = ContextOf(array);

            if (array.Mode != LiveBuilderArrayMode.Fill)
                return context;

            if (wallPath != null)
                return array.Length > 0f
                    ? context.WithFill(array.Length, wallPath.CopyWidth, 0f)
                    : context.WithFill(wallPath.SideLength, wallPath.CopyWidth, wallPath.FirstCopyShiftFor(array.Margin));

            var extent = LiveBuilderArrayCopyBounds.ExtentAlong(array, LiveBuilderArrayCopyBounds.CornersOf(array), array.transform.right);
            return context.WithFill(array.Length, extent.y - extent.x, 0f);
        }

        // Здание над повтором, а не само здание с повтором: копии здания по этажам самого себя не считаются.
        private static SplineBuilding BuildingAround(LiveBuilderArray array)
        {
            var parent = array.transform.parent;
            return parent != null ? parent.GetComponentInParent<SplineBuilding>(true) : null;
        }


        // ── Подпись ──────────────────────────────────────────────────────

        // Сдвиги копий в осях space, с округлением; без повторов ничего не добавляет — подписи старых построек те же.
        public static void AppendKey(ref Hash128 hash, Transform node, Transform space)
        {
            if (HasArrayCopies(node))
                AppendKey(ref hash, CopiesOf(node), space);
        }


        public static void AppendCutterKey(ref Hash128 hash, GeometryCutter cutter, Transform space)
        {
            if (HasCutterCopies(cutter))
                AppendKey(ref hash, CutterCopiesOf(cutter), space);
        }

        private static void AppendKey(ref Hash128 hash, IReadOnlyList<Vector3> copies, Transform space)
        {
            hash.Append(copies.Count);

            foreach (var copy in copies)
                AppendQuantized(ref hash, space.InverseTransformVector(copy));
        }


        public static void AppendQuantized(ref Hash128 hash, Vector3 offset)
        {
            LiveBuilderStableHash.AppendQuantized(ref hash, offset.x);
            LiveBuilderStableHash.AppendQuantized(ref hash, offset.y);
            LiveBuilderStableHash.AppendQuantized(ref hash, offset.z);
        }


        // ── Предупреждения ───────────────────────────────────────────────

        // Что не так с повтором — по-английски, для инспектора: пропуски не читаются, копий больше предела, «до верха» не в здании,
        // вдоль стены без стены, узор без вариантов, копии на изгибе.
        public static List<string> WarningsOf(LiveBuilderArray array)
        {
            var warnings = new List<string>();
            var context = FilledContextOf(array);
            var counts = LiveBuilderArrayLayout.CountsOf(array, context);
            var axisNames = new[] { "X", "Y", "Z" };

            for (var axis = 0; axis < 3; axis++)
                if (!LiveBuilderArraySkipList.TryParse(array.SkipOf(axis), counts[axis], out _, out var error))
                    warnings.Add("Skip " + axisNames[axis] + ": " + error);

            var copiesAbove = CopiesOf(array.transform.parent).Count;
            var chainCount = (long)copiesAbove * LiveBuilderArrayLayout.TotalCountOf(array, context);

            if (chainCount > LiveBuilderArray.MaxCopies)
                warnings.Add("Too many copies: " + chainCount + " together with the repeats above. Only " + LiveBuilderArray.MaxCopies + " are built.");

            if (array.CountYToBuildingTop && context.StoreysToTop == 0)
                warnings.Add("Count Y to building top works inside a building only — Count Y is used here.");

            if (array.Along == LiveBuilderArrayAlong.Wall && LiveBuilderArrayWallPath.Of(array) == null)
                warnings.Add("Along Wall needs a wall or building above this object — copies go along its axes.");

            var patternProblem = LiveBuilderArrayPattern.ProblemOf(array);

            if (patternProblem != null)
                warnings.Add(patternProblem);

            var turnedCount = PlacedCopiesOf(array, copiesAbove).FindAll(copy => copy.IsTurned).Count;

            if (turnedCount > 0)
                warnings.Add(turnedCount + " copies stand on a curve: only prefabs and other real objects are copied there, boxes and cuts are not.");

            return warnings;
        }
    }
}
