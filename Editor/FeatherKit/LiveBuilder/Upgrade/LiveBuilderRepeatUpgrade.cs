using System.Collections.Generic;
using FeatherKit.LevelDesign;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Старые повторы — в один повтор <see cref="LiveBuilderArray"/>: группа повтора, копии вверх у стены и выреза, Array FeatherKit на объектах Live Builder.
    /// - шаг по одной оси → число и шаг этой оси; шаг сразу по двум осям или пропуски в сетке — разворачиваются в обычные объекты
    /// - Array по кругу остаётся (он и дальше работает); Array на остальных объектах не трогается
    /// - ось Y у объекта уже занята повтором по этажам — второй повтор встаёт на обёртку-родителя
    /// </summary>
    public static class LiveBuilderRepeatUpgrade
    {
        // Шаг по оси меньше — оси нет, м.
        private const float StepTolerance = 1e-4f;


        // ── Группа повтора ───────────────────────────────────────────────

        public static void UpgradeRepeatGroups(IReadOnlyList<GameObject> roots, LiveBuilderUpgradeReport report)
        {
            foreach (var repeat in AllUnder<BoxModelRepeat>(roots))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(repeat))
                {
                    report.AddMayLookDifferent(repeat.gameObject, "repeat group inside a prefab instance: open the prefab to upgrade it");
                    continue;
                }

                var model = repeat.transform.parent != null ? repeat.transform.parent.GetComponentInParent<BoxModel>(true) : null;
                var modelRotation = model != null ? model.transform.rotation : Quaternion.identity;
                var localStep = Quaternion.Inverse(repeat.transform.rotation) * (modelRotation * repeat.Step);

                if (repeat.Count <= 1)
                    report.AddRestructured(repeat.gameObject, "repeat group of one copy removed");
                else if (TryGetSingleAxis(localStep, out var axis))
                    SetAxis(LiveBuilderStoreyLinkUpgrade.ArrayOn(repeat.gameObject), axis, repeat.Count, localStep[axis], report, repeat.gameObject, "repeat group");
                else
                    Unroll(repeat.transform, repeat.Count, repeat.transform.InverseTransformVector(modelRotation * repeat.Step), report);

                Object.DestroyImmediate(repeat);
            }
        }

        // Шаг по двум осям сразу: копии — обычными объектами в той же группе, после оригиналов, по порядку копий, как их считал старый повтор.
        private static void Unroll(Transform group, int count, Vector3 localStep, LiveBuilderUpgradeReport report)
        {
            var originals = new List<Transform>();

            foreach (Transform child in group)
                originals.Add(child);

            for (var copy = 1; copy < count; copy++)
                foreach (var original in originals)
                {
                    var clone = Object.Instantiate(original.gameObject, group, false);
                    clone.name = original.name;
                    clone.transform.localPosition = original.localPosition + localStep * copy;
                }

            report.AddRestructured(group.gameObject, "repeat along two axes at once unrolled into " + (count - 1) + " plain copies");
        }


        // ── Копии вверх ──────────────────────────────────────────────────

        // Копии вверх у стены и выреза вне здания — повтор по Y со своим шагом, не высотой этажа.
        public static void UpgradeCopiesUp(IReadOnlyList<GameObject> roots, LiveBuilderUpgradeReport report)
        {
            foreach (var wall in AllUnder<SplineWall>(roots))
                if (wall.LegacyCopiesUp > 1 && !PrefabUtility.IsPartOfPrefabInstance(wall))
                {
                    AddCopiesUp(wall.gameObject, wall.LegacyCopiesUp, wall.LegacyCopyStep, report);
                    wall.LegacyCopiesUp = 1;
                }

            foreach (var cutter in AllUnder<GeometryCutter>(roots))
                if (cutter.LegacyCopiesUp > 1 && !PrefabUtility.IsPartOfPrefabInstance(cutter))
                {
                    AddCopiesUp(cutter.gameObject, cutter.LegacyCopiesUp, cutter.LegacyCopyStep, report);
                    cutter.LegacyCopiesUp = 1;
                }
        }

        private static void AddCopiesUp(GameObject target, int count, float step, LiveBuilderUpgradeReport report)
        {
            var host = HasAxisTaken(target, 1) ? WrapperOf(target, report) : target;
            SetAxis(LiveBuilderStoreyLinkUpgrade.ArrayOn(host), 1, count, step, report, target, "copies up");
        }


        // ── Array FeatherKit ─────────────────────────────────────────────

        // Array на проёме, коробке, комнате, лестнице, стене, объекте из коробок. Скрытые копии убираются.
        public static void UpgradeArrayModifiers(IReadOnlyList<GameObject> roots, LiveBuilderUpgradeReport report)
        {
            foreach (var array in AllUnder<ArrayModifier>(roots))
            {
                if (array == null || array.GetComponent<ArrayPreviewCopy>() != null || !IsLiveBuilderObject(array.gameObject))
                    continue;

                if (PrefabUtility.IsPartOfPrefabInstance(array))
                {
                    report.AddMayLookDifferent(array.gameObject, "Array inside a prefab instance: open the prefab to upgrade it");
                    continue;
                }

                if (array.Layout == ArrayModifierLayout.Circle)
                {
                    report.AddMayLookDifferent(array.gameObject, "circular Array kept as it is: the Live Builder repeat goes along axes only");
                    continue;
                }

                UpgradeGrid(array, report);
            }
        }

        private static void UpgradeGrid(ArrayModifier array, LiveBuilderUpgradeReport report)
        {
            var target = array.gameObject;
            var counts = array.Counts;
            var step = ArrayModifierPlacement.GridStepOf(array, ArrayModifierPlacement.CopySizeOf(target));
            var axes = new List<int>();

            for (var axis = 0; axis < 3; axis++)
                if (counts[axis] > 1)
                    axes.Add(axis);

            var skipped = new List<int>(array.SkippedCopies);
            skipped.Remove(1);

            if (skipped.Count > 0 && axes.Count > 1)
            {
                ArrayModifierBuilder.Apply(array);
                report.AddRestructured(target, "Array grid with skipped copies unrolled into plain copies");
                return;
            }

            ArrayModifierBuilder.Remove(array);

            foreach (var axis in axes)
            {
                var host = HasAxisTaken(target, axis) ? WrapperOf(target, report) : target;
                var repeat = SetAxis(LiveBuilderStoreyLinkUpgrade.ArrayOn(host), axis, counts[axis], step[axis], report, target, "Array");

                if (axes.Count == 1 && skipped.Count > 0)
                    SetSkip(repeat, axis, string.Join(", ", skipped));
            }
        }

        // Проём, коробка, комната, лестница, стена или объект из коробок — Array на нём переводится в повтор Live Builder.
        internal static bool IsLiveBuilderObject(GameObject target)
        {
            return target.GetComponent<GeometryCutter>() != null || BoxModelShapeCollector.IsPiece(target) || target.GetComponent<SplineWall>() != null
                   || target.GetComponent<BoxModel>() != null;
        }


        // ── Повтор ───────────────────────────────────────────────────────

        private static LiveBuilderArray SetAxis(LiveBuilderArray array, int axis, int count, float step, LiveBuilderUpgradeReport report, GameObject source, string what)
        {
            switch (axis)
            {
                case 0:
                    array.CountX = count;
                    array.StepX = step;
                    break;
                case 1:
                    array.CountY = count;
                    array.StepY = step;
                    array.StepYIsStoreyHeight = false;
                    array.CountYToBuildingTop = false;
                    break;
                default:
                    array.CountZ = count;
                    array.StepZ = step;
                    break;
            }

            report.AddRestructured(source, what + " became a Live Builder repeat: " + "XYZ"[axis] + " × " + count + ", step " + step.ToString("0.###"));
            return array;
        }

        private static void SetSkip(LiveBuilderArray array, int axis, string skip)
        {
            if (axis == 0)
                array.SkipX = skip;
            else if (axis == 1)
                array.SkipY = skip;
            else
                array.SkipZ = skip;
        }

        // Ось занята повтором на самом объекте: по этажам (Y) или уже своим числом.
        private static bool HasAxisTaken(GameObject target, int axis)
        {
            if (!target.TryGetComponent<LiveBuilderArray>(out var array))
                return false;

            return array.CountOf(axis) > 1 || axis == 1 && array.CountYToBuildingTop;
        }

        // Обёртка-родитель на месте объекта: второй повтор по той же оси — на ней. Копии перемножаются так же.
        internal static GameObject WrapperOf(GameObject target, LiveBuilderUpgradeReport report)
        {
            var transform = target.transform;
            var wrapper = new GameObject(target.name + " Repeat");
            wrapper.transform.SetParent(transform.parent, false);
            wrapper.transform.SetSiblingIndex(transform.GetSiblingIndex());
            wrapper.transform.SetPositionAndRotation(transform.position, transform.rotation);
            transform.SetParent(wrapper.transform, true);
            report.AddRestructured(wrapper, "wrapper for a second repeat along the same axis");
            return wrapper;
        }

        private static bool TryGetSingleAxis(Vector3 step, out int axis)
        {
            axis = -1;

            for (var index = 0; index < 3; index++)
                if (Mathf.Abs(step[index]) > StepTolerance)
                {
                    if (axis >= 0)
                        return false;

                    axis = index;
                }

            return axis >= 0;
        }

        private static List<T> AllUnder<T>(IReadOnlyList<GameObject> roots) where T : Component
        {
            var components = new List<T>();

            foreach (var root in roots)
                components.AddRange(root.GetComponentsInChildren<T>(true));

            return components;
        }
    }
}
