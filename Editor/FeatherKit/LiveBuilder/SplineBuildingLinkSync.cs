using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Сверяет привязанное к зданию (<see cref="SplineBuildingStoreyLink"/>) с самим зданием перед каждой пересборкой. Копий по этажам —
    /// всегда сколько задано в привязке (только свой, «по этаж …», «до верха»):
    /// - копии вверх (<see cref="IStoreyCopies"/>: перегородка, вырез) — по числу этажей в повторе, шаг — этаж; у перегородки ещё и высота — до потолка этажа
    /// - лестница во весь этаж — высота этажа (своя высота остаётся); копии — без верхнего этажа
    /// - коробку, комнату и лестницу по этажам копирует сам объект из коробок (<see cref="BoxModelShapeCollector"/>)
    /// Поменялись у здания высота этажа или пол — объект встаёт на свой этаж на ту же высоту над полом.
    /// Этажа объекта не стало (этажей меньше) — объект прячется; этаж вернулся — объект тоже.
    /// Здание то же, а объект сдвинули руками — запоминается его новое место.
    /// </summary>
    public static class SplineBuildingLinkSync
    {
        private const float Tolerance = 0.001f;


        // Все привязки на сцене или в открытом префабе, и спрятанные тоже — иначе этаж вернулся, а объект остался выключенным.
        // Сначала — те, что выше в иерархии: проём в перегородке едет за ней, а не дважды.
        public static void SyncAll()
        {
            var stage = StageUtility.GetCurrentStageHandle();
            var links = new HashSet<SplineBuildingStoreyLink>(stage.FindComponentsOfType<SplineBuildingStoreyLink>());

            foreach (var building in stage.FindComponentsOfType<SplineBuilding>())
                links.UnionWith(building.GetComponentsInChildren<SplineBuildingStoreyLink>(true));

            var ordered = new List<SplineBuildingStoreyLink>(links);
            ordered.Sort((first, second) => DepthOf(first.transform).CompareTo(DepthOf(second.transform)));

            foreach (var link in ordered)
                if (link.IsValid)
                    Sync(link);
        }

        private static int DepthOf(Transform node)
        {
            var depth = 0;

            for (var current = node.parent; current != null; current = current.parent)
                depth++;

            return depth;
        }


        // Объект — на другой этаж здания, на ту же высоту над полом. Зовёт инспектор, когда меняют «Этаж».
        public static void MoveToStorey(SplineBuildingStoreyLink link, int storey)
        {
            Undo.RecordObjects(new Object[] { link, link.transform }, "Change Storey");
            link.Storey = Mathf.Clamp(storey, 0, link.Building.StoreyCount - 1);
            link.SyncedStoreyHeight = -1f;
            Sync(link);
        }


        // Новая привязка: к какому зданию, «до верха» ли, и запомнить, где объект стоит сейчас.
        public static SplineBuildingStoreyLink Attach(GameObject target, SplineBuilding building, bool isToBuildingTop, bool skipsTopStorey = false)
        {
            var link = target.GetComponent<SplineBuildingStoreyLink>();

            if (link == null)
                link = Undo.AddComponent<SplineBuildingStoreyLink>(target);

            // Сверка ниже меняет место, высоту и копии без Undo — записать их заранее: Ctrl+Z вернёт всё вместе с привязкой.
            Undo.RecordObjects(ChangedBySync(target, link), "Attach To Building Storeys");

            link.Building = building;
            link.IsToBuildingTop = isToBuildingTop;
            link.SkipsTopStorey = skipsTopStorey;
            RememberPlace(link, building);
            Sync(link);
            return link;
        }

        // Что сверка привязки может поменять у объекта: сама привязка, место, копии вверх, высота стены и лестницы, видимость.
        private static Object[] ChangedBySync(GameObject target, SplineBuildingStoreyLink link)
        {
            var changed = new List<Object> { link, target.transform, target };

            foreach (var storeyCopies in target.GetComponents<IStoreyCopies>())
                changed.Add((Object)storeyCopies);

            if (target.TryGetComponent<SplineWall>(out var wall))
                changed.Add(wall);

            if (target.TryGetComponent<BoxModelStairs>(out var stairs))
                changed.Add(stairs);

            return changed.ToArray();
        }


        // ── Сверка ───────────────────────────────────────────────────────

        // Сверить одну привязку сейчас: есть ли её этаж, место на этаже, высоты и число копий.
        public static void Sync(SplineBuildingStoreyLink link)
        {
            var building = link.Building;

            if (link.Storey >= building.StoreyCount)
            {
                Hide(link);
                return;
            }

            Show(link);

            var previousStoreyHeight = link.SyncedStoreyHeight;
            var isBuildingChanged = Mathf.Abs(link.SyncedStoreyHeight - building.StoreyHeight) > Tolerance
                || Mathf.Abs(link.SyncedFirstFloor - building.FirstFloorWorldHeight) > Tolerance;

            if (isBuildingChanged)
                MoveToPlace(link, building);
            else
                RememberPlace(link, building);

            var copies = link.CopiesFrom(link.Storey);

            if (link.TryGetComponent<IStoreyCopies>(out var storeyCopies))
            {
                var target = (Object)storeyCopies;
                SetIfDifferent(target, storeyCopies.CopyStep, building.StoreyHeight, value => storeyCopies.CopyStep = value);

                if (storeyCopies.CopiesUp != copies)
                    LiveBuilderSyncEdits.Apply(target, () => storeyCopies.CopiesUp = copies);
            }

            if (link.TryGetComponent<SplineWall>(out var wall))
                SetIfDifferent(wall, wall.Height, building.ClearHeight, value => wall.Height = value);

            // Лестница во весь этаж следит за высотой этажа; своя высота (полэтажа, площадка) остаётся.
            if (link.TryGetComponent<BoxModelStairs>(out var stairs) && Mathf.Abs(stairs.Height - previousStoreyHeight) < Tolerance)
                SetIfDifferent(stairs, stairs.Height, building.StoreyHeight, value => stairs.Height = value);

            // С верхнего этажа лестнице идти некуда: копии — без него.
            if (stairs != null && !link.SkipsTopStorey)
                LiveBuilderSyncEdits.Apply(link, () => link.SkipsTopStorey = true);
        }


        // ── Пропавший этаж ───────────────────────────────────────────────

        // Этажа нет — объект выключен, и помечено, что выключил его не человек.
        private static void Hide(SplineBuildingStoreyLink link)
        {
            if (!link.gameObject.activeSelf)
                return;

            LiveBuilderSyncEdits.Apply(link, () => link.IsHiddenByMissingStorey = true);
            link.gameObject.SetActive(false);
            LiveBuilderSyncEdits.RecordPrefabInstance(link.gameObject);
        }

        // Этаж снова есть — включить то, что прятали из-за него. Выключенное руками не трогаем.
        private static void Show(SplineBuildingStoreyLink link)
        {
            if (!link.IsHiddenByMissingStorey)
                return;

            LiveBuilderSyncEdits.Apply(link, () => link.IsHiddenByMissingStorey = false);
            link.gameObject.SetActive(true);
            LiveBuilderSyncEdits.RecordPrefabInstance(link.gameObject);
        }


        // ── Место на этаже ───────────────────────────────────────────────

        // Запомнить, где объект сейчас: этаж и высота над его полом. Что лежит в перекрытии над этажом — уже этаж выше, чуть ниже его пола:
        // вырез в потолке едет за перекрытием, а «до верха» не пробивает крышу.
        private static void RememberPlace(SplineBuildingStoreyLink link, SplineBuilding building)
        {
            var worldHeight = link.transform.position.y;
            var storey = building.StoreyAtHeight(worldHeight + building.SlabThickness);
            var heightAboveFloor = worldHeight - (building.FirstFloorWorldHeight + storey * building.StoreyHeight);

            if (link.Storey != storey || Mathf.Abs(link.HeightAboveFloor - heightAboveFloor) > Tolerance
                || Mathf.Abs(link.SyncedStoreyHeight - building.StoreyHeight) > Tolerance || Mathf.Abs(link.SyncedFirstFloor - building.FirstFloorWorldHeight) > Tolerance)
                LiveBuilderSyncEdits.Apply(link, () =>
                {
                    link.Storey = storey;
                    link.HeightAboveFloor = heightAboveFloor;
                    link.SyncedStoreyHeight = building.StoreyHeight;
                    link.SyncedFirstFloor = building.FirstFloorWorldHeight;
                });
        }

        // Здание поменяло высоту этажа или пол — объект на свой этаж, на ту же высоту над полом.
        private static void MoveToPlace(SplineBuildingStoreyLink link, SplineBuilding building)
        {
            var targetHeight = building.FirstFloorWorldHeight + link.Storey * building.StoreyHeight + link.HeightAboveFloor;
            var shift = targetHeight - link.transform.position.y;

            if (Mathf.Abs(shift) > Tolerance)
                LiveBuilderSyncEdits.Apply(link.transform, () => link.transform.position += Vector3.up * shift);

            LiveBuilderSyncEdits.Apply(link, () =>
            {
                link.SyncedStoreyHeight = building.StoreyHeight;
                link.SyncedFirstFloor = building.FirstFloorWorldHeight;
            });
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static void SetIfDifferent(Object target, float current, float wanted, System.Action<float> set)
        {
            if (Mathf.Abs(current - wanted) > Tolerance)
                LiveBuilderSyncEdits.Apply(target, () => set(wanted));
        }
    }
}
