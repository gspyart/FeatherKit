using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Вид выделенного объекта для карточки панели: от него зависят секции и плашки «2 Box · 1 Cut».</summary>
    public enum LiveBuilderSelectionKind
    {
        Other,
        Box,
        LegacyCutBox,
        Cut,
        Room,
        Stairs,
        Column,
        Wall,
        Building,
        BoxModel,
        Group,
        Storey,
        BuildingPart,
        Module
    }


    /// <summary>
    /// Что за постройка Live Builder выделена — одно правило для карточки панели (<see cref="LiveBuilderSelectionCardSet"/>) и её заголовка.
    /// - вид объекта и его имя на плашке
    /// - сколько выделено каждого вида, по порядку первого появления
    /// </summary>
    public static class LiveBuilderSelectionKinds
    {
        // ── Вид ──────────────────────────────────────────────────────────

        public static LiveBuilderSelectionKind KindOf(GameObject selected)
        {
            if (selected == null)
                return LiveBuilderSelectionKind.Other;

            if (selected.TryGetComponent<BoxModelPart>(out var part))
                return part.Mode == BoxModelPartMode.Add ? LiveBuilderSelectionKind.Box : LiveBuilderSelectionKind.LegacyCutBox;

            if (selected.TryGetComponent<BoxModelRoom>(out _))
                return LiveBuilderSelectionKind.Room;

            if (selected.TryGetComponent<BoxModelStairs>(out _))
                return LiveBuilderSelectionKind.Stairs;

            if (selected.TryGetComponent<GeometryCutter>(out _))
                return LiveBuilderSelectionKind.Cut;

            if (selected.TryGetComponent<LiveBuilderColumn>(out _))
                return LiveBuilderSelectionKind.Column;

            if (selected.TryGetComponent<SplineWall>(out _))
                return LiveBuilderSelectionKind.Wall;

            if (selected.TryGetComponent<SplineBuilding>(out _))
                return LiveBuilderSelectionKind.Building;

            if (selected.TryGetComponent<SplineBuildingStorey>(out _))
                return LiveBuilderSelectionKind.Storey;

            if (IsBuildingPart(selected))
                return LiveBuilderSelectionKind.BuildingPart;

            if (selected.TryGetComponent<BoxModel>(out var model))
                return model.transform.parent != null && model.transform.parent.GetComponentInParent<BoxModel>() != null
                    ? LiveBuilderSelectionKind.Group
                    : LiveBuilderSelectionKind.BoxModel;

            return selected.TryGetComponent<LiveBuilderModule>(out _) ? LiveBuilderSelectionKind.Module : LiveBuilderSelectionKind.Other;
        }

        // Собранная часть здания: стены, перекрытие, крыша или группа этажа старого здания. Этаж нового здания — сам вид.
        private static bool IsBuildingPart(GameObject selected)
        {
            if (selected.TryGetComponent<SplineBuildingPart>(out _))
                return true;

            var parent = selected.transform.parent;
            return parent != null && parent.TryGetComponent<SplineBuilding>(out _) && selected.name.StartsWith(SplineBuildingParts.StoreyGroupPrefix);
        }


        public static string NameOf(LiveBuilderSelectionKind kind)
        {
            return kind switch
            {
                LiveBuilderSelectionKind.Box => "Box",
                LiveBuilderSelectionKind.LegacyCutBox => "Legacy Cut Box",
                LiveBuilderSelectionKind.Cut => "Cut",
                LiveBuilderSelectionKind.Room => "Room",
                LiveBuilderSelectionKind.Stairs => "Stairs",
                LiveBuilderSelectionKind.Column => "Column",
                LiveBuilderSelectionKind.Wall => "Wall",
                LiveBuilderSelectionKind.Building => "Building",
                LiveBuilderSelectionKind.BoxModel => "Box Model",
                LiveBuilderSelectionKind.Group => "Group",
                LiveBuilderSelectionKind.Storey => "Storey",
                LiveBuilderSelectionKind.BuildingPart => "Building Part",
                LiveBuilderSelectionKind.Module => "Module",
                _ => "Object"
            };
        }


        // ── Несколько выделенных ─────────────────────────────────────────

        // Сколько выделено каждого вида; порядок — как виды впервые встретились в выделении.
        public static List<(LiveBuilderSelectionKind Kind, int Count)> CountsOf(IReadOnlyList<GameObject> selected)
        {
            var counts = new List<(LiveBuilderSelectionKind Kind, int Count)>();

            foreach (var each in selected)
            {
                var kind = KindOf(each);
                var index = counts.FindIndex(count => count.Kind == kind);

                if (index < 0)
                    counts.Add((kind, 1));
                else
                    counts[index] = (kind, counts[index].Count + 1);
            }

            return counts;
        }


        public static bool IsOneKind(IReadOnlyList<GameObject> selected)
        {
            return CountsOf(selected).Count <= 1;
        }


        // «2 Box · 1 Cut · 1 Building».
        public static string DescribeCounts(IReadOnlyList<GameObject> selected)
        {
            return string.Join(" · ", CountsOf(selected).ConvertAll(count => count.Count + " " + NameOf(count.Kind)));
        }


        // Выделенные этого вида — клик по плашке оставляет выделенными только их.
        public static List<GameObject> OfKind(IReadOnlyList<GameObject> selected, LiveBuilderSelectionKind kind)
        {
            var result = new List<GameObject>();

            foreach (var each in selected)
                if (KindOf(each) == kind)
                    result.Add(each);

            return result;
        }
    }
}
