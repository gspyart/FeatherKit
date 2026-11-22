using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Сверка этажей зданий (<see cref="SplineBuildingStorey"/>) перед каждой пересборкой — то, что делала сверка привязки к этажам:
    /// - этажи есть, стоят на своей высоте и названы; этажа больше нет — выключен с содержимым, пустой убран; вернулся — включён
    /// - содержимое этажа — под его высоту: перегородка до потолка или на весь этаж, лестница и коробка во весь этаж, комната до потолка
    /// Только здания с этажами-контейнерами; правки — без Undo, как у других сверок (<see cref="LiveBuilderSyncEdits"/>).
    /// </summary>
    public static class SplineBuildingStoreySync
    {
        private const float Tolerance = 0.001f;


        // ── Сверка ───────────────────────────────────────────────────────

        public static void SyncAll()
        {
            SyncAll(LiveBuilderRebuildScope.WholeScene);
        }


        // Только здания в области: черновой проход очереди. Ошибка в одном не останавливает остальные.
        public static void SyncAll(LiveBuilderRebuildScope scope)
        {
            foreach (var building in StageUtility.GetCurrentStageHandle().FindComponentsOfType<SplineBuilding>())
            {
                try
                {
                    if (SplineBuildingStoreys.HasStoreyContainers(building) && scope.Contains(building))
                        Sync(building);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, building);
                }
            }
        }


        public static void Sync(SplineBuilding building)
        {
            for (var storey = 0; storey < building.StoreyCount; storey++)
                SplineBuildingStoreys.ContainerOf(building, storey);

            if (building.HasRoof)
                SplineBuildingStoreys.RoofOf(building);

            SplineBuildingStoreys.PlaceContainers(building);

            foreach (var container in SplineBuildingStoreys.ContainersOf(building))
            {
                var isGone = container.IsRoof ? !building.HasRoof : container.Storey >= building.StoreyCount;

                if (isGone && !HasContent(container))
                {
                    Remove(container);
                    continue;
                }

                if (isGone && !container.IsRoof)
                {
                    Hide(container);
                    continue;
                }

                Show(container);

                if (!container.IsRoof)
                    FitContent(container, building);
            }
        }

        // Что-то кроме частей самого здания: части пропавшего этажа здание и так уберёт.
        private static bool HasContent(SplineBuildingStorey container)
        {
            foreach (Transform child in container.transform)
                if (!child.TryGetComponent<SplineBuildingPart>(out _))
                    return true;

            return false;
        }

        // В экземпляре префаба удалять нельзя — выключается.
        private static void Remove(SplineBuildingStorey container)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(container))
                container.gameObject.SetActive(false);
            else
                Object.DestroyImmediate(container.gameObject);
        }


        // ── Пропавший этаж ───────────────────────────────────────────────

        // Этажа нет — выключен, и помечено, что выключил его не человек.
        private static void Hide(SplineBuildingStorey container)
        {
            if (!container.gameObject.activeSelf)
                return;

            LiveBuilderSyncEdits.Apply(container, () => container.IsHiddenByMissingStorey = true);
            container.gameObject.SetActive(false);
            LiveBuilderSyncEdits.RecordPrefabInstance(container.gameObject);
        }

        // Этаж снова есть — включить, если прятала сверка. Выключенный руками не трогаем.
        private static void Show(SplineBuildingStorey container)
        {
            if (!container.IsHiddenByMissingStorey)
                return;

            LiveBuilderSyncEdits.Apply(container, () => container.IsHiddenByMissingStorey = false);
            container.gameObject.SetActive(true);
            LiveBuilderSyncEdits.RecordPrefabInstance(container.gameObject);
        }


        // ── Содержимое ───────────────────────────────────────────────────

        // Высоты содержимого — по прежним правилам привязки, память прошлых высот одна на этаж. Своя высота (не во весь этаж) остаётся.
        private static void FitContent(SplineBuildingStorey container, SplineBuilding building)
        {
            var previousStoreyHeight = container.SyncedStoreyHeight;
            var previousClearHeight = previousStoreyHeight - container.SyncedSlabThickness;

            foreach (var wall in container.GetComponentsInChildren<SplineWall>(true))
                if (wall.HeightInBuilding != SplineWallHeightInBuilding.Custom)
                    SetIfDifferent(wall, wall.Height, wall.HeightInBuilding == SplineWallHeightInBuilding.FullStorey ? building.StoreyHeight : building.ClearHeight,
                        value => wall.Height = value);

            foreach (var stairs in container.GetComponentsInChildren<BoxModelStairs>(true))
                if (Mathf.Abs(stairs.Height - previousStoreyHeight) < Tolerance)
                    SetIfDifferent(stairs, stairs.Height, building.StoreyHeight, value => stairs.Height = value);

            foreach (var part in container.GetComponentsInChildren<BoxModelPart>(true))
                if (Mathf.Abs(part.Size.y - previousStoreyHeight) < Tolerance)
                    SetIfDifferent(part, part.Size.y, building.StoreyHeight, value => part.Size = new Vector3(part.Size.x, value, part.Size.z));

            foreach (var room in container.GetComponentsInChildren<BoxModelRoom>(true))
                if (Mathf.Abs(room.Size.y - previousClearHeight) < Tolerance)
                    SetIfDifferent(room, room.Size.y, building.ClearHeight, value => room.Size = new Vector3(room.Size.x, value, room.Size.z));

            if (Mathf.Abs(previousStoreyHeight - building.StoreyHeight) > Tolerance || Mathf.Abs(container.SyncedSlabThickness - building.SlabThickness) > Tolerance)
                LiveBuilderSyncEdits.Apply(container, () =>
                {
                    container.SyncedStoreyHeight = building.StoreyHeight;
                    container.SyncedSlabThickness = building.SlabThickness;
                });
        }

        private static void SetIfDifferent(Object target, float current, float wanted, System.Action<float> set)
        {
            if (Mathf.Abs(current - wanted) > Tolerance)
                LiveBuilderSyncEdits.Apply(target, () => set(wanted));
        }
    }
}
