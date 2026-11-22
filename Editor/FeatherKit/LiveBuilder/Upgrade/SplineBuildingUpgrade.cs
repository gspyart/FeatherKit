using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Здание старого вида (версия 0) — в новый: этажи-контейнеры «Storey N» и «Roof» вместо групп в нуле здания и папки «Interior».
    /// - здание без объекта из коробок над ним оборачивается в «&lt;имя&gt; Object»; части — в свои этажи, в осях здания; «Floor» → «Slab»
    /// - флаги уровня — по тому, где здание стоит; привязанное разъезжается по этажам отдельно (<see cref="LiveBuilderStoreyLinkUpgrade"/>)
    /// - оставшееся в «Interior» — в этаж по высоте или на крышу; пустая папка удаляется
    /// </summary>
    public static class SplineBuildingUpgrade
    {
        private const string InteriorName = "Interior";
        private const string OldSlabName = "Floor";
        private const string WrapperSuffix = " Object";


        // ── Здание ───────────────────────────────────────────────────────

        // Старые здания под корнями. До их обновления — список: обновление меняет версию.
        public static List<SplineBuilding> OldBuildingsUnder(IReadOnlyList<GameObject> roots)
        {
            var buildings = new List<SplineBuilding>();

            foreach (var root in roots)
                foreach (var building in root.GetComponentsInChildren<SplineBuilding>(true))
                    if (!SplineBuildingStoreys.HasStoreyContainers(building) && !PrefabUtility.IsPartOfPrefabInstance(building))
                        buildings.Add(building);

            return buildings;
        }


        // Обёртка, флаги уровня, этажи и части в них. Интерьер — потом, после привязок (FinishInterior).
        public static void Upgrade(SplineBuilding building, LiveBuilderUpgradeReport report)
        {
            WrapIntoModel(building, report);
            building.DataVersion = SplineBuilding.CurrentDataVersion;
            SplineBuildingLevelSync.ReadPlacementOfOldBuilding(building);

            for (var storey = 0; storey < building.StoreyCount; storey++)
                SplineBuildingStoreys.ContainerOf(building, storey);

            if (building.HasRoof)
                SplineBuildingStoreys.RoofOf(building);

            SplineBuildingStoreys.PlaceContainers(building);
            MoveParts(building);
            MoveOldStoreyGroups(building);
            EditorUtility.SetDirty(building);
            report.AddRestructured(building, "storeys became '" + SplineBuildingParts.StoreyGroupPrefix + "N' and '" + SplineBuildingStoreys.RoofName + "' containers" +
                                             (building.LevelPlacement.IsOnLevelStorey ? ", stands on level storey " + LiveBuilderStoreys.NumberOf(building.LevelPlacement.LevelStorey) : ", free") +
                                             (building.LevelPlacement.UsesLevelStoreyHeight ? ", level storey height" : ", own storey height"));
        }

        // Здание всегда в объекте из коробок: иначе его куски (лестница, комната) не в чём собирать. Корень префаба обернуть нельзя.
        private static void WrapIntoModel(SplineBuilding building, LiveBuilderUpgradeReport report)
        {
            var transform = building.transform;

            if (transform.parent != null && transform.parent.GetComponentInParent<BoxModel>(true) != null)
                return;

            if (transform.parent == null && BoxModelMeshStorage.IsLoadedPrefabContents(building.gameObject.scene))
            {
                report.AddMayLookDifferent(building, "building at the prefab root is not wrapped into an object: rooms and stairs in it need one");
                return;
            }

            var wrapperObject = new GameObject(building.name + WrapperSuffix);
            wrapperObject.transform.SetParent(transform.parent, false);
            wrapperObject.transform.SetSiblingIndex(transform.GetSiblingIndex());
            wrapperObject.transform.SetPositionAndRotation(transform.position, transform.rotation);
            wrapperObject.layer = building.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(wrapperObject, GameObjectUtility.GetStaticEditorFlags(building.gameObject));

            if (transform.parent == null)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(wrapperObject, building.gameObject.scene);

            var wrapper = wrapperObject.AddComponent<BoxModel>();
            wrapper.DataVersion = BoxModel.CurrentDataVersion;
            wrapper.DefaultMaterial = BoxModelFactory.LoadDefaultMaterial();
            wrapper.TextureAlignment = building.TextureAlignment;
            transform.SetParent(wrapperObject.transform, true);
            report.AddRestructured(wrapperObject, "new object around the building: a building always stands in one now");
        }


        // Части — в свой этаж и в оси здания; у перекрытия имя «Slab».
        private static void MoveParts(SplineBuilding building)
        {
            foreach (var part in building.GetComponentsInChildren<SplineBuildingPart>(true))
            {
                var container = part.Kind == SplineBuildingPartKind.Roof ? SplineBuildingStoreys.RoofOf(building) : SplineBuildingStoreys.ContainerOf(building, part.Storey);
                part.transform.SetParent(container.transform, false);
                SplineBuildingStoreys.KeepInBuildingAxes(part.transform);

                if (part.name == OldSlabName)
                    part.name = SplineBuildingParts.PartNameOf(building, part.Kind);
            }
        }

        // Что ещё лежало в старых группах «Storey N» — в тот же этаж, мир тот же; пустые группы — вон.
        private static void MoveOldStoreyGroups(SplineBuilding building)
        {
            var oldGroups = new List<Transform>();

            foreach (Transform child in building.transform)
                if (child.name.StartsWith(SplineBuildingParts.StoreyGroupPrefix) && !child.TryGetComponent<SplineBuildingStorey>(out _))
                    oldGroups.Add(child);

            foreach (var group in oldGroups)
            {
                var number = int.TryParse(group.name.Substring(SplineBuildingParts.StoreyGroupPrefix.Length), out var parsed) ? parsed - 1 : 0;
                var container = SplineBuildingStoreys.ContainerOf(building, Mathf.Max(0, number));

                while (group.childCount > 0)
                    group.GetChild(0).SetParent(container.transform, true);

                Object.DestroyImmediate(group.gameObject);
            }
        }


        // ── Интерьер ─────────────────────────────────────────────────────

        // Папка «Interior» здания, если есть.
        public static Transform InteriorOf(SplineBuilding building)
        {
            return building.transform.Find(InteriorName);
        }


        // Что осталось в «Interior» после привязок — в этаж по высоте (в перекрытии — этаж выше) или на крышу; пустая папка удаляется.
        public static void FinishInterior(SplineBuilding building, LiveBuilderUpgradeReport report)
        {
            var interior = InteriorOf(building);

            if (interior == null)
                return;

            var roofHeight = building.transform.TransformPoint(new Vector3(0f, SplineBuildingStoreys.LocalHeightOf(building, SplineBuildingStoreys.RoofOf(building)), 0f)).y;
            var children = new List<Transform>();

            foreach (Transform child in interior)
                children.Add(child);

            foreach (var child in children)
            {
                var isOnRoof = child.position.y >= roofHeight - 0.001f;
                var container = isOnRoof ? SplineBuildingStoreys.RoofOf(building) : SplineBuildingStoreys.ContainerAtHeight(building, child.position.y);
                child.SetParent(container.transform, true);
                report.AddRestructured(child.gameObject, "moved from 'Interior' into '" + container.name + "' by its height");
            }

            if (interior.childCount == 0)
                Object.DestroyImmediate(interior.gameObject);

            if (!building.HasRoof)
                RemoveEmptyRoof(building);
        }

        // Крыши у здания нет, а контейнер понадобился только для проверки высоты.
        private static void RemoveEmptyRoof(SplineBuilding building)
        {
            var roof = SplineBuildingStoreys.FindRoof(building);

            if (roof != null && roof.transform.childCount == 0)
                Object.DestroyImmediate(roof.gameObject);
        }
    }
}
