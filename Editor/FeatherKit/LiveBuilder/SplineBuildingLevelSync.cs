using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Здания и этажи уровня своей сцены (<see cref="SplineBuildingLevelPlacement"/>) — перед каждой пересборкой:
    /// - здание с высотой уровня берёт высоту этажа уровня; поменяли высоту у самого здания — она становится своей
    /// - здание на этаже уровня встаёт низом на пол своего этажа, когда меняют этажи уровня
    /// - сдвинули здание руками — запоминается новый этаж, а если низ не на полу этажа — здание свободное
    /// Свободное здание со своей высотой не трогает. Правки — без Undo, как у других сверок (<see cref="LiveBuilderSyncEdits"/>).
    /// </summary>
    public static class SplineBuildingLevelSync
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
                    if (building.LevelPlacement.FollowsLevel && scope.Contains(building))
                        Sync(building);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, building);
                }
            }
        }


        // Поменялся уровень — здание за ним; поменялось само здание при том же уровне — это правка руками, её и запомнить.
        public static void Sync(SplineBuilding building)
        {
            var placement = building.LevelPlacement;
            var levelStoreyHeight = LevelStoreyHeightOf(building);
            var groundLevel = GroundLevelOf(building);
            var isLevelChanged = Mathf.Abs(levelStoreyHeight - placement.SyncedLevelStoreyHeight) > Tolerance
                                 || Mathf.Abs(groundLevel - placement.SyncedGroundLevel) > Tolerance;

            if (placement.UsesLevelStoreyHeight && Mathf.Abs(building.StoreyHeight - levelStoreyHeight) > Tolerance)
            {
                if (isLevelChanged)
                    LiveBuilderSyncEdits.Apply(building, () => building.StoreyHeight = levelStoreyHeight);
                else
                    LiveBuilderSyncEdits.Apply(building, () => placement.UsesLevelStoreyHeight = false);
            }

            if (placement.IsOnLevelStorey)
                SyncLevelStorey(building, isLevelChanged, levelStoreyHeight, groundLevel);

            if (isLevelChanged)
                LiveBuilderSyncEdits.Apply(building, () =>
                {
                    placement.SyncedLevelStoreyHeight = levelStoreyHeight;
                    placement.SyncedGroundLevel = groundLevel;
                });
        }

        // Низ не на полу своего этажа: уровень поменялся — здание на пол; нет — его сдвинули, и этаж теперь тот, на котором низ.
        private static void SyncLevelStorey(SplineBuilding building, bool isLevelChanged, float levelStoreyHeight, float groundLevel)
        {
            var placement = building.LevelPlacement;
            var offset = BottomWorldHeightOf(building) - (groundLevel + placement.LevelStorey * levelStoreyHeight);

            if (Mathf.Abs(offset) <= Tolerance)
                return;

            if (isLevelChanged)
            {
                LiveBuilderSyncEdits.Apply(building.transform, () => building.transform.position -= Vector3.up * offset);
                return;
            }

            var nearest = NearestLevelStoreyOf(building);

            if (IsOnLevelFloor(building, nearest))
                LiveBuilderSyncEdits.Apply(building, () => placement.LevelStorey = nearest);
            else
                LiveBuilderSyncEdits.Apply(building, () => placement.IsOnLevelStorey = false);
        }


        // ── Правка руками ────────────────────────────────────────────────

        // Здание — на этаж уровня: низ на его пол, интерьер едет вместе. С Undo.
        public static void PutOnLevelStorey(SplineBuilding building, int levelStorey)
        {
            Undo.RecordObjects(new Object[] { building, building.transform }, "Put Building On Level Storey");
            var placement = building.LevelPlacement;
            var offset = BottomWorldHeightOf(building) - (GroundLevelOf(building) + levelStorey * LevelStoreyHeightOf(building));
            building.transform.position -= Vector3.up * offset;
            placement.IsOnLevelStorey = true;
            placement.LevelStorey = levelStorey;
            RememberLevel(building);
        }


        // Здание свободное: стоит, где стоит, этажи уровня его не двигают. С Undo.
        public static void MakeFree(SplineBuilding building)
        {
            Undo.RecordObject(building, "Free Building From Level Storeys");
            building.LevelPlacement.IsOnLevelStorey = false;
        }


        // Высота этажа здания — уровня (сразу берётся) или своя. С Undo.
        public static void UseLevelStoreyHeight(SplineBuilding building, bool isUsed)
        {
            Undo.RecordObject(building, "Change Building Storey Height Source");
            building.LevelPlacement.UsesLevelStoreyHeight = isUsed;

            if (isUsed)
                building.StoreyHeight = LevelStoreyHeightOf(building);

            RememberLevel(building);
        }


        // ── Новое здание ─────────────────────────────────────────────────

        // Высота этажа — уровня; низ на полу этажа уровня — здание стоит на нём, иначе свободное. Зовёт фабрика сразу после создания.
        public static void PlaceOnLevel(SplineBuilding building)
        {
            var placement = building.LevelPlacement;
            var levelStorey = NearestLevelStoreyOf(building);
            building.StoreyHeight = LevelStoreyHeightOf(building);
            placement.UsesLevelStoreyHeight = true;
            placement.IsOnLevelStorey = IsOnLevelFloor(building, levelStorey);
            placement.LevelStorey = levelStorey;
            RememberLevel(building);
        }


        // ── Старое здание ────────────────────────────────────────────────

        // Флаги уровня по тому, где здание стоит сейчас, — ничего не двигает и высоту не меняет (обновление старых данных).
        // Высота уровня — только если совпадает и этажи уровня лежат в сцене; «стоит на этаже» — если низ ровно на полу этажа уровня.
        public static void ReadPlacementOfOldBuilding(SplineBuilding building)
        {
            var placement = building.LevelPlacement;
            var levelStorey = NearestLevelStoreyOf(building);
            var isLevelStoredInScene = LiveBuilderLevelStoreysInScene.FindIn(building.gameObject.scene) != null;
            placement.IsOnLevelStorey = IsOnLevelFloor(building, levelStorey);
            placement.LevelStorey = levelStorey;
            placement.UsesLevelStoreyHeight = isLevelStoredInScene && Mathf.Abs(building.StoreyHeight - LevelStoreyHeightOf(building)) <= Tolerance;
            RememberLevel(building);
        }


        // ── Этажи уровня здания ──────────────────────────────────────────

        // Ближайший к низу здания этаж уровня его сцены, с нуля.
        public static int NearestLevelStoreyOf(SplineBuilding building)
        {
            return Mathf.RoundToInt((BottomWorldHeightOf(building) - GroundLevelOf(building)) / LevelStoreyHeightOf(building));
        }


        // Числа уровня, с которыми здание сверено сейчас: следующая сверка не примет их за правку уровня.
        private static void RememberLevel(SplineBuilding building)
        {
            building.LevelPlacement.SyncedLevelStoreyHeight = LevelStoreyHeightOf(building);
            building.LevelPlacement.SyncedGroundLevel = GroundLevelOf(building);
        }

        private static bool IsOnLevelFloor(SplineBuilding building, int levelStorey)
        {
            var levelFloor = GroundLevelOf(building) + levelStorey * LevelStoreyHeightOf(building);
            return Mathf.Abs(BottomWorldHeightOf(building) - levelFloor) <= Tolerance;
        }

        // Низ здания в мире: низ плиты пола, без пола — низ стен; на высоте контура.
        private static float BottomWorldHeightOf(SplineBuilding building)
        {
            return building.transform.TransformPoint(new Vector3(0f, SplineBuildingLevelAlignment.BaseHeightOf(building), 0f)).y;
        }

        private static float LevelStoreyHeightOf(SplineBuilding building)
        {
            return LiveBuilderStoreys.StoreyHeightIn(building.gameObject.scene);
        }

        private static float GroundLevelOf(SplineBuilding building)
        {
            return LiveBuilderStoreys.GroundLevelIn(building.gameObject.scene);
        }
    }
}
