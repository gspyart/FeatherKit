using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Куда встанет новое у инструмента — строка «Into: …»: путь построек Live Builder и этаж, например «Building › Storey 2».
    /// - по месту под мышью: этаж здания, объект из коробок, на котором рисуешь (запоминает окно сцены на движении мыши)
    /// - иначе — по выделенному объекту и этажу уровня
    /// Путь — тот же, что в заголовке карточки панели (<see cref="LiveBuilderCardHeader"/>).
    /// </summary>
    public static class LiveBuilderIntoTarget
    {
        private const string Separator = " › ";

        private static LiveBuilderBuildingContext hoveredBuilding;
        private static BoxModel hoveredModel;


        public static event System.Action HoverChanged;


        private static string LevelStorey => "Level" + Separator + "Storey " + LiveBuilderStoreys.CurrentStoreyNumber;

        private static string NewObject => "New object" + Separator + "Storey " + LiveBuilderStoreys.CurrentStoreyNumber;


        // ── Под мышью ────────────────────────────────────────────────────

        // Окно сцены на движении мыши: здание и объект из коробок под ней; нет — null. Другое место — строка «Into» панели перерисовывается.
        public static void RememberHover(LiveBuilderBuildingContext building, BoxModel model)
        {
            var isSamePlace = model == hoveredModel && building?.Building == hoveredBuilding?.Building && building?.Storey == hoveredBuilding?.Storey;
            hoveredBuilding = building;
            hoveredModel = model;

            if (!isSamePlace)
                HoverChanged?.Invoke();
        }


        // ── Куда ─────────────────────────────────────────────────────────

        // Этаж здания под мышью, иначе этаж уровня: стена, лестница, модуль, здание.
        public static string OfStorey()
        {
            if (hoveredBuilding is not { IsBuildingAlive: true })
                return LevelStorey;

            return PathOf(hoveredBuilding.Building.transform) + Separator + "Storey " + (hoveredBuilding.Storey + 1);
        }


        // Коробка: в объект под мышью, в этаж здания под мышью, в выделенный объект — или новым объектом.
        public static string OfBox(bool isNewModelRequested)
        {
            var building = hoveredBuilding is { IsBuildingAlive: true } ? hoveredBuilding.Building : null;
            var isOnModelInBuilding = hoveredModel != null && building != null && hoveredModel.transform.IsChildOf(building.transform);

            if (building != null && !isOnModelInBuilding)
                return OfStorey() + (isNewModelRequested ? " · new object" : "");

            if (isNewModelRequested)
                return NewObject;

            var model = hoveredModel != null ? hoveredModel : BoxModelActions.SelectedModel();
            return model != null ? PathOf(model.transform) : NewObject;
        }


        // Проём — в стену под мышью; не навёл — как остальные, этаж.
        public static string OfWall(Component wall)
        {
            return wall != null ? PathOf(wall.transform) : OfStorey();
        }


        // ── Путь ─────────────────────────────────────────────────────────

        // Постройки Live Builder и этажи зданий над объектом, от внешней к нему самому; сам объект — последним звеном.
        public static string PathOf(Transform target)
        {
            return string.Join(Separator, PathLinksOf(target).ConvertAll(link => link.name));
        }


        // То же звеньями — заголовок карточки панели выделяет звено по клику.
        public static List<Transform> PathLinksOf(Transform target)
        {
            var links = new List<Transform> { target };

            for (var parent = target.parent; parent != null; parent = parent.parent)
                if (IsPathLink(parent))
                    links.Insert(0, parent);

            return links;
        }

        private static bool IsPathLink(Transform node)
        {
            return node.TryGetComponent<BoxModel>(out _) || node.TryGetComponent<SplineBuilding>(out _)
                || node.TryGetComponent<SplineBuildingStorey>(out _) || node.TryGetComponent<SplineWall>(out _);
        }
    }
}
