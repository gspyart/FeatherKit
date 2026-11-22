using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Привязки к этажам здания (<see cref="SplineBuildingStoreyLink"/>) — в иерархию: объект ложится в свой этаж «Storey N» или в «Roof»,
    /// копии по этажам — повтор (<see cref="LiveBuilderArray"/>) по Y. Здания к этому времени уже с этажами-контейнерами.
    /// - «до верха» → Y до верха здания, без верхнего — Skip Y «last»; «по этаж K» → своё число; привязка в привязанном — копии даёт повтор выше
    /// - стена, вырез или вложенный объект, которому повтор выше дал бы другое число копий, выезжает в свой этаж — мир тот же
    /// - пропавший этаж: объект включается, выключен его этаж
    /// </summary>
    public static class LiveBuilderStoreyLinkUpgrade
    {
        // Сколько копий давала привязка и где стоял объект — по старым числам, до переноса.
        private readonly struct OldLinkCopies
        {
            public OldLinkCopies(int count, int storey)
            {
                Count = count;
                Storey = storey;
            }


            public int Count { get; }
            public int Storey { get; }
        }


        // ── Привязки ─────────────────────────────────────────────────────

        // Все привязки под корнями — от верхних в иерархии к нижним: группа уезжает в этаж раньше того, что в ней.
        public static void Upgrade(IReadOnlyList<GameObject> roots, LiveBuilderUpgradeReport report)
        {
            var links = new List<SplineBuildingStoreyLink>();

            foreach (var root in roots)
                links.AddRange(root.GetComponentsInChildren<SplineBuildingStoreyLink>(true));

            links.Sort((first, second) => DepthOf(first.transform).CompareTo(DepthOf(second.transform)));
            var oldCopies = new Dictionary<Transform, OldLinkCopies>();

            foreach (var link in links)
                if (link.IsValid)
                    oldCopies[link.transform] = new OldLinkCopies(link.IsOnRoof ? 1 : link.CopiesFrom(link.Storey), link.Storey);

            foreach (var link in links)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(link))
                {
                    report.AddMayLookDifferent(link.gameObject, "storey link inside a prefab instance: open the prefab to upgrade it");
                    continue;
                }

                if (link.IsValid && SplineBuildingStoreys.HasStoreyContainers(link.Building))
                    UpgradeLink(link, oldCopies, report);
                else
                    report.AddRestructured(link.gameObject, "storey link without a building removed");

                Object.DestroyImmediate(link);
            }

            foreach (var linked in oldCopies.Keys)
                if (linked != null)
                    MoveOutWhatRepeatsDifferently(linked, oldCopies, report);
        }

        private static int DepthOf(Transform node)
        {
            var depth = 0;

            for (var current = node.parent; current != null; current = current.parent)
                depth++;

            return depth;
        }


        private static void UpgradeLink(SplineBuildingStoreyLink link, Dictionary<Transform, OldLinkCopies> oldCopies, LiveBuilderUpgradeReport report)
        {
            var node = link.transform;
            var building = link.Building;
            var container = link.IsOnRoof ? SplineBuildingStoreys.RoofOf(building) : SplineBuildingStoreys.ContainerOf(building, link.Storey);
            var linkedAncestor = LinkedAncestorOf(node, oldCopies);
            var place = "in '" + container.name + "'";

            if (linkedAncestor == null)
                place = PutIntoStorey(node, building, container) ? place : "stays where it is, so it does not ride with the storey";
            else if (IsCopyUnit(node) && !IsSameCopies(oldCopies[node], oldCopies[linkedAncestor]))
                place = MoveOut(node, container, report);
            else
                place = "copies come from " + LiveBuilderUpgradeReport.PathOf(linkedAncestor);

            var repeat = "";

            if (!link.IsOnRoof && (linkedAncestor == null || node.parent == container.transform))
                repeat = RepeatOnStoreys(link, oldCopies[node].Count, report);

            ResetStoreyCopies(node.gameObject);

            if (link.IsHiddenByMissingStorey)
            {
                node.gameObject.SetActive(true);
                HideStorey(container);
                place += ", its storey is gone: the storey is switched off instead";
            }

            report.AddRestructured(node.gameObject, "storey link became: " + place + repeat);
        }

        // Ближайший предок с привязкой, через который объект получал копии.
        private static Transform LinkedAncestorOf(Transform node, Dictionary<Transform, OldLinkCopies> oldCopies)
        {
            for (var current = node.parent; current != null; current = current.parent)
                if (oldCopies.ContainsKey(current))
                    return current;

            return null;
        }

        // Стена, вырез и вложенный объект из коробок копировались своими числами, а не группой над ними.
        private static bool IsCopyUnit(Transform node)
        {
            return node.GetComponent<SplineWall>() != null || node.GetComponent<GeometryCutter>() != null || node.GetComponent<BoxModel>() != null;
        }

        private static bool IsSameCopies(OldLinkCopies own, OldLinkCopies ancestor)
        {
            return own.Count == ancestor.Count && own.Storey == ancestor.Storey;
        }


        // В этаж здания; из папки внутри «Interior» — в папку с тем же именем в этаже. В другом объекте из коробок, чем здание, — остаётся на месте.
        private static bool PutIntoStorey(Transform node, SplineBuilding building, SplineBuildingStorey container)
        {
            if (LiveBuilderGroups.GroupOf(node.parent) != LiveBuilderGroups.GroupOf(building.transform))
                return false;

            node.SetParent(FolderIn(container, node, building), true);
            return true;
        }

        private static Transform FolderIn(SplineBuildingStorey container, Transform node, SplineBuilding building)
        {
            var interior = SplineBuildingUpgrade.InteriorOf(building);
            var parent = node.parent;

            if (interior == null || parent == interior || !parent.IsChildOf(interior) || LiveBuilderCutRules.IsConstruction(parent))
                return container.transform;

            var folder = container.transform.Find(parent.name);

            if (folder != null)
                return folder;

            var folderObject = new GameObject(parent.name);
            folderObject.transform.SetParent(container.transform, false);
            return folderObject.transform;
        }


        // Повтор выше дал бы другое число копий — выезжает в свой этаж, мир тот же. Дверь теряет место на линии стены.
        private static string MoveOut(Transform node, SplineBuildingStorey container, LiveBuilderUpgradeReport report)
        {
            var wall = node.GetComponentInParent<SplineWall>();
            var cutter = node.GetComponent<GeometryCutter>();
            var constructionBefore = LiveBuilderCutRules.OwnConstructionOf(node);
            node.SetParent(container.transform, true);

            if (cutter != null && wall != null && cutter.FollowsWallLine)
            {
                cutter.FollowsWallLine = false;
                report.AddMayLookDifferent(node.gameObject, "moved out of " + LiveBuilderUpgradeReport.PathOf(wall) + ": no longer follows its line");
            }

            if (cutter != null && cutter.Scope != GeometryCutterScope.EverythingItTouches && LiveBuilderCutRules.OwnConstructionOf(node) != constructionBefore)
                report.AddMayLookDifferent(node.gameObject, "'Own object' is now " + LiveBuilderUpgradeReport.PathOf(LiveBuilderCutRules.OwnConstructionOf(node)) + ": check what it cuts");

            return "moved out into '" + container.name + "': the repeat above it gave a different number of copies";
        }


        // ── Повтор ───────────────────────────────────────────────────────

        // «до верха» → Y до верха, без верхнего этажа — «last»; иначе своё число копий. Одна копия — без повтора.
        private static string RepeatOnStoreys(SplineBuildingStoreyLink link, int count, LiveBuilderUpgradeReport report)
        {
            var building = link.Building;
            var skipsTop = link.SkipsTopStorey && building.StoreyCount - link.Storey > 1;

            if (!link.IsToBuildingTop && count <= 1)
                return "";

            var host = link.TryGetComponent<LiveBuilderArray>(out var taken) && taken.CountY > 1
                ? LiveBuilderRepeatUpgrade.WrapperOf(link.gameObject, report)
                : link.gameObject;
            var array = ArrayOn(host);
            array.StepYIsStoreyHeight = true;
            array.StepY = building.StoreyHeight;
            array.CountYToBuildingTop = link.IsToBuildingTop;
            array.CountY = link.IsToBuildingTop ? Mathf.Max(1, building.StoreyCount - link.Storey) : count;
            array.SkipY = link.IsToBuildingTop && skipsTop ? "last" : "";
            return link.IsToBuildingTop ? ", repeat Y to the building top" + (skipsTop ? ", skipping the last" : "") : ", repeat Y × " + count;
        }

        // Повтор на объекте: свой, если уже есть (Array FeatherKit мог дать X), иначе новый, без копий по X и Z.
        public static LiveBuilderArray ArrayOn(GameObject target)
        {
            if (target.TryGetComponent<LiveBuilderArray>(out var array))
                return array;

            array = target.AddComponent<LiveBuilderArray>();
            array.CountX = 1;
            array.CountY = 1;
            array.CountZ = 1;
            array.StepYIsStoreyHeight = false;
            return array;
        }

        // Копии вверх у стены и выреза задавала привязка — теперь их даёт повтор.
        private static void ResetStoreyCopies(GameObject target)
        {
            if (target.TryGetComponent<SplineWall>(out var wall))
                wall.LegacyCopiesUp = 1;

            if (target.TryGetComponent<GeometryCutter>(out var cutter))
                cutter.LegacyCopiesUp = 1;
        }


        // ── Пропавший этаж ───────────────────────────────────────────────

        private static void HideStorey(SplineBuildingStorey container)
        {
            container.IsHiddenByMissingStorey = true;
            container.gameObject.SetActive(false);
        }


        // ── Стены и вырезы в привязанной группе ──────────────────────────

        // Стена, вырез или вложенный объект без своей привязки в привязанной группе копировались один раз (или своими копиями вверх),
        // а повтор группы скопирует их по этажам. Выезжают в этаж группы, мир тот же.
        private static void MoveOutWhatRepeatsDifferently(Transform linked, Dictionary<Transform, OldLinkCopies> oldCopies, LiveBuilderUpgradeReport report)
        {
            if (!linked.TryGetComponent<LiveBuilderArray>(out _))
                return;

            var container = SplineBuildingStoreys.StoreyOf(linked);

            if (container == null)
                return;

            var units = new List<Transform>();
            CollectCopyUnits(linked, units);

            foreach (var unit in units)
                if (!oldCopies.ContainsKey(unit) && unit.GetComponent<LiveBuilderArray>() == null)
                    report.AddRestructured(unit.gameObject, MoveOut(unit, container, report));
        }

        // Стены, вырезы и вложенные объекты под узлом, не заходя в них самих.
        private static void CollectCopyUnits(Transform node, List<Transform> units)
        {
            foreach (Transform child in node)
            {
                if (IsCopyUnit(child))
                    units.Add(child);
                else
                    CollectCopyUnits(child, units);
            }
        }
    }
}
