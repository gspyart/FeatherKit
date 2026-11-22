using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Правка порядка вырезов: что по старому правилу оставалось целым, а по новому («сначала все добавления») резалось бы, — во вложенный «Kept From Cuts».
    /// - F1: коробка после пересекающего её выреза — целиком; у комнаты и лестницы — их часть внутри выреза (<see cref="LiveBuilderKeptRegion"/>)
    /// - F2: стена в пустоте комнаты или во всю высоту вырезающей коробки
    /// - F3: вырезы, которые резали переложенное, дотягиваются до него — «Everything it touches», если так не прибавится целей, иначе копия выреза рядом
    /// - F4: вырез в интерьере здания, резавший объект вне здания, — «Everything it touches», если можно; иначе строка в итог
    /// Решения — по снимку «как было» до любых преобразований, правки — после них.
    /// </summary>
    public sealed class LiveBuilderCutOrderUpgrade
    {
        // Высота стены совпадает с коробкой с таким запасом, м: так же считало старое правило.
        private const float FullHeightTolerance = 0.01f;

        // Что переложить во вложенный объект и к какому объекту оно относится.
        private readonly Dictionary<GameObject, BoxModel> keptObjects = new();

        // Части коробок, которые встанут во вложенный объект простыми коробками.
        private readonly List<LiveBuilderKeptRegion> keptRegions = new();

        // Вырез → переложенное (объект сцены или часть коробки), которое он резал раньше и должен резать дальше.
        private readonly Dictionary<GameObject, HashSet<object>> keptTargetsByCut = new();

        // Вырез в здании, который раньше резал объект вне здания, → этот объект.
        private readonly Dictionary<GameObject, BoxModel> modelsLostByCut = new();


        // ── Решения ──────────────────────────────────────────────────────

        // По старым коробкам каждого объекта: кого переложить, кому из вырезов дотянуться. Строки о том, что поправить нельзя, — сразу в итог.
        public static LiveBuilderCutOrderUpgrade Plan(LiveBuilderUpgradeSnapshot snapshot, LiveBuilderUpgradeReport report)
        {
            var plan = new LiveBuilderCutOrderUpgrade();

            foreach (var model in snapshot.LegacyModels)
            {
                var boxes = snapshot.LegacyBoxesOf(model);
                var owners = snapshot.LegacyOwnersOf(model);
                plan.PlanAddsAfterCuts(model, boxes, owners, report);
                plan.PlanWallsInsideCuts(model, boxes, owners);
                plan.PlanCutsLeavingModel(model, boxes, owners);
            }

            return plan;
        }


        // F1: добавляющая коробка после пересекающего её выреза — по новому правилу он бы её резал: переезжает во вложенный объект целиком.
        // Комната и лестница переехать не могут (их пустота резала бы только вложенный) — туда встаёт их часть внутри выреза простой коробкой.
        private void PlanAddsAfterCuts(BoxModel model, IReadOnlyList<BoxModelSolverBox> boxes, IReadOnlyList<GameObject> owners, LiveBuilderUpgradeReport report)
        {
            foreach (var pair in ConflictsByOwner(boxes, owners))
            {
                var owner = pair.Key;

                if (owner.TryGetComponent<BoxModelPart>(out _))
                {
                    keptObjects[owner] = model;

                    if (IsFlushWithOtherMaterial(owner, boxes, owners))
                        report.AddMayLookDifferent(owner, "kept from cuts, but flush with a face of another material in the object around it: may flicker there");

                    continue;
                }

                if (HasRepeatAbove(owner.transform, model))
                {
                    report.AddMayLookDifferent(owner, "listed after a cut that now cuts it too; inside a repeated group it cannot be kept from cuts");
                    continue;
                }

                foreach (var (add, cut) in pair.Value)
                    keptRegions.Add(new LiveBuilderKeptRegion(model, owner.transform.parent, owner.name, boxes[add].Materials,
                        Intersection(boxes[add].Box, boxes[cut].Box), add));

                report.AddMayLookDifferent(owner, "listed after a cut that now cuts it too: the part inside the cut is kept as a plain box; face materials around it may differ");
            }

            // F3 для коробок: вырезы ниже в списке резали переложенное — пусть режут дальше.
            for (var cut = 0; cut < boxes.Count; cut++)
            {
                if (boxes[cut].Mode != BoxModelPartMode.Cut || owners[cut] == null)
                    continue;

                for (var add = 0; add < cut; add++)
                    if (owners[add] != null && keptObjects.TryGetValue(owners[add], out var keptModel) && keptModel == model && boxes[cut].Box.Overlaps(boxes[add].Box))
                        AddKeptTarget(owners[cut], owners[add]);

                foreach (var region in keptRegions)
                    if (region.Model == model && region.AddIndex < cut && boxes[cut].Box.Overlaps(region.Box))
                        AddKeptTarget(owners[cut], region);
            }

            AddProemsCuttingKept(model, boxes, owners);
        }

        // Пары «коробка, вырез раньше неё», где новый порядок режет то, что старый оставлял, — по объектам сцены коробок.
        internal static Dictionary<GameObject, List<(int Add, int Cut)>> ConflictsByOwner(IReadOnlyList<BoxModelSolverBox> boxes, IReadOnlyList<GameObject> owners)
        {
            var conflicts = new Dictionary<GameObject, List<(int Add, int Cut)>>();

            for (var add = 0; add < boxes.Count; add++)
            {
                if (boxes[add].Mode != BoxModelPartMode.Add || owners[add] == null)
                    continue;

                for (var cut = 0; cut < add; cut++)
                {
                    // Копии одного куска режут друг друга — это правит разворот повтора (LiveBuilderCopyConflictUpgrade).
                    if (owners[cut] == owners[add] || !IsNewCutOf(boxes[cut], boxes[add]))
                        continue;

                    if (!conflicts.TryGetValue(owners[add], out var list))
                        conflicts[owners[add]] = list = new List<(int Add, int Cut)>();

                    list.Add((add, cut));
                }
            }

            return conflicts;
        }

        // Грань коробки вровень с гранью другой коробки объекта другого материала, которая заходит в неё: после переезда там два меша
        // в одной плоскости, и видно то один, то другой. Тот же материал в тех же осях выглядит одинаково.
        private static bool IsFlushWithOtherMaterial(GameObject owner, IReadOnlyList<BoxModelSolverBox> boxes, IReadOnlyList<GameObject> owners)
        {
            for (var kept = 0; kept < boxes.Count; kept++)
            {
                if (owners[kept] != owner)
                    continue;

                for (var other = 0; other < boxes.Count; other++)
                    if (owners[other] != owner && boxes[other].Mode == BoxModelPartMode.Add && boxes[other].Box.Overlaps(boxes[kept].Box)
                        && boxes[other].Materials.Key != boxes[kept].Materials.Key && HasSharedFacePlane(boxes[kept].Box, boxes[other].Box))
                        return true;
            }

            return false;
        }

        private static bool HasSharedFacePlane(MillimeterBox first, MillimeterBox second)
        {
            for (var axis = 0; axis < 3; axis++)
                if (first.Min[axis] == second.Min[axis] || first.Max[axis] == second.Max[axis])
                    return true;

            return false;
        }

        // Повтор над коробкой в её объекте — старый или новый: часть внутри выреза у каждой копии своя.
        private static bool HasRepeatAbove(Transform node, BoxModel model)
        {
            for (var current = node.parent; current != null; current = current.parent)
            {
                if (current.GetComponent<LiveBuilderArray>() != null || current.GetComponent<BoxModelRepeat>() != null
                    || current.GetComponent<SplineBuildingStoreyLink>() != null || current.GetComponent<FeatherKit.LevelDesign.ArrayModifier>() != null)
                    return true;

                if (current == model.transform)
                    break;
            }

            return false;
        }

        private static MillimeterBox Intersection(MillimeterBox first, MillimeterBox second)
        {
            return new MillimeterBox(Vector3Int.Max(first.Min, second.Min), Vector3Int.Min(first.Max, second.Max));
        }

        // Вырез стоит раньше коробки, пересекает её, не свой кусок и по новому правилу её достаёт: «Own object» — постройка над вырезом есть над коробкой.
        internal static bool IsNewCutOf(BoxModelSolverBox cut, BoxModelSolverBox add)
        {
            if (cut.Mode != BoxModelPartMode.Cut || cut.PieceKey == add.PieceKey || !cut.Box.Overlaps(add.Box))
                return false;

            var cutConstruction = cut.ConstructionsAbove.Count > 0 ? cut.ConstructionsAbove[cut.ConstructionsAbove.Count - 1] : null;

            foreach (var construction in add.ConstructionsAbove)
                if (construction == cutConstruction)
                    return true;

            return false;
        }

        // Проёмы объекта старого правила резали его меш вычитанием — и переложенную коробку тоже. «Own object» её больше не достанет.
        private void AddProemsCuttingKept(BoxModel model, IReadOnlyList<BoxModelSolverBox> boxes, IReadOnlyList<GameObject> owners)
        {
            foreach (var cutter in model.GetComponentsInChildren<GeometryCutter>(true))
            {
                if (cutter.Scope == GeometryCutterScope.EverythingItTouches || !LiveBuilderCutRules.CanCutterCut(cutter, model))
                    continue;

                var cutterBounds = LiveBuilderCutBoxUpgrade.WorldBoundsOf(cutter);

                for (var index = 0; index < boxes.Count; index++)
                    if (owners[index] != null && keptObjects.TryGetValue(owners[index], out var keptModel) && keptModel == model
                        && cutterBounds.Intersects(WorldBoundsOf(model, boxes[index].Box)))
                        AddKeptTarget(cutter.gameObject, owners[index]);

                foreach (var region in keptRegions)
                    if (region.Model == model && cutterBounds.Intersects(WorldBoundsOf(model, region.Box)))
                        AddKeptTarget(cutter.gameObject, region);
            }
        }


        // F2: стену внутри пустоты комнаты или коробки во всю её высоту старое правило не резало, новое режет. Вырезы, что резали её, — дотянуть.
        private void PlanWallsInsideCuts(BoxModel model, IReadOnlyList<BoxModelSolverBox> boxes, IReadOnlyList<GameObject> owners)
        {
            foreach (var wall in model.GetComponentsInChildren<SplineWall>(true))
            {
                if (wall.transform.parent == null || LiveBuilderGroups.GroupOf(wall.transform.parent) != model || !wall.gameObject.activeInHierarchy)
                    continue;

                var oldCuts = new List<int>();
                var isCutOnlyByNewRule = false;

                for (var index = 0; index < boxes.Count; index++)
                {
                    var box = boxes[index];

                    if (box.Mode != BoxModelPartMode.Cut || !box.CutSource.CutsWalls || !CrossesWall(model, wall, box.Box, out var isFullHeight))
                        continue;

                    if (box.Owner is BoxModelRoom || isFullHeight)
                        isCutOnlyByNewRule |= LiveBuilderCutRules.Reaches(box.CutSource, wall);
                    else
                        oldCuts.Add(index);
                }

                if (!isCutOnlyByNewRule)
                    continue;

                keptObjects[wall.gameObject] = model;

                foreach (var index in oldCuts)
                    if (owners[index] != null)
                        AddKeptTarget(owners[index], wall.gameObject);

                foreach (var cutter in model.GetComponentsInChildren<GeometryCutter>(true))
                    if (!cutter.transform.IsChildOf(wall.transform) && cutter.Scope != GeometryCutterScope.EverythingItTouches && LiveBuilderCutRules.CanCutterCut(cutter, wall)
                        && wall.TryGetComponent<MeshRenderer>(out var renderer) && renderer.bounds.Intersects(LiveBuilderCutBoxUpgrade.WorldBoundsOf(cutter)))
                        AddKeptTarget(cutter.gameObject, wall.gameObject);
            }
        }

        // Линия стены проходит через коробку в плане и по высоте; isFullHeight — коробка во всю высоту стены (хоть одной её копии вверх).
        private static bool CrossesWall(BoxModel model, SplineWall wall, MillimeterBox box, out bool isFullHeight)
        {
            isFullHeight = false;
            var wallToModel = model.transform.worldToLocalMatrix * wall.transform.localToWorldMatrix;
            var boxMin = box.MinInMeters;
            var boxMax = box.MaxInMeters;
            var crosses = false;

            foreach (var path in SplineWallPath.FromWall(wall))
                for (var segment = 0; segment < path.SegmentCount; segment++)
                {
                    var start = wallToModel.MultiplyPoint3x4(path.Points[segment]);
                    var end = wallToModel.MultiplyPoint3x4(path.Points[segment + 1]);

                    if (!SplineWallRebuilder.TryClipToRectangle(start, end, boxMin, boxMax, out var from, out var to))
                        continue;

                    var baseHeight = Vector3.Lerp(start, end, (from + to) / 2f).y;

                    for (var copy = 0; copy < wall.LegacyCopiesUp; copy++)
                    {
                        var bottom = baseHeight + copy * wall.LegacyCopyStep;
                        var top = bottom + wall.Height;

                        if (boxMax.y <= bottom || boxMin.y >= top)
                            continue;

                        crosses = true;
                        isFullHeight |= boxMin.y <= bottom + FullHeightTolerance && boxMax.y >= top - FullHeightTolerance;
                    }
                }

            return crosses;
        }


        // F4: вырезающая коробка в здании резала коробки объекта вне здания, а «Own object» теперь — здание.
        private void PlanCutsLeavingModel(BoxModel model, IReadOnlyList<BoxModelSolverBox> boxes, IReadOnlyList<GameObject> owners)
        {
            for (var cut = 0; cut < boxes.Count; cut++)
            {
                var cutBox = boxes[cut];

                if (cutBox.Mode != BoxModelPartMode.Cut || cutBox.OwnerPart == null || owners[cut] == null || cutBox.ConstructionsAbove.Count < 2)
                    continue;

                var building = cutBox.ConstructionsAbove[cutBox.ConstructionsAbove.Count - 1];

                for (var add = 0; add < cut; add++)
                    if (boxes[add].Mode == BoxModelPartMode.Add && boxes[add].Box.Overlaps(cutBox.Box) && !Contains(boxes[add].ConstructionsAbove, building))
                    {
                        modelsLostByCut[owners[cut]] = model;
                        break;
                    }
            }
        }

        private static bool Contains(IReadOnlyList<Transform> constructions, Transform construction)
        {
            foreach (var candidate in constructions)
                if (candidate == construction)
                    return true;

            return false;
        }


        private void AddKeptTarget(GameObject cut, object kept)
        {
            if (ReferenceEquals(cut, kept))
                return;

            if (!keptTargetsByCut.TryGetValue(cut, out var targets))
                keptTargetsByCut[cut] = targets = new HashSet<object>();

            targets.Add(kept);
        }


        // ── Правки ───────────────────────────────────────────────────────

        // После всех преобразований: переложить, дотянуть вырезы. Без Undo — это обновление при загрузке.
        public void Apply(IReadOnlyList<GameObject> roots, LiveBuilderUpgradeReport report)
        {
            var keptGroups = new Dictionary<GameObject, BoxModel>();

            foreach (var pair in keptObjects)
                if (pair.Key != null && pair.Value != null)
                    MoveIntoKeptGroup(pair.Key, pair.Value, keptGroups, report);

            foreach (var region in keptRegions)
                if (region.Parent != null && region.Model != null)
                {
                    var part = region.CreateIn(KeptGroupUnder(region.Parent, region.Model));
                    report.AddRestructured(part.gameObject, "part of '" + region.SourceName +
                                                            "' inside a cut listed before it, kept as a plain box so the cut leaves it as before");
                }

            foreach (var pair in keptTargetsByCut)
                if (pair.Key != null)
                    LiveBuilderCutReachUpgrade.ReachKeptObjects(pair.Key, GroupsOf(pair.Value, keptGroups), roots, report);

            foreach (var pair in modelsLostByCut)
                if (pair.Key != null && pair.Key.TryGetComponent<GeometryCutter>(out var cutter) && cutter.Scope != GeometryCutterScope.EverythingItTouches)
                    LiveBuilderCutReachUpgrade.ReachModelAgain(cutter, pair.Value, roots, report);
        }


        // F1, F2: к тому же родителю во вложенный объект; мир тот же. Оси вложенного — оси внешнего: тело сверяется клетка в клетку.
        private static void MoveIntoKeptGroup(GameObject kept, BoxModel model, Dictionary<GameObject, BoxModel> keptGroups, LiveBuilderUpgradeReport report)
        {
            var parent = kept.transform.parent;

            if (parent == null || PrefabUtility.IsPartOfPrefabInstance(kept))
            {
                report.AddMayLookDifferent(kept, "now cut by cuts listed before it; could not be moved into '" + LiveBuilderKeptFromCuts.GroupName + "' here");
                return;
            }

            var group = KeptGroupUnder(parent, model);
            kept.transform.SetParent(group.transform, true);
            keptGroups[kept] = group;

            var why = kept.GetComponent<SplineWall>() != null ? "to stay uncut inside the empty space around it" : "to stay uncut by cuts listed before it";
            report.AddRestructured(kept, "moved into '" + LiveBuilderKeptFromCuts.GroupName + "' " + why);
        }

        private static BoxModel KeptGroupUnder(Transform parent, BoxModel model)
        {
            var existing = parent.Find(LiveBuilderKeptFromCuts.GroupName);

            if (existing != null && existing.TryGetComponent<BoxModel>(out var existingGroup))
                return existingGroup;

            var groupObject = new GameObject(LiveBuilderKeptFromCuts.GroupName);
            groupObject.transform.SetParent(parent, false);
            groupObject.transform.SetPositionAndRotation(model.transform.position, model.transform.rotation);
            groupObject.layer = model.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(groupObject, GameObjectUtility.GetStaticEditorFlags(model.gameObject));

            var group = groupObject.AddComponent<BoxModel>();
            group.DataVersion = BoxModel.CurrentDataVersion;
            group.DefaultMaterial = model.DefaultMaterial;
            group.BevelSize = model.BevelSize;
            group.BevelSegments = model.BevelSegments;
            group.TextureAlignment = model.TextureAlignment;

            var serializedGroup = new SerializedObject(group);
            serializedGroup.FindProperty("hasCollider").boolValue = model.HasCollider;
            serializedGroup.ApplyModifiedPropertiesWithoutUndo();
            return group;
        }


        // Вложенные объекты, в которые встало переложенное: объекты сцены и части коробок.
        private static HashSet<BoxModel> GroupsOf(HashSet<object> keptTargets, Dictionary<GameObject, BoxModel> keptGroups)
        {
            var groups = new HashSet<BoxModel>();

            foreach (var kept in keptTargets)
                if (kept is GameObject keptObject && keptObject != null && keptGroups.TryGetValue(keptObject, out var group))
                    groups.Add(group);
                else if (kept is LiveBuilderKeptRegion region && region.Group != null)
                    groups.Add(region.Group);

            return groups;
        }


        private static Bounds WorldBoundsOf(BoxModel model, MillimeterBox box)
        {
            var bounds = new Bounds(model.transform.TransformPoint(box.MinInMeters), Vector3.zero);
            bounds.Encapsulate(model.transform.TransformPoint(box.MaxInMeters));
            return bounds;
        }
    }
}
