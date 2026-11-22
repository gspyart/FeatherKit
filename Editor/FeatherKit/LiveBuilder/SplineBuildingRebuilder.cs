using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Пересобирает части зданий по контуру (<see cref="SplineBuildingPart"/>: стены и перекрытие каждого этажа, крыша) — только те,
    /// у которых поменялась своя подпись (<see cref="SplineBuildingPartKeys"/>) или вырезы (<see cref="SplineBuildingPartChecks"/>).
    /// - нерезаная геометрия частей помнится (<see cref="SplineBuildingUncutParts"/>): сдвиг окна только режет заново
    /// - меши — файлами рядом со сценой, экземпляр префаба без правок берёт меши префаба
    /// Когда проверять — решает очередь; где лежат части — <see cref="SplineBuildingParts"/>, дыры перекрытий — <see cref="SplineBuildingSlabHoles"/>.
    /// </summary>
    public static class SplineBuildingRebuilder
    {
        // Версия раскладки: поменялась — все здания пересобираются, даже если их числа те же. 2 — на точке опоры, 3 — по частям, 4 — прямые откосы и торцы дыр;
        // 6 — развёртка верха стен по плану; 7 — подпись у каждой части своя.
        public const int LayoutVersion = 7;


        // ── Пересборка ───────────────────────────────────────────────────

        public static void RebuildChangedBuildings()
        {
            RebuildChangedBuildings(LiveBuilderRebuildScope.WholeScene, LiveBuilderBuildQuality.Final);
        }


        public static void RebuildChangedBuildings(LiveBuilderRebuildScope scope, LiveBuilderBuildQuality quality)
        {
            foreach (var building in scope.Find<SplineBuilding>())
            {
                try
                {
                    if (TryUsePrefabGeometry(building))
                        continue;

                    SplineBuildingPartLayout layout;
                    string signature;
                    SplineBuildingPartChecks partChecks;
                    bool isUpToDate;

                    using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.SignatureCheck))
                    {
                        layout = SplineBuildingPartKeys.LayoutOf(building);
                        signature = SignatureOf(building);
                        partChecks = new SplineBuildingPartChecks(building, layout, quality);
                        isUpToDate = LiveBuilderDraftBuilds.IsBuilt(building, building.BuiltSignature, signature, quality) && SplineBuildingParts.AreBuilt(building)
                                     && partChecks.AreAllUpToDate(layout);
                    }

                    if (!isUpToDate)
                        Rebuild(building, layout, signature, partChecks, quality);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, building);
                }
            }
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


        public static void Rebuild(SplineBuilding building)
        {
            Rebuild(building, LiveBuilderBuildQuality.Final);
        }


        public static void Rebuild(SplineBuilding building, LiveBuilderBuildQuality quality)
        {
            SplineBuildingPartLayout layout;
            string signature;
            SplineBuildingPartChecks partChecks;

            using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.SignatureCheck))
            {
                layout = SplineBuildingPartKeys.LayoutOf(building);
                signature = SignatureOf(building);
                partChecks = new SplineBuildingPartChecks(building, layout, quality);
            }

            Rebuild(building, layout, signature, partChecks, quality);
        }

        // Недостающие части создаёт, лишние (этажей стало меньше, крышу выключили) убирает; собирает только части с другой подписью или вырезами.
        private static void Rebuild(SplineBuilding building, SplineBuildingPartLayout layout, string signature,
            SplineBuildingPartChecks partChecks, LiveBuilderBuildQuality quality)
        {
            LiveBuilderRebuildProfiler.CountRebuiltConstruction();
            SplineBuildingParts.RemoveOwnMeshComponents(building);

            var builtParts = new HashSet<SplineBuildingPart>();
            var partsToFill = new List<(SplineBuildingPart Part, SplineBuildingPartKey Key, SplineBuildingPartCheck Check)>();
            var isHierarchyChanged = false;

            foreach (var partKey in layout.Parts)
            {
                var isCreated = !partChecks.TryGet(partKey, out var check);
                var part = isCreated ? SplineBuildingParts.Create(building, partKey.Kind, partKey.Storey) : check.Part;
                isHierarchyChanged |= isCreated;
                SplineBuildingStoreys.KeepInBuildingAxes(part.transform);
                builtParts.Add(part);

                if (isCreated || !check.IsUpToDate)
                    partsToFill.Add((part, partKey, check));
            }

            var uncutParts = UncutPartsOf(building, layout, partsToFill);
            var materials = SplineBuildingMeshBuilder.MaterialsOf(building, BoxModelFactory.LoadDefaultMaterial());
            var texturing = SplineWallTexturing.ForBuilding(building, materials);
            var worldKey = quality == LiveBuilderBuildQuality.Final ? SplineBuildingPartKeys.WorldKeyOf(building, layout.Paths) : null;

            foreach (var (part, partKey, check) in partsToFill)
                FillPart(building, part, partKey, check, uncutParts[partKey.Key], new SplineBuildingPartLook(materials, texturing, worldKey), quality);

            SplineBuildingUncutParts.ShiftGenerations(building, layout.Parts);
            isHierarchyChanged |= SplineBuildingParts.RemoveUnused(building, builtParts);
            RememberSignature(building, signature, quality);

            if (isHierarchyChanged && building.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
        }

        // Нерезаная геометрия частей, которые пора собрать: из памяти, а чего там нет — строится, только эти части.
        private static Dictionary<string, SplineBuildingUncutPart> UncutPartsOf(SplineBuilding building, SplineBuildingPartLayout layout,
            List<(SplineBuildingPart Part, SplineBuildingPartKey Key, SplineBuildingPartCheck Check)> partsToFill)
        {
            var uncutParts = new Dictionary<string, SplineBuildingUncutPart>();
            var missing = new Dictionary<(SplineBuildingPartKind, int), string>();

            foreach (var (_, partKey, _) in partsToFill)
                if (SplineBuildingUncutParts.TryGet(building, partKey.Key, out var uncutPart))
                    uncutParts[partKey.Key] = uncutPart;
                else
                    missing[(partKey.Kind, partKey.Storey)] = partKey.Key;

            if (missing.Count == 0)
                return uncutParts;

            using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.BuildingGeometry))
                foreach (var built in BuildCopiesOfParts(building, layout, (kind, storey) => missing.ContainsKey((kind, storey))))
                {
                    var key = missing[(built.Kind, built.Storey)];
                    uncutParts[key] = SplineBuildingUncutParts.Store(building, key, built.Geometry);
                }

            return uncutParts;
        }

        // Здание без повтора — части как есть; под повтором — каждая часть со всеми копиями, каждая копия со своими проёмами.
        private static List<SplineBuildingPartGeometry> BuildCopiesOfParts(SplineBuilding building, SplineBuildingPartLayout layout,
            System.Func<SplineBuildingPartKind, int, bool> isPartNeeded)
        {
            var copies = layout.Copies;

            if (copies.Count == 1 && copies[0].LocalOffset == Vector3.zero)
                return SplineBuildingMeshBuilder.BuildParts(building, layout.Paths, copies[0].HolesByPath, copies[0].SlabHoles, isPartNeeded);

            // Части — в том же порядке, что их отдаёт сборка каждой копии; все копии пропущены — части пустые.
            var merged = new List<SplineBuildingPartGeometry>();

            foreach (var (kind, storey) in SplineBuildingMeshBuilder.PartsOf(building))
                if (isPartNeeded(kind, storey))
                    merged.Add(new SplineBuildingPartGeometry(kind, storey));

            foreach (var copy in copies)
            {
                var built = SplineBuildingMeshBuilder.BuildParts(building, layout.Paths, copy.HolesByPath, copy.SlabHoles, isPartNeeded);

                for (var index = 0; index < built.Count; index++)
                    merged[index].Geometry.Append(built[index].Geometry, copy.LocalOffset);
            }

            return merged;
        }

        // Меш, материалы и коллайдер одной части. Общий с копией или префабом файл не перезаписывается — у части будет свой.
        // Черновик — без коллайдера и без подписи; габарит до вырезов пишется и в нём: по нему ищут вырезы части.
        private static void FillPart(SplineBuilding building, SplineBuildingPart part, SplineBuildingPartKey partKey, SplineBuildingPartCheck check,
            SplineBuildingUncutPart uncutPart, SplineBuildingPartLook look, LiveBuilderBuildQuality quality)
        {
            LiveBuilderRebuildProfiler.CountRebuiltBuildingPart();

            var uncutBounds = uncutPart.Bounds;
            List<GeometryCutter> cutters;
            string partSignature;

            using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.SignatureCheck))
            {
                var isCheckedHere = check?.Cutters != null && check.UncutBounds.Equals(uncutBounds);
                cutters = isCheckedHere ? check.Cutters : SplineBuildingPartChecks.CuttersOf(part, uncutBounds);
                partSignature = isCheckedHere
                    ? check.Signature
                    : partKey.Key + SplineBuildingPartKeys.WorldKeyOfPart(part, quality, look.WorldKey)
                                  + GeometryCutterLookup.KeyOfTouchingCopies(part, uncutBounds, cutters);
            }

            var written = LiveBuilderMeshWriter.Write(part, building.name + " " + SplineBuildingParts.NameOf(building, partKey.Kind, partKey.Storey), mesh =>
            {
                using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.MeshFill))
                    uncutPart.Geometry.WriteTo(mesh, look.Texturing);

                return look.Materials;
            }, cutters, quality, partKey.Key);

            var isFinal = quality == LiveBuilderBuildQuality.Final;

            if ((isFinal && part.BuiltSignature != partSignature) || part.UncutBounds != uncutBounds)
            {
                if (isFinal)
                    part.BuiltSignature = partSignature;

                part.UncutBounds = uncutBounds;
                EditorUtility.SetDirty(part);
                LiveBuilderSyncEdits.RecordPrefabInstance(part);
            }

            if (!isFinal)
            {
                LiveBuilderDraftBuilds.Remember(part, partSignature);
                return;
            }

            LiveBuilderDraftBuilds.Forget(part);

            using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.Collider))
                LiveBuilderMeshWriter.UpdateCollider(part, building.HasCollider, written.Mesh);
        }

        // Подпись здания — в сцену только у чистовой сборки, черновик помнится в памяти.
        private static void RememberSignature(SplineBuilding building, string signature, LiveBuilderBuildQuality quality)
        {
            if (LiveBuilderDraftBuilds.IsBuilt(building, building.BuiltSignature, signature, quality))
                return;

            if (quality == LiveBuilderBuildQuality.Draft)
            {
                LiveBuilderDraftBuilds.Remember(building, signature);
                return;
            }

            building.BuiltSignature = signature;
            LiveBuilderDraftBuilds.Forget(building);
            EditorUtility.SetDirty(building);
            LiveBuilderSyncEdits.RecordPrefabInstance(building);
        }


        // ── Подпись ──────────────────────────────────────────────────────

        // Всё здание разом: числа, материалы, точки контура, вырезающие коробки объекта вокруг — для префабов и быстрой сверки. Хеш одинаков на любой машине.
        // Части собираются по своим подписям (<see cref="SplineBuildingPartKeys"/>), вырезы — у каждой части свои.
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

            SplineBuildingPartKeys.AppendTexture(ref hash, building);
            LiveBuilderStableHash.AppendSplines(ref hash, building.GetComponent<SplineContainer>());
            var model = SplineWallRebuilder.OuterModelOf(building);

            // Коробки — уже в осях здания: где стоит само здание в объекте, подписи не важно — здание едет со своей лестницей без пересборки.
            if (model != null)
            {
                var cutBoxes = SplineWallSignature.CutBoxesHashOf(model, building, true);
                hash.Append(ref cutBoxes);
            }

            LiveBuilderArrayCopyPoses.AppendKey(ref hash, building.transform, building.transform);
            return hash.ToString();
        }
    }


    /// <summary>Как выглядят части здания в одной сборке: материалы по частям меша, развёртка и мировой ключ текстуры (чистовая; черновая — null).</summary>
    internal readonly struct SplineBuildingPartLook
    {
        public SplineBuildingPartLook(Material[] materials, SplineWallTexturing texturing, string worldKey)
        {
            Materials = materials;
            Texturing = texturing;
            WorldKey = worldKey;
        }


        public Material[] Materials { get; }
        public SplineWallTexturing Texturing { get; }
        public string WorldKey { get; }
    }
}
