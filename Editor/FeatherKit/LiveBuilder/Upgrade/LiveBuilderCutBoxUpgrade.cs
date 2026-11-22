using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Обновление вырезов старых данных: вырезающая коробка становится вырезом (<see cref="GeometryCutter"/>), «Где режет» — по новым словам.
    /// - коробка → вырез-коробка той же формы, места, «Режет» и материалов; материал, который нигде не был виден, снимается — стены сохраняют свой
    /// - коробка тоньше стены, сквозь которую раньше давала проём, углубляется до её толщины — иначе стала бы нишей
    /// - «Своя группа» → «Own object»; «Авто» без постройки над вырезом в сцене → «Everything it touches», как он и резал
    /// </summary>
    public static class LiveBuilderCutBoxUpgrade
    {
        // Насколько отрезок стены может отклониться от оси объекта, чтобы толщину мерить вдоль другой оси.
        private const float AxisAlignment = 0.999f;

        // Запас за сторону стены, м: сторона вровень с гранью выреза — тоже насквозь.
        private const float ThicknessMargin = 0.001f;


        // ── Вырезающие коробки ───────────────────────────────────────────

        // Вырезающие коробки под корнями — вырезы на тех же объектах сцены. Возвращает новые вырезы.
        public static List<GeometryCutter> ConvertCutBoxes(IReadOnlyList<GameObject> roots, LiveBuilderUpgradeReport report)
        {
            var cutters = new List<GeometryCutter>();

            foreach (var root in roots)
                foreach (var part in root.GetComponentsInChildren<BoxModelPart>(true))
                {
                    if (part.Mode != BoxModelPartMode.Cut)
                        continue;

                    var model = part.transform.parent != null ? part.transform.parent.GetComponentInParent<BoxModel>(true) : null;

                    if (model == null || PrefabUtility.IsPartOfPrefabInstance(part))
                    {
                        report.AddMayLookDifferent(part, model == null ? "cut box outside any object: left as it is" : "cut box inside a prefab instance: open the prefab to upgrade it");
                        continue;
                    }

                    var hasChamfers = part.Chamfers.Count > 0;
                    cutters.Add(ToCutter(part, model));
                    report.AddRestructured(cutters[cutters.Count - 1].gameObject, "cut box became a Cut" + (hasChamfers ? "; its edge bevels are dropped" : ""));
                }

            return cutters;
        }

        // Как «Make Cut», только без Undo: это обновление при загрузке.
        private static GeometryCutter ToCutter(BoxModelPart part, BoxModel model)
        {
            var gameObject = part.gameObject;
            var size = part.Size;
            var materials = BoxModelSideMaterials.Of(part);
            var cutTargets = part.CutTargets;

            gameObject.transform.rotation = model.transform.rotation;
            Object.DestroyImmediate(part);

            var cutter = gameObject.AddComponent<GeometryCutter>();
            BoxModelFactory.ShapeAsBox(cutter, size);
            cutter.CutTargets = cutTargets;
            cutter.Scope = GeometryCutterScope.OwnObject;
            GeometryCutterBoxConversion.CopyMaterials(materials, cutter);
            return cutter;
        }


        // ── Материал выреза ──────────────────────────────────────────────

        // Материал старой коробки красил только грани коробок своего объекта, а стены и перекрытия резались с их материалом.
        // Нет граней своего объекта — материал снимается: вырез режет стены точно, как раньше. Есть и то и другое — строка в итог.
        public static void KeepWallMaterialsOnHoles(IReadOnlyList<GeometryCutter> cutters, LiveBuilderUpgradeSnapshot snapshot, LiveBuilderUpgradeReport report)
        {
            foreach (var cutter in cutters)
            {
                if (cutter == null || !GeometryCutterSolverBoxes.HasOwnMaterials(cutter))
                    continue;

                var model = GeometryCutterSolverBoxes.CollectingModelOf(cutter);

                if (model == null || !CutsWallsOrSlabs(cutter, model))
                    continue;

                if (TouchesAddBoxes(cutter, model, snapshot))
                {
                    report.AddMayLookDifferent(cutter.gameObject, "hole sides in walls and slabs now take this cut's material, before they kept the wall's");
                    continue;
                }

                cutter.RevealMaterial = null;
                cutter.SideMaterials.Clear();
                report.AddRestructured(cutter.gameObject, "its material showed on no face and is dropped, so walls and slabs keep their own on hole sides");
            }
        }

        private static bool CutsWallsOrSlabs(GeometryCutter cutter, BoxModel model)
        {
            var bounds = WorldBoundsOf(cutter);

            foreach (var wall in model.GetComponentsInChildren<SplineWall>())
                if (LiveBuilderCutRules.CanCutterCut(cutter, wall) && wall.TryGetComponent<MeshRenderer>(out var renderer) && renderer.bounds.Intersects(bounds))
                    return true;

            foreach (var part in model.GetComponentsInChildren<SplineBuildingPart>())
                if (LiveBuilderCutRules.CanCutterCut(cutter, part) && part.TryGetComponent<MeshRenderer>(out var renderer) && renderer.bounds.Intersects(bounds))
                    return true;

            return false;
        }

        // Пересекает ли вырез добавляющие коробки своего объекта (по старому списку): тогда его грани были видны в объекте.
        private static bool TouchesAddBoxes(GeometryCutter cutter, BoxModel model, LiveBuilderUpgradeSnapshot snapshot)
        {
            foreach (var cutterBox in GeometryCutterSolverBoxes.BoxesOf(cutter, model, Vector3.zero))
                foreach (var box in snapshot.LegacyBoxesOf(model))
                    if (box.Mode == BoxModelPartMode.Add && box.Box.Overlaps(cutterBox))
                        return true;

            return false;
        }


        // ── Тонкая коробка в стене ───────────────────────────────────────

        // Старая коробка давала проём во всю толщину стены, если задевала её линию. Вырез тоньше стены режет нишу — углубить до толщины,
        // если так он не заденет новых коробок объекта. Задевает стену, но не её линию, — раньше не резал её вовсе: строка в итог.
        public static void FitThinCutsToWalls(IReadOnlyList<GeometryCutter> cutters, LiveBuilderUpgradeReport report)
        {
            foreach (var cutter in cutters)
            {
                var model = cutter != null ? GeometryCutterSolverBoxes.CollectingModelOf(cutter) : null;

                if (model == null || cutter.CutTargets == LiveBuilderCutTargets.SlabsOnly)
                    continue;

                foreach (var wallOwner in WallOwnersIn(model))
                    FitThinCutToWall(cutter, model, wallOwner, report);
            }
        }

        private static IEnumerable<Component> WallOwnersIn(BoxModel model)
        {
            foreach (var wall in model.GetComponentsInChildren<SplineWall>())
                yield return wall;

            foreach (var building in model.GetComponentsInChildren<SplineBuilding>())
                yield return building;
        }

        private static void FitThinCutToWall(GeometryCutter cutter, BoxModel model, Component wallOwner, LiveBuilderUpgradeReport report)
        {
            var target = wallOwner is SplineBuilding building ? WallsPartOf(building) : wallOwner;

            if (target == null || !LiveBuilderCutRules.CanCutterCut(cutter, target))
                return;

            var paths = SplineWallRebuilder.PathsOf(wallOwner);
            var box = GeometryCutterSolverBoxes.BoxesOf(cutter, model, Vector3.zero)[0];
            var wallToModel = model.transform.worldToLocalMatrix * wallOwner.transform.localToWorldMatrix;

            if (!TryGetThicknessAcross(wallOwner, paths, wallToModel, box, out var crossesLine, out var axis, out var thicknessMin, out var thicknessMax))
                return;

            if (!crossesLine)
            {
                report.AddMayLookDifferent(cutter.gameObject, "now cuts a niche into " + LiveBuilderUpgradeReport.PathOf(wallOwner) + ", before it did not cut it");
                return;
            }

            if (SplineWallRebuilder.IsThroughThickness(wallOwner, model, cutter, paths))
                return;

            var min = box.MinInMeters;
            var max = box.MaxInMeters;
            min[axis] = Mathf.Min(min[axis], thicknessMin - ThicknessMargin);
            max[axis] = Mathf.Max(max[axis], thicknessMax + ThicknessMargin);
            var deeper = MillimeterBox.FromMeters(min, max - min);

            if (axis < 0 || TouchesNewAddBoxes(model, box, deeper))
            {
                report.AddMayLookDifferent(cutter.gameObject, "cut box thinner than " + LiveBuilderUpgradeReport.PathOf(wallOwner) + ": now a niche, was a hole");
                return;
            }

            cutter.transform.position = model.transform.TransformPoint(deeper.MinInMeters);
            BoxModelFactory.ShapeAsBox(cutter, deeper.MaxInMeters - deeper.MinInMeters);
            report.AddRestructured(cutter.gameObject, "cut box thinner than " + LiveBuilderUpgradeReport.PathOf(wallOwner) + " deepened to the wall thickness: still a hole");
        }

        private static Component WallsPartOf(SplineBuilding building)
        {
            foreach (var part in building.GetComponentsInChildren<SplineBuildingPart>(true))
                if (part.Kind == SplineBuildingPartKind.Walls)
                    return part;

            return null;
        }

        // Где стена проходит через коробку: задевает ли её линию, вдоль какой оси объекта толщина и где обе стороны стены на ней.
        // Отрезок стены не вдоль оси объекта — ось −1.
        private static bool TryGetThicknessAcross(Component wallOwner, IReadOnlyList<SplineWallPath> paths, Matrix4x4 wallToModel, MillimeterBox box,
            out bool crossesLine, out int axis, out float thicknessMin, out float thicknessMax)
        {
            crossesLine = false;
            axis = -2;
            thicknessMin = float.MaxValue;
            thicknessMax = float.MinValue;
            var boxMin = box.MinInMeters;
            var boxMax = box.MaxInMeters;

            foreach (var path in paths)
            {
                LiveBuilderPathOwners.GetWallSides(wallOwner, path, out var leftOffset, out var rightOffset);

                for (var segment = 0; segment < path.SegmentCount; segment++)
                {
                    var side = Vector3.Cross(Vector3.up, path.DirectionOf(segment));
                    var start = path.Points[segment];
                    var end = path.Points[segment + 1];
                    var center = IsInPlan(wallToModel, start, end, boxMin, boxMax);
                    var left = IsInPlan(wallToModel, start + side * leftOffset, end + side * leftOffset, boxMin, boxMax);
                    var right = IsInPlan(wallToModel, start + side * rightOffset, end + side * rightOffset, boxMin, boxMax);

                    if (!center && !left && !right || !IsInHeight(wallOwner, wallToModel, boxMin, boxMax))
                        continue;

                    crossesLine |= center;
                    var direction = wallToModel.MultiplyVector(end - start).normalized;
                    var segmentAxis = Mathf.Abs(direction.x) >= AxisAlignment ? 2 : Mathf.Abs(direction.z) >= AxisAlignment ? 0 : -1;
                    axis = axis == -2 || axis == segmentAxis ? segmentAxis : -1;

                    foreach (var offset in new[] { leftOffset, rightOffset })
                    {
                        var sidePoint = wallToModel.MultiplyPoint3x4(start + side * offset);
                        thicknessMin = Mathf.Min(thicknessMin, axis >= 0 ? sidePoint[axis] : 0f);
                        thicknessMax = Mathf.Max(thicknessMax, axis >= 0 ? sidePoint[axis] : 0f);
                    }
                }
            }

            return axis != -2;
        }

        // Отрезок в плане (X и Z осей объекта) проходит через прямоугольник коробки.
        private static bool IsInPlan(Matrix4x4 wallToModel, Vector3 start, Vector3 end, Vector3 boxMin, Vector3 boxMax)
        {
            return SplineWallRebuilder.TryClipToRectangle(wallToModel.MultiplyPoint3x4(start), wallToModel.MultiplyPoint3x4(end), boxMin, boxMax, out _, out _);
        }

        // Коробка по высоте задевает меш стены или здания.
        private static bool IsInHeight(Component wallOwner, Matrix4x4 wallToModel, Vector3 boxMin, Vector3 boxMax)
        {
            var renderers = wallOwner.GetComponentsInChildren<MeshRenderer>();

            foreach (var renderer in renderers)
            {
                var bounds = renderer.bounds;
                var modelToWorld = wallToModel * wallOwner.transform.worldToLocalMatrix;
                var bottom = modelToWorld.MultiplyPoint3x4(bounds.min).y;
                var top = modelToWorld.MultiplyPoint3x4(bounds.max).y;

                if (Mathf.Min(bottom, top) < boxMax.y && Mathf.Max(bottom, top) > boxMin.y)
                    return true;
            }

            return false;
        }

        // Углублённая коробка задевает добавляющие коробки объекта, которых не задевала прежняя: тогда углублять нельзя.
        private static bool TouchesNewAddBoxes(BoxModel model, MillimeterBox before, MillimeterBox deeper)
        {
            foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
                if (box.Mode == BoxModelPartMode.Add && box.Box.Overlaps(deeper) && !box.Box.Overlaps(before))
                    return true;

            return false;
        }


        // ── Где режет ────────────────────────────────────────────────────

        // «Своя группа» → «Own object». «Авто» без постройки над вырезом в сцене резал всё вокруг — теперь так и называется.
        // В префабе-ассете место решает расстановка экземпляров: там такой вырез не трогаем.
        public static void UpgradeScopes(IReadOnlyList<GameObject> roots, bool isPrefabAsset, LiveBuilderUpgradeReport report)
        {
            foreach (var root in roots)
                foreach (var cutter in root.GetComponentsInChildren<GeometryCutter>(true))
                {
                    if (cutter.Scope == GeometryCutterScope.LegacyOwnGroup)
                    {
                        SetScope(cutter, GeometryCutterScope.OwnObject);
                        report.AddRestructured(cutter.gameObject, "'Own group' became 'Own object'");
                    }

                    if (!isPrefabAsset && IsOwnObjectWithoutConstruction(cutter))
                    {
                        SetScope(cutter, GeometryCutterScope.EverythingItTouches);
                        report.AddRestructured(cutter.gameObject, "no object above this cut: it cut everything it touched, now says so ('Everything it touches')");
                    }
                }
        }


        // «Own object» без постройки над вырезом — так выглядит старое «Авто» в корне.
        public static bool IsOwnObjectWithoutConstruction(GeometryCutter cutter)
        {
            return cutter.Scope == GeometryCutterScope.OwnObject && LiveBuilderCutRules.OwnConstructionOf(cutter.transform) == null;
        }

        private static void SetScope(GeometryCutter cutter, GeometryCutterScope scope)
        {
            cutter.Scope = scope;
            EditorUtility.SetDirty(cutter);
            LiveBuilderSyncEdits.RecordPrefabInstance(cutter);
        }


        // ── Помощники ────────────────────────────────────────────────────

        public static Bounds WorldBoundsOf(GeometryCutter cutter)
        {
            var center = cutter.transform.TransformPoint(cutter.ShapeOffset + Vector3.up * cutter.Height / 2f);
            var bounds = new Bounds(center, Vector3.zero);
            var half = new Vector3(cutter.Width, cutter.Height, cutter.Depth) / 2f;

            for (var corner = 0; corner < 8; corner++)
            {
                var local = new Vector3((corner & 1) == 0 ? -half.x : half.x, (corner & 2) == 0 ? -half.y : half.y, (corner & 4) == 0 ? -half.z : half.z);
                bounds.Encapsulate(cutter.transform.TransformPoint(cutter.ShapeOffset + Vector3.up * cutter.Height / 2f + local));
            }

            return bounds;
        }
    }
}
