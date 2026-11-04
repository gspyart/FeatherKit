using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Объекты частей здания по контуру (<see cref="SplineBuildingPart"/>) в иерархии: найти, создать, убрать лишние, все ли на месте.
    /// Стены и пол этажа лежат в группе «Storey N», крыша — прямо в здании. Имена частей и групп — тоже здесь.
    /// Геометрию частей не строит — это <see cref="SplineBuildingRebuilder"/>.
    /// </summary>
    public static class SplineBuildingParts
    {
        // Имя группы этажа: «Storey 1», «Storey 2»… Панель окна узнаёт по нему собранную часть здания.
        public const string StoreyGroupPrefix = "Storey ";


        // ── Поиск и проверка ─────────────────────────────────────────────

        // Все части собраны: каждая на месте и с непустым мешем, числом — сколько требуют числа здания.
        public static bool AreBuilt(SplineBuilding building)
        {
            var parts = building.GetComponentsInChildren<SplineBuildingPart>(true);
            var expected = building.StoreyCount + (building.StoreyCount - 1) + (building.HasFloor ? 1 : 0) + (building.HasRoof ? 1 : 0);
            var active = 0;

            foreach (var part in parts)
            {
                if (!part.gameObject.activeSelf && PrefabUtility.IsPartOfPrefabInstance(part))
                    continue;

                var mesh = part.GetComponent<MeshFilter>().sharedMesh;

                if (mesh == null)
                    return false;

                active++;
            }

            return active == expected;
        }


        public static SplineBuildingPart Find(SplineBuilding building, SplineBuildingPartKind kind, int storey)
        {
            foreach (var part in building.GetComponentsInChildren<SplineBuildingPart>(true))
                if (part.Kind == kind && (kind == SplineBuildingPartKind.Roof || part.Storey == storey))
                    return part;

            return null;
        }


        // ── Создание ─────────────────────────────────────────────────────

        // Новая часть: стены и пол этажа — в группе «Storey N», крыша — прямо в здании. Статика и слой — как у здания.
        public static SplineBuildingPart Create(SplineBuilding building, SplineBuildingPartKind kind, int storey)
        {
            var parent = kind == SplineBuildingPartKind.Roof ? building.transform : StoreyGroupOf(building, storey);
            var partObject = new GameObject(NameOf(kind, storey).Replace(StoreyGroupName(storey) + " ", ""));
            partObject.transform.SetParent(parent, false);
            partObject.layer = building.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(partObject, GameObjectUtility.GetStaticEditorFlags(building.gameObject));

            var part = partObject.AddComponent<SplineBuildingPart>();
            part.Kind = kind;
            part.Storey = storey;
            return part;
        }

        private static Transform StoreyGroupOf(SplineBuilding building, int storey)
        {
            var name = StoreyGroupName(storey);
            var group = building.transform.Find(name);

            if (group != null)
                return group;

            var groupObject = new GameObject(name);
            groupObject.transform.SetParent(building.transform, false);
            groupObject.transform.SetSiblingIndex(Mathf.Min(storey, building.transform.childCount - 1));
            groupObject.layer = building.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(groupObject, GameObjectUtility.GetStaticEditorFlags(building.gameObject));
            return groupObject.transform;
        }


        // ── Уборка ───────────────────────────────────────────────────────

        // Лишние части и опустевшие группы этажей — вон; в экземпляре префаба удалять нельзя — выключаются.
        public static bool RemoveUnused(SplineBuilding building, HashSet<SplineBuildingPart> builtParts)
        {
            var isChanged = false;

            foreach (var part in building.GetComponentsInChildren<SplineBuildingPart>(true))
            {
                if (builtParts.Contains(part))
                {
                    if (!part.gameObject.activeSelf && PrefabUtility.IsPartOfPrefabInstance(part))
                        part.gameObject.SetActive(true);

                    continue;
                }

                RemoveObject(part.gameObject);
                isChanged = true;
            }

            // Группа этажа уходит, только если в ней совсем ничего не осталось: положенное туда руками не теряется.
            var emptyGroups = new List<GameObject>();

            foreach (Transform child in building.transform)
                if (child.name.StartsWith(StoreyGroupPrefix) && child.childCount == 0 && child.GetComponents<Component>().Length == 1)
                    emptyGroups.Add(child.gameObject);

            foreach (var group in emptyGroups)
                RemoveObject(group);

            return isChanged || emptyGroups.Count > 0;
        }

        private static void RemoveObject(GameObject target)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                target.SetActive(false);
            else
                Object.DestroyImmediate(target);
        }


        // Здание старого вида держало меш на себе — теперь меши у частей.
        public static void RemoveOwnMeshComponents(SplineBuilding building)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(building))
                return;

            if (building.TryGetComponent<MeshCollider>(out var meshCollider))
                Object.DestroyImmediate(meshCollider);

            if (building.TryGetComponent<MeshRenderer>(out var meshRenderer))
                Object.DestroyImmediate(meshRenderer);

            if (building.TryGetComponent<MeshFilter>(out var meshFilter))
                Object.DestroyImmediate(meshFilter);
        }


        // ── Имена ────────────────────────────────────────────────────────

        // «Storey 2 Walls», «Storey 1 Floor», «Roof» — и имя части (без этажа), и имя файла меша.
        public static string NameOf(SplineBuildingPartKind kind, int storey)
        {
            return kind == SplineBuildingPartKind.Roof ? "Roof" : StoreyGroupName(storey) + " " + kind;
        }


        private static string StoreyGroupName(int storey)
        {
            return StoreyGroupPrefix + (storey + 1);
        }
    }
}
