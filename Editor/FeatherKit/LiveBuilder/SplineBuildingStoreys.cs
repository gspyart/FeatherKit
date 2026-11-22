using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Этажи здания в иерархии (<see cref="SplineBuildingStorey"/>): «Storey N» на высоте пола этажа и «Roof» на верху крыши.
    /// - найти и создать этаж, поставить все этажи на высоту, назвать по нумерации уровня
    /// - части здания в этаже держит в осях здания: их меши не меняются
    /// - в каком этаже и каком здании лежит узел; у здания до миграции — по привязке к этажам
    /// </summary>
    public static class SplineBuildingStoreys
    {
        public const string RoofName = "Roof";

        // Расхождение меньше этого — на месте, м.
        private const float Tolerance = 0.0005f;


        // ── Здание ───────────────────────────────────────────────────────

        // Здание держит этажи контейнерами; старое (до миграции) — части в группах в нуле здания и «Interior» с привязками.
        public static bool HasStoreyContainers(SplineBuilding building)
        {
            return building != null && building.DataVersion >= SplineBuilding.CurrentDataVersion;
        }


        // ── Этажи здания ─────────────────────────────────────────────────

        // Этажи и крыша здания — его прямые дети, и выключенные тоже.
        public static List<SplineBuildingStorey> ContainersOf(SplineBuilding building)
        {
            var containers = new List<SplineBuildingStorey>();

            foreach (Transform child in building.transform)
                if (child.TryGetComponent<SplineBuildingStorey>(out var container))
                    containers.Add(container);

            return containers;
        }


        public static SplineBuildingStorey FindContainer(SplineBuilding building, int storey)
        {
            foreach (var container in ContainersOf(building))
                if (!container.IsRoof && container.Storey == storey)
                    return container;

            return null;
        }


        public static SplineBuildingStorey FindRoof(SplineBuilding building)
        {
            foreach (var container in ContainersOf(building))
                if (container.IsRoof)
                    return container;

            return null;
        }


        // Этаж здания; нет — создаётся на своей высоте, без Undo: это устройство здания, как его части.
        public static SplineBuildingStorey ContainerOf(SplineBuilding building, int storey)
        {
            var container = FindContainer(building, storey);
            return container != null ? container : Create(building, storey, false);
        }


        public static SplineBuildingStorey RoofOf(SplineBuilding building)
        {
            var roof = FindRoof(building);
            return roof != null ? roof : Create(building, 0, true);
        }


        // Этаж, на высоте которого точка мира. Что лежит в перекрытии над этажом — уже этаж выше: вырез в потолке едет с перекрытием.
        public static SplineBuildingStorey ContainerAtHeight(SplineBuilding building, float worldHeight)
        {
            return ContainerOf(building, building.StoreyAtHeight(worldHeight + building.SlabThickness));
        }

        // Этажи — по порядку перед крышей; статика и слой — как у здания.
        private static SplineBuildingStorey Create(SplineBuilding building, int storey, bool isRoof)
        {
            var containerObject = new GameObject(isRoof ? RoofName : NameOf(building, storey));
            containerObject.transform.SetParent(building.transform, false);
            containerObject.layer = building.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(containerObject, GameObjectUtility.GetStaticEditorFlags(building.gameObject));

            var container = containerObject.AddComponent<SplineBuildingStorey>();
            container.Storey = storey;
            container.IsRoof = isRoof;
            container.SyncedStoreyHeight = building.StoreyHeight;
            container.SyncedSlabThickness = building.SlabThickness;
            containerObject.transform.localPosition = new Vector3(0f, LocalHeightOf(building, container), 0f);
            containerObject.transform.SetSiblingIndex(SiblingIndexFor(building, container));
            return container;
        }

        // После этажей ниже, перед этажами выше и крышей.
        private static int SiblingIndexFor(SplineBuilding building, SplineBuildingStorey created)
        {
            var index = 0;

            foreach (Transform child in building.transform)
            {
                if (child == created.transform)
                    continue;

                if (child.TryGetComponent<SplineBuildingStorey>(out var other) && !other.IsRoof && (created.IsRoof || other.Storey < created.Storey))
                    index = child.GetSiblingIndex() + 1;
            }

            return Mathf.Min(index, building.transform.childCount - 1);
        }


        // ── Место и имя ──────────────────────────────────────────────────

        // Все этажи здания — на свою высоту, с именем по нумерации уровня; части в них — в осях здания. Правки без Undo, как у сверок.
        public static void PlaceContainers(SplineBuilding building)
        {
            var baseHeight = SplineBuildingLevelAlignment.BaseHeightOf(building);
            var levelStorey = LevelStoreyOf(building);

            foreach (var container in ContainersOf(building))
            {
                var place = container.transform;
                var localPosition = new Vector3(0f, LocalHeightOf(building, container, baseHeight), 0f);

                if ((place.localPosition - localPosition).sqrMagnitude > Tolerance * Tolerance || place.localRotation != Quaternion.identity || place.localScale != Vector3.one)
                    LiveBuilderSyncEdits.Apply(place, () =>
                    {
                        place.localPosition = localPosition;
                        place.localRotation = Quaternion.identity;
                        place.localScale = Vector3.one;
                    });

                var name = container.IsRoof ? RoofName : NameFor(levelStorey, container.Storey);

                if (container.name != name)
                    LiveBuilderSyncEdits.Apply(container.gameObject, () => container.name = name);

                foreach (Transform child in place)
                    if (child.TryGetComponent<SplineBuildingPart>(out _))
                        KeepInBuildingAxes(child);
            }
        }


        // Часть здания в этаже — со сдвигом назад: её оси — оси здания, где бы этаж ни стоял.
        public static void KeepInBuildingAxes(Transform part)
        {
            var container = part.parent;

            if (container == null || !container.TryGetComponent<SplineBuildingStorey>(out _))
                return;

            var localPosition = -container.localPosition;

            if (part.localPosition != localPosition || part.localRotation != Quaternion.identity || part.localScale != Vector3.one)
                LiveBuilderSyncEdits.Apply(part, () =>
                {
                    part.localPosition = localPosition;
                    part.localRotation = Quaternion.identity;
                    part.localScale = Vector3.one;
                });
        }


        // «Storey 3» у этажа 0 здания, что стоит на третьем этаже уровня; подвалы — «Storey -1».
        public static string NameOf(SplineBuilding building, int storey)
        {
            return NameFor(LevelStoreyOf(building), storey);
        }

        private static string NameFor(int levelStorey, int storey)
        {
            return SplineBuildingParts.StoreyGroupPrefix + LiveBuilderStoreys.NumberOf(levelStorey + storey);
        }


        // Этаж уровня, на котором стоит низ здания, с нуля: записанный у здания, а у свободного — ближайший по этажам уровня его сцены.
        public static int LevelStoreyOf(SplineBuilding building)
        {
            var placement = building.LevelPlacement;
            return placement.IsOnLevelStorey ? placement.LevelStorey : SplineBuildingLevelSync.NearestLevelStoreyOf(building);
        }


        // Высота этажа в осях здания: пол этажа, у крыши — её верх.
        public static float LocalHeightOf(SplineBuilding building, SplineBuildingStorey container)
        {
            return LocalHeightOf(building, container, SplineBuildingLevelAlignment.BaseHeightOf(building));
        }

        private static float LocalHeightOf(SplineBuilding building, SplineBuildingStorey container, float baseHeight)
        {
            return container.IsRoof ? baseHeight + building.TotalHeight : baseHeight + building.FloorTopHeight + container.Storey * building.StoreyHeight;
        }


        // ── Узел ─────────────────────────────────────────────────────────

        // Этаж здания, в котором лежит узел (или он сам). Нет — null.
        public static SplineBuildingStorey StoreyOf(Transform node)
        {
            for (var current = node; current != null; current = current.parent)
                if (current.TryGetComponent<SplineBuildingStorey>(out var container))
                    return container;

            return null;
        }


        // Здание, частью которого стал кусок: лежит в его этаже, привязан к его этажам сам или с группой, либо просто лежит в нём. Нет — null.
        public static SplineBuilding BuildingOf(Transform node)
        {
            if (node == null)
                return null;

            var container = StoreyOf(node);

            if (container != null && container.Building != null)
                return container.Building;

            var link = node.GetComponentInParent<SplineBuildingStoreyLink>(true);
            return link != null && link.IsValid ? link.Building : node.GetComponentInParent<SplineBuilding>(true);
        }


        // Здание, к этажам которого относится узел: лежит в его этаже или привязан к нему сам. Нет — null.
        public static SplineBuilding BuildingOfStoreyContent(Transform node)
        {
            if (node == null)
                return null;

            var container = StoreyOf(node);

            if (container != null && container.Building != null)
                return container.Building;

            return node.TryGetComponent<SplineBuildingStoreyLink>(out var link) && link.IsValid ? link.Building : null;
        }


        // Здание и этаж узла, с нуля: этаж, в котором он лежит (на крыше — «над верхним»), или его привязка.
        public static bool TryGetStoreyOf(Transform node, out SplineBuilding building, out int storey)
        {
            building = null;
            storey = 0;
            var container = StoreyOf(node);

            if (container != null && container.Building != null)
            {
                building = container.Building;
                storey = container.IsRoof ? building.StoreyCount : container.Storey;
                return true;
            }

            if (node == null || !node.TryGetComponent<SplineBuildingStoreyLink>(out var link) || !link.IsValid)
                return false;

            building = link.Building;
            storey = link.Storey;
            return true;
        }
    }
}
