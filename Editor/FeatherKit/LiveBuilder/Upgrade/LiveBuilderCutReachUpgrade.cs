using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Вырез после правки порядка вырезов (<see cref="LiveBuilderCutOrderUpgrade"/>) дотягивается до того, что резал раньше:
    /// - «Everything it touches», если так он не достанет ничего сверх своего объекта и переложенного — проверка по габаритам всего вокруг
    /// - иначе копия выреза в каждом вложенном «Kept From Cuts» или строка в итог
    /// </summary>
    public static class LiveBuilderCutReachUpgrade
    {
        // F3: вырез резал переложенное раньше. «Everything it touches», если так он не достанет ничего сверх того, что резал; иначе — копия
        // выреза в каждом вложенном объекте: две копии надо держать вместе.
        public static void ReachKeptObjects(GameObject cutObject, HashSet<BoxModel> groups, IReadOnlyList<GameObject> roots, LiveBuilderUpgradeReport report)
        {
            if (groups.Count == 0)
                return;

            if (!cutObject.TryGetComponent<GeometryCutter>(out var cutter))
            {
                report.AddMayLookDifferent(cutObject, "no longer cuts what was moved into '" + LiveBuilderKeptFromCuts.GroupName + "'");
                return;
            }

            if (cutter.Scope == GeometryCutterScope.EverythingItTouches)
                return;

            var model = LiveBuilderGroups.GroupOf(cutter.transform.parent);

            if (!AddsTargets(cutter, model, groups, roots))
            {
                cutter.Scope = GeometryCutterScope.EverythingItTouches;
                report.AddRestructured(cutObject, "now 'Everything it touches' to keep cutting what was moved into '" + LiveBuilderKeptFromCuts.GroupName + "'");
                return;
            }

            foreach (var group in groups)
            {
                var copy = Object.Instantiate(cutObject, group.transform, true);
                copy.name = cutObject.name;
                report.AddRestructured(copy, "copy of " + LiveBuilderUpgradeReport.PathOf(cutObject) + " to keep cutting inside '" +
                                             LiveBuilderKeptFromCuts.GroupName + "': keep the two copies together");
            }
        }


        // F4: вырез в здании резал коробки объекта вне здания.
        public static void ReachModelAgain(GeometryCutter cutter, BoxModel model, IReadOnlyList<GameObject> roots, LiveBuilderUpgradeReport report)
        {
            if (AddsTargets(cutter, model, new HashSet<BoxModel>(), roots))
            {
                report.AddMayLookDifferent(cutter.gameObject, "in the building it cuts only the building now; before it cut the whole '" + model.name + "'");
                return;
            }

            cutter.Scope = GeometryCutterScope.EverythingItTouches;
            report.AddRestructured(cutter.gameObject, "now 'Everything it touches' to keep cutting '" + model.name + "' outside the building");
        }


        // Достанет ли вырез с «Everything it touches» что-то сверх своего объекта, его стен и зданий и вложенных объектов allowed: по габаритам всего вокруг.
        private static bool AddsTargets(GeometryCutter cutter, BoxModel model, HashSet<BoxModel> allowed, IReadOnlyList<GameObject> roots)
        {
            // Вровень с соседом — не задевает его.
            var bounds = LiveBuilderCutBoxUpgrade.WorldBoundsOf(cutter);
            bounds.Expand(-0.002f);

            foreach (var root in roots)
            {
                foreach (var other in root.GetComponentsInChildren<BoxModel>())
                    if (other != model && !allowed.Contains(other) && IsTouchedBy(other, bounds))
                        return true;

                foreach (var wall in root.GetComponentsInChildren<SplineWall>())
                    if (!IsInside(wall, model, allowed) && wall.TryGetComponent<MeshRenderer>(out var renderer) && renderer.bounds.Intersects(bounds))
                        return true;

                foreach (var part in root.GetComponentsInChildren<SplineBuildingPart>())
                    if (!IsInside(part, model, allowed) && part.TryGetComponent<MeshRenderer>(out var renderer) && renderer.bounds.Intersects(bounds))
                        return true;
            }

            return false;
        }

        private static bool IsInside(Component construction, BoxModel model, HashSet<BoxModel> allowed)
        {
            var owner = construction.transform.parent != null ? LiveBuilderGroups.GroupOf(construction.transform.parent) : null;
            return owner == model || owner != null && allowed.Contains(owner);
        }

        private static bool IsTouchedBy(BoxModel model, Bounds bounds)
        {
            foreach (var box in BoxModelShapeCollector.Collect(model, new List<BoxModelRampShape>()))
                if (box.Mode == BoxModelPartMode.Add && WorldBoundsOf(model, box.Box).Intersects(bounds))
                    return true;

            return false;
        }

        private static Bounds WorldBoundsOf(BoxModel model, MillimeterBox box)
        {
            var bounds = new Bounds(model.transform.TransformPoint(box.MinInMeters), Vector3.zero);
            bounds.Encapsulate(model.transform.TransformPoint(box.MaxInMeters));
            return bounds;
        }
    }
}
