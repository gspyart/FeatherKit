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
    /// Режут вырезающие коробки объекта, которые задевают здание (<see cref="BoxModelSolverBox.CutsTarget"/>), где бы ни стояли в списке: стены — те, что режут стены, перекрытия — те, что режут перекрытия.
    /// Когда проверять — решает общая очередь <see cref="LiveBuilderRebuildQueue"/>: здания — после объектов из коробок.
    /// Объекты частей в иерархии — найти, создать, убрать — <see cref="SplineBuildingParts"/>.
    /// </summary>
    public static class SplineBuildingRebuilder
    {
        // Версия раскладки: поменялась — все здания пересобираются, даже если их числа те же. 2 — на точке опоры, 3 — по частям, 4 — прямые откосы и торцы дыр;
        // 6 — развёртка верха стен по плану.
        private const int LayoutVersion = 6;


        // ── Пересборка ───────────────────────────────────────────────────

        public static void RebuildChangedBuildings()
        {
            foreach (var building in StageUtility.GetCurrentStageHandle().FindComponentsOfType<SplineBuilding>())
            {
                try
                {
                    if (TryUsePrefabGeometry(building))
                        continue;

                    var signature = SignatureOf(building);

                    if (building.BuiltSignature != signature || !SplineBuildingParts.AreBuilt(building) || !ArePartsCutUpToDate(building, signature))
                        Rebuild(building);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, building);
                }
            }
        }

        // У каждой части меш прорезан теми вырезами, что режут её сейчас.
        private static bool ArePartsCutUpToDate(SplineBuilding building, string signature)
        {
            foreach (var part in building.GetComponentsInChildren<SplineBuildingPart>(false))
                if (part.BuiltSignature != PartSignatureOf(part, signature, CuttersOf(part, part.UncutBounds)))
                    return false;

            return true;
        }


        // Экземпляр префаба без своих правок берёт меши префаба (отставший префаб пересобирается) — сам тогда не собирается.
        public static bool TryUsePrefabGeometry(SplineBuilding building)
        {
            return BoxModelPrefabInstances.TryUsePrefabGeometry(building, () => IsPrefabUpToDate(building), () => PrefabStateOf(building));
        }

        // Префаб собран по тому же, что у экземпляра без правок, и все его части на месте.
        private static bool IsPrefabUpToDate(SplineBuilding instance)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            return source != null && source.BuiltSignature == SignatureOf(instance) && SplineBuildingParts.AreBuilt(source);
        }

        // Чем сейчас собран префаб — если после пересборки он всё равно не сходится, второй раз его не трогаем.
        private static string PrefabStateOf(SplineBuilding instance)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            return (source != null ? source.BuiltSignature : "") + "|" + (source != null && SplineBuildingParts.AreBuilt(source));
        }


        // Собирает части: недостающие создаёт, лишние (этажей стало меньше, крышу выключили) убирает.
        // Само здание то же, поменялись только вырезы — пересобираются лишь части, которые они задели: окно на пятом этаже не трогает первый.
        public static void Rebuild(SplineBuilding building)
        {
            SplineBuildingParts.RemoveOwnMeshComponents(building);

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
                var part = SplineBuildingParts.Find(building, partGeometry.Kind, partGeometry.Storey);

                if (part == null)
                {
                    part = SplineBuildingParts.Create(building, partGeometry.Kind, partGeometry.Storey);
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

            isHierarchyChanged |= SplineBuildingParts.RemoveUnused(building, builtParts);

            if (!isBuildingSame)
            {
                building.BuiltSignature = signature;
                EditorUtility.SetDirty(building);
                LiveBuilderSyncEdits.RecordPrefabInstance(building);
            }

            if (!isHierarchyChanged)
                return;

            if (building.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
        }

        // Меш, материалы и коллайдер одной части. Общий с копией или префабом файл не перезаписывается — у части будет свой.
        private static void FillPart(SplineBuilding building, SplineBuildingPart part, SplineBuildingPartGeometry partGeometry, Material[] materials,
            List<GeometryCutter> cutters, string partSignature, Bounds uncutBounds)
        {
            var written = LiveBuilderMeshWriter.Write(part, building.name + " " + SplineBuildingParts.NameOf(partGeometry.Kind, partGeometry.Storey), mesh =>
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


        // ── Дыры в перекрытиях ───────────────────────────────────────────

        // Вырезающие коробки объекта, в котором лежит здание или замкнутая стена с полом и потолком, — те, что режут перекрытия
        // (<see cref="BoxModelSolverBox.CutsTarget"/>, порядок в списке не важен): прямоугольник в плане владельца и высоты над его линией.
        public static List<SplineBuildingSlabHole> CollectSlabHoles(Component owner)
        {
            var holes = new List<SplineBuildingSlabHole>();
            var model = SplineWallRebuilder.OuterModelOf(owner);

            if (model == null)
                return holes;

            var modelToBuilding = owner.transform.worldToLocalMatrix * model.transform.localToWorldMatrix;

            foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
            {
                if (!box.CutsSlabs)
                    continue;

                if (!box.CutsTarget(owner))
                    continue;

                var min = box.Box.MinInMeters;
                var max = box.Box.MaxInMeters;
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

            // Парапет — только если есть: у прежних зданий подпись та же.
            if (building.HasParapet)
                hash.Append(building.ParapetHeight);
            hash.Append(building.HasCollider ? 1 : 0);
            hash.Append(building.CurveStep);
            hash.Append(building.CornerSegments);

            foreach (var material in new[] { building.OuterWallMaterial, building.InnerWallMaterial, building.FloorMaterial, building.CeilingMaterial, building.RoofMaterial })
                hash.Append(BoxModelSideMaterials.KeyOf(material));

            LiveBuilderStableHash.AppendSplines(ref hash, building.GetComponent<SplineContainer>());
            var model = SplineWallRebuilder.OuterModelOf(building);

            // Коробки — уже в осях здания: где стоит само здание в объекте, подписи не важно — здание едет со своей лестницей без пересборки.
            if (model != null)
            {
                var cutBoxes = SplineWallRebuilder.CutBoxesHashOf(model, building, true);
                hash.Append(ref cutBoxes);
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
