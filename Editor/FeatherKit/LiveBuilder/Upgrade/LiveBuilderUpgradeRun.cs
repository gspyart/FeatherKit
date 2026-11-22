using System.Collections.Generic;
using FeatherKit.LevelDesign;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Один проход обновления старых данных по корням сцены или префаба — в каком порядке что делается:
    /// - «как было»: скрытые копии Array FeatherKit по нынешним числам, потом снимок по сохранённым мешам (<see cref="LiveBuilderUpgradeSnapshot"/>)
    /// - преобразования: здания, Array, привязки, повторы, копии вверх, комнаты, вырезающие коробки, версии, порядок вырезов, «Где режет»
    /// - новая пересборка и сверка «как стало» — расхождения в итог. Без Undo: это обновление при загрузке
    /// </summary>
    public static class LiveBuilderUpgradeRun
    {
        public static void Run(IReadOnlyList<GameObject> roots, bool isPrefabAsset, LiveBuilderUpgradeReport report)
        {
            RebuildArrayModifiers(roots);

            // Старой сборки больше нет: «как было» — меши, которые она сохранила, и тело коробок по старому сборщику.
            var snapshot = LiveBuilderUpgradeSnapshot.Take(roots);
            var cutOrder = LiveBuilderCutOrderUpgrade.Plan(snapshot, report);
            var copyConflicts = LiveBuilderCopyConflictUpgrade.Plan(snapshot, report);
            var legacyModels = new List<BoxModel>(snapshot.LegacyModels);
            var buildings = SplineBuildingUpgrade.OldBuildingsUnder(roots);

            foreach (var building in buildings)
                SplineBuildingUpgrade.Upgrade(building, report);

            LiveBuilderRepeatUpgrade.UpgradeArrayModifiers(roots, report);
            LiveBuilderStoreyLinkUpgrade.Upgrade(roots, report);

            foreach (var building in buildings)
                SplineBuildingUpgrade.FinishInterior(building, report);

            LiveBuilderRepeatUpgrade.UpgradeRepeatGroups(roots, report);
            LiveBuilderRepeatUpgrade.UpgradeCopiesUp(roots, report);
            LiveBuilderRoomUpgrade.Upgrade(legacyModels, report);
            var cutters = LiveBuilderCutBoxUpgrade.ConvertCutBoxes(roots, report);
            RaiseVersions(legacyModels, report);

            BoxModelRebuilder.ForgetCachedResults();
            copyConflicts.Apply(report);
            cutOrder.Apply(roots, report);
            LiveBuilderCutBoxUpgrade.UpgradeScopes(roots, isPrefabAsset, report);

            BoxModelRebuilder.ForgetCachedResults();
            LiveBuilderCutBoxUpgrade.KeepWallMaterialsOnHoles(cutters, snapshot, report);
            LiveBuilderCutBoxUpgrade.FitThinCutsToWalls(cutters, report);

            Rebuild(roots);
            snapshot.CompareAfter(copyConflicts, report);
        }


        // ── Версии ───────────────────────────────────────────────────────

        private static void RaiseVersions(List<BoxModel> legacyModels, LiveBuilderUpgradeReport report)
        {
            foreach (var model in legacyModels)
            {
                if (model == null || PrefabUtility.IsPartOfPrefabInstance(model))
                    continue;

                model.DataVersion = BoxModel.CurrentDataVersion;
                EditorUtility.SetDirty(model);
                report.IsDataVersionRaised = true;
            }
        }


        // ── Пересборка ───────────────────────────────────────────────────

        // Скрытые копии Array на объектах Live Builder — по нынешним числам: без них «как было» неполное.
        private static void RebuildArrayModifiers(IReadOnlyList<GameObject> roots)
        {
            foreach (var root in roots)
                foreach (var array in root.GetComponentsInChildren<ArrayModifier>(true))
                    if (array.GetComponent<ArrayPreviewCopy>() == null && !ArrayModifierBuilder.IsUpToDate(array))
                        ArrayModifierBuilder.Rebuild(array);
        }


        // Все постройки под корнями заново, новым кодом: «как стало» сверяется по свежим мешам. Сцене и открытому префабу
        // сверки делает проход очереди; загруженное содержимое префаба очередь не видит — сверки по списку.
        public static void Rebuild(IReadOnlyList<GameObject> roots)
        {
            BoxModelRebuilder.ForgetCachedResults();

            if (roots.Count > 0 && BoxModelMeshStorage.IsLoadedPrefabContents(roots[0].scene))
                SyncLoadedPrefab(roots[0]);
            else
                LiveBuilderRebuildQueue.RunFinalPassNow();

            BoxModelRebuilder.ForgetCachedResults();
            RebuildEverything(roots);
        }

        private static void SyncLoadedPrefab(GameObject root)
        {
            foreach (var building in root.GetComponentsInChildren<SplineBuilding>(true))
                if (SplineBuildingStoreys.HasStoreyContainers(building))
                    SplineBuildingStoreySync.Sync(building);
        }

        private static void RebuildEverything(IReadOnlyList<GameObject> roots)
        {
            GeometryCutterLookup.BeginPass();
            LiveBuilderMeshWriter.BeginPass();
            LiveBuilderArrayCopyPoses.BeginPass();

            try
            {
                foreach (var root in roots)
                    foreach (var model in root.GetComponentsInChildren<BoxModel>(true))
                        if (!BoxModelRebuilder.TryUsePrefabGeometry(model))
                            BoxModelRebuilder.Rebuild(model);

                foreach (var root in roots)
                    foreach (var building in root.GetComponentsInChildren<SplineBuilding>(true))
                        if (!SplineBuildingRebuilder.TryUsePrefabGeometry(building))
                            SplineBuildingRebuilder.Rebuild(building);

                foreach (var root in roots)
                    foreach (var wall in root.GetComponentsInChildren<SplineWall>(true))
                        if (!SplineWallRebuilder.TryUsePrefabGeometry(wall))
                            SplineWallRebuilder.Rebuild(wall);
            }
            finally
            {
                LiveBuilderArrayCopyPoses.EndPass();
                LiveBuilderMeshWriter.EndPass();
                GeometryCutterLookup.EndPass();
            }
        }
    }
}
