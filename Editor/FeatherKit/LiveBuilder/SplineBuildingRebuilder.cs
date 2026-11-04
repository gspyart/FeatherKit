using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Пересобирает части зданий по контуру (<see cref="SplineBuildingPart"/>: стены и пол каждого этажа, крыша),
    /// когда изменились контур, числа или вырезающие коробки объекта вокруг — все части, а когда только вырезы — лишь те части, что они режут. Как у стен по линии: по «подписи»,
    /// меши — файлами рядом со сценой, экземпляр префаба без своих правок берёт меши префаба, общий меш копий не перезаписывается. Вырезающие коробки переводит в дыры перекрытий — прямоугольники в плане здания.
    /// Когда проверять — решает общая очередь <see cref="LiveBuilderRebuildQueue"/>: здания — после объектов из коробок.
    /// </summary>
    public static class SplineBuildingRebuilder
    {
        // Версия раскладки: поменялась — все здания пересобираются, даже если их числа те же. 2 — на точке опоры, 3 — по частям, 4 — прямые откосы и торцы дыр;
        // 6 — развёртка верха стен по плану.
        private const int LayoutVersion = 6;

        // Имя группы этажа: «Storey 1», «Storey 2»… Панель окна узнаёт по нему собранную часть здания.
        public const string StoreyGroupPrefix = "Storey ";


        // ── Пересборка ───────────────────────────────────────────────────

        public static void RebuildChangedBuildings()
        {
            foreach (var building in StageUtility.GetCurrentStageHandle().FindComponentsOfType<SplineBuilding>())
            {
                try
                {
                    if (BoxModelPrefabInstances.TryUsePrefabGeometry(building, () => IsPrefabUpToDate(building), () => PrefabStateOf(building)))
                        continue;

                    var signature = SignatureOf(building);

                    if (building.BuiltSignature != signature || !ArePartsBuilt(building) || !ArePartsCutUpToDate(building, signature))
                        Rebuild(building);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, building);
                }
            }
        }

        // Префаб собран по тому же, что у экземпляра без правок, и все его части на месте.
        private static bool IsPrefabUpToDate(SplineBuilding instance)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            return source != null && source.BuiltSignature == SignatureOf(instance) && ArePartsBuilt(source);
        }

        // Чем сейчас собран префаб — если после пересборки он всё равно не сходится, второй раз его не трогаем.
        private static string PrefabStateOf(SplineBuilding instance)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            return (source != null ? source.BuiltSignature : "") + "|" + (source != null && ArePartsBuilt(source));
        }

        // У каждой части меш прорезан теми вырезами, что режут её сейчас.
        private static bool ArePartsCutUpToDate(SplineBuilding building, string signature)
        {
            foreach (var part in building.GetComponentsInChildren<SplineBuildingPart>(false))
                if (part.BuiltSignature != PartSignatureOf(part, signature, CuttersOf(part, part.UncutBounds)))
                    return false;

            return true;
        }


        // Собирает части: недостающие создаёт, лишние (этажей стало меньше, крышу выключили) убирает.
        // Само здание то же, поменялись только вырезы — пересобираются лишь части, которые они задели: окно на пятом этаже не трогает первый.
        public static void Rebuild(SplineBuilding building)
        {
            RemoveOwnMeshComponents(building);

            var signature = SignatureOf(building);
            var isBuildingSame = building.BuiltSignature == signature;
            var paths = SplineWallPath.FromContainer(building.GetComponent<SplineContainer>(), building.CurveStep);
            var wallHoles = paths.ConvertAll(path => SplineWallRebuilder.CollectHoles(building, path));
            var parts = SplineBuildingMeshBuilder.BuildParts(building, paths, wallHoles, CollectSlabHoles(building));
            var materials = SplineBuildingMeshBuilder.MaterialsOf(building, BoxModelFactory.LoadDefaultMaterial());
            var builtParts = new HashSet<SplineBuildingPart>();
            var isHierarchyChanged = false;

            foreach (var partGeometry in parts)
            {
                var part = FindPart(building, partGeometry.Kind, partGeometry.Storey);

                if (part == null)
                {
                    part = CreatePart(building, partGeometry.Kind, partGeometry.Storey);
                    isHierarchyChanged = true;
                }

                builtParts.Add(part);

                var uncutBounds = partGeometry.Geometry.Bounds;
                var cutters = CuttersOf(part, uncutBounds);
                var partSignature = PartSignatureOf(part, signature, cutters);
                var isPartBuilt = isBuildingSame && part.BuiltSignature == partSignature && part.GetComponent<MeshFilter>().sharedMesh != null;

                if (!isPartBuilt)
                    FillPart(building, part, partGeometry, materials, cutters, partSignature, uncutBounds);
            }

            isHierarchyChanged |= RemoveUnusedParts(building, builtParts);

            if (!isBuildingSame)
            {
                building.BuiltSignature = signature;
                EditorUtility.SetDirty(building);
                LiveBuilderSyncEdits.RecordPrefabInstance(building);
            }

            if (isHierarchyChanged && building.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
        }

        // Меш, материалы и коллайдер одной части. Общий с копией или префабом файл не перезаписывается — у части будет свой.
        private static void FillPart(SplineBuilding building, SplineBuildingPart part, SplineBuildingPartGeometry partGeometry, Material[] materials,
            List<GeometryCutter> cutters, string partSignature, Bounds uncutBounds)
        {
            var written = LiveBuilderMeshWriter.Write(part, building.name + " " + PartName(partGeometry.Kind, partGeometry.Storey), mesh =>
            {
                partGeometry.Geometry.WriteTo(mesh);
                return materials;
            }, cutters);

            if (part.BuiltSignature != partSignature || part.UncutBounds != uncutBounds)
            {
                part.BuiltSignature = partSignature;
                part.UncutBounds = uncutBounds;
                EditorUtility.SetDirty(part);
                LiveBuilderSyncEdits.RecordPrefabInstance(part);
            }

            LiveBuilderMeshWriter.UpdateCollider(part, building.HasCollider, written.Mesh);
        }

        // ── Части здания ─────────────────────────────────────────────────

        // Все части собраны: каждая на месте и с непустым мешем, числом — сколько требуют числа здания.
        private static bool ArePartsBuilt(SplineBuilding building)
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

        private static SplineBuildingPart FindPart(SplineBuilding building, SplineBuildingPartKind kind, int storey)
        {
            foreach (var part in building.GetComponentsInChildren<SplineBuildingPart>(true))
                if (part.Kind == kind && (kind == SplineBuildingPartKind.Roof || part.Storey == storey))
                    return part;

            return null;
        }

        // Новая часть: стены и пол этажа — в группе «Storey N», крыша — прямо в здании. Статика и слой — как у здания.
        private static SplineBuildingPart CreatePart(SplineBuilding building, SplineBuildingPartKind kind, int storey)
        {
            var parent = kind == SplineBuildingPartKind.Roof ? building.transform : StoreyGroupOf(building, storey);
            var partObject = new GameObject(PartName(kind, storey).Replace(StoreyGroupName(storey) + " ", ""));
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

        // Лишние части и опустевшие группы этажей — вон; в экземпляре префаба удалять нельзя — выключаются.
        private static bool RemoveUnusedParts(SplineBuilding building, HashSet<SplineBuildingPart> builtParts)
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
        private static void RemoveOwnMeshComponents(SplineBuilding building)
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

        private static string StoreyGroupName(int storey)
        {
            return StoreyGroupPrefix + (storey + 1);
        }

        // «Storey 2 Walls», «Storey 1 Floor», «Roof» — и имя части (без этажа), и имя файла меша.
        private static string PartName(SplineBuildingPartKind kind, int storey)
        {
            return kind == SplineBuildingPartKind.Roof ? "Roof" : StoreyGroupName(storey) + " " + kind;
        }


        // ── Дыры в перекрытиях ───────────────────────────────────────────

        // Вырезающие коробки объекта, в котором лежит здание: прямоугольник в плане здания и высоты над контуром.
        private static List<SplineBuildingSlabHole> CollectSlabHoles(SplineBuilding building)
        {
            var holes = new List<SplineBuildingSlabHole>();
            var model = SplineWallRebuilder.OuterModelOf(building);

            if (model == null)
                return holes;

            var modelToBuilding = building.transform.worldToLocalMatrix * model.transform.localToWorldMatrix;

            foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
            {
                if (box.Mode != BoxModelPartMode.Cut)
                    continue;

                var min = (Vector3)box.Box.Min / MillimeterBox.MillimetersPerMeter;
                var max = (Vector3)box.Box.Max / MillimeterBox.MillimetersPerMeter;
                var corners = new[]
                {
                    Flat(modelToBuilding.MultiplyPoint3x4(new Vector3(min.x, min.y, min.z))),
                    Flat(modelToBuilding.MultiplyPoint3x4(new Vector3(max.x, min.y, min.z))),
                    Flat(modelToBuilding.MultiplyPoint3x4(new Vector3(max.x, min.y, max.z))),
                    Flat(modelToBuilding.MultiplyPoint3x4(new Vector3(min.x, min.y, max.z)))
                };

                var bottom = modelToBuilding.MultiplyPoint3x4(min).y;
                var top = modelToBuilding.MultiplyPoint3x4(new Vector3(min.x, max.y, min.z)).y;
                holes.Add(new SplineBuildingSlabHole(corners, bottom, top));
            }

            return holes;
        }

        private static Vector2 Flat(Vector3 point)
        {
            return new Vector2(point.x, point.z);
        }


        // ── Подпись ──────────────────────────────────────────────────────

        // Всё, от чего зависит геометрия здания: числа, материалы, точки контура, вырезающие коробки объекта вокруг и где стоят здание и объект.
        // Хеш, одинаковый на любом запуске и машине (<see cref="LiveBuilderStableHash"/>).
        // Вырезы сюда не входят — они у каждой части свои (PartSignatureOf).
        private static string SignatureOf(SplineBuilding building)
        {
            var hash = new Hash128();
            hash.Append(LayoutVersion);
            hash.Append(building.StoreyCount);
            hash.Append(building.StoreyHeight);
            hash.Append(building.WallThickness);
            hash.Append((int)building.ContourSide);
            hash.Append(building.SlabThickness);
            hash.Append(building.HasFloor ? 1 : 0);
            hash.Append(building.HasRoof ? 1 : 0);
            hash.Append(building.HasCollider ? 1 : 0);
            hash.Append(building.CurveStep);
            hash.Append(building.CornerSegments);

            foreach (var material in new[] { building.OuterWallMaterial, building.InnerWallMaterial, building.FloorMaterial, building.CeilingMaterial, building.RoofMaterial })
                hash.Append(BoxModelSideMaterials.KeyOf(material));

            LiveBuilderStableHash.AppendSplines(ref hash, building.GetComponent<SplineContainer>());
            var model = SplineWallRebuilder.OuterModelOf(building);

            if (model != null)
            {
                var cutBoxes = SplineWallRebuilder.CutBoxesHashOf(model, true);
                hash.Append(ref cutBoxes);
                LiveBuilderStableHash.AppendMatrix(ref hash, model.transform.worldToLocalMatrix * building.transform.localToWorldMatrix);
            }

            return hash.ToString();
        }


        // Подпись части: здание и вырезы, что её режут.
        private static string PartSignatureOf(SplineBuildingPart part, string buildingSignature, List<GeometryCutter> cutters)
        {
            return buildingSignature + GeometryCutterLookup.KeyOf(part, cutters);
        }


        // Вырезы, что режут часть с этим габаритом до вырезов (в осях части).
        private static List<GeometryCutter> CuttersOf(SplineBuildingPart part, Bounds uncutBounds)
        {
            return GeometryCutterLookup.CuttersFor(part, GeometryCutterLookup.WorldBoundsOf(uncutBounds, part.transform));
        }
    }
}
