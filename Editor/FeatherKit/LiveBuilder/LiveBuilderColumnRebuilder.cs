using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Пересобирает меш колонн (<see cref="LiveBuilderColumn"/>) — свой шаг очереди (<see cref="LiveBuilderRebuildQueue"/>) после объектов из коробок.
    /// - по подписи, как у остальных построек: без лишних пересборок; экземпляр префаба без правок берёт меш префаба
    /// - запись — общим концом построек (<see cref="LiveBuilderMeshWriter"/>), но без вырезов: колонна не режется
    /// - копии повторов над колонной — в её меше (<see cref="LiveBuilderArrayCopyPoses"/>)
    /// </summary>
    public static class LiveBuilderColumnRebuilder
    {
        // Версия расчёта колонны: поменялась — все колонны пересобираются.
        private const int GeometryVersion = 1;

        // Колонну вырезы не режут: пустой список — запись их и не ищет.
        private static readonly List<GeometryCutter> NoCutters = new();


        // ── Пересборка ───────────────────────────────────────────────────

        public static void RebuildChangedColumns()
        {
            RebuildChangedColumns(LiveBuilderRebuildScope.WholeScene, LiveBuilderBuildQuality.Final);
        }


        public static void RebuildChangedColumns(LiveBuilderRebuildScope scope, LiveBuilderBuildQuality quality)
        {
            foreach (var column in scope.Find<LiveBuilderColumn>())
            {
                try
                {
                    if (TryUsePrefabGeometry(column))
                        continue;

                    bool isUpToDate;

                    using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.SignatureCheck))
                        isUpToDate = column.GetComponent<MeshFilter>().sharedMesh != null
                                     && LiveBuilderDraftBuilds.IsBuilt(column, column.BuiltSignature, SignatureOf(column, quality), quality);

                    if (!isUpToDate)
                        Rebuild(column, quality);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, column);
                }
            }
        }


        // Экземпляр префаба без своих правок берёт меш префаба (отставший префаб пересобирается) — сам тогда не собирается.
        public static bool TryUsePrefabGeometry(LiveBuilderColumn column)
        {
            return BoxModelPrefabInstances.TryUsePrefabGeometry(column, () => IsPrefabMeshUpToDate(column), () => PrefabStateOf(column));
        }

        private static bool IsPrefabMeshUpToDate(LiveBuilderColumn instance)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            var sourceMesh = source != null ? source.GetComponent<MeshFilter>().sharedMesh : null;
            return sourceMesh != null && sourceMesh.vertexCount > 0 && source.BuiltSignature == SignatureOf(instance, LiveBuilderBuildQuality.Final);
        }

        private static string PrefabStateOf(LiveBuilderColumn instance)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            var sourceMesh = source != null ? source.GetComponent<MeshFilter>().sharedMesh : null;
            return (source != null ? source.BuiltSignature : "") + "|" + (sourceMesh != null ? sourceMesh.vertexCount : -1);
        }


        public static void Rebuild(LiveBuilderColumn column)
        {
            Rebuild(column, LiveBuilderBuildQuality.Final);
        }


        // Черновик — без коллайдера, подпись в сцене не пишется (<see cref="LiveBuilderDraftBuilds"/>).
        public static void Rebuild(LiveBuilderColumn column, LiveBuilderBuildQuality quality)
        {
            LiveBuilderRebuildProfiler.CountRebuiltConstruction();

            var written = LiveBuilderMeshWriter.Write(column, null, mesh =>
            {
                using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.MeshFill))
                    return LiveBuilderColumnMeshBuilder.Fill(mesh, column, LocalCopiesOf(column), BoxModelFactory.LoadDefaultMaterial(),
                        LiveBuilderTextureSpace.Of(column.transform, column.TextureAlignment));
            }, NoCutters, quality);

            var signature = SignatureOf(column, quality);

            if (quality == LiveBuilderBuildQuality.Draft)
            {
                LiveBuilderDraftBuilds.Remember(column, signature);
                return;
            }

            var isSceneChanged = written.IsNewMesh || column.BuiltSignature != signature;

            using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.Collider))
                LiveBuilderMeshWriter.UpdateCollider(column, column.HasCollider, written.Mesh);

            column.BuiltSignature = signature;
            LiveBuilderDraftBuilds.Forget(column);

            if (isSceneChanged)
            {
                EditorUtility.SetDirty(column);
                LiveBuilderSyncEdits.RecordPrefabInstance(column);
            }
        }

        // Сдвиги копий повторов над колонной — в её осях; без повторов — одна, сама колонна.
        private static List<Vector3> LocalCopiesOf(LiveBuilderColumn column)
        {
            var copies = new List<Vector3>();

            foreach (var offset in LiveBuilderArrayCopyPoses.CopiesOf(column.transform))
                copies.Add(LiveBuilderArrayCopyPoses.LocalOffsetOf(column.transform, offset));

            return copies;
        }


        // ── Подпись ──────────────────────────────────────────────────────

        // Всё, от чего зависит меш колонны, — хешем, одинаковым на любом запуске (<see cref="LiveBuilderStableHash"/>); в конце — мировой ключ текстуры.
        public static string SignatureOf(LiveBuilderColumn column, LiveBuilderBuildQuality quality)
        {
            var hash = new Hash128();
            hash.Append(GeometryVersion);
            hash.Append(column.Sides);
            var size = column.Size;
            hash.Append(ref size);
            hash.Append(column.BevelSize);
            hash.Append(column.BevelSegments);
            hash.Append(column.HasCollider ? 1 : 0);
            hash.Append(BoxModelSideMaterials.KeyOf(column.Material));
            hash.Append(BoxModelSideMaterials.KeyOf(column.TopMaterial));
            hash.Append(BoxModelSideMaterials.KeyOf(column.BottomMaterial));
            LiveBuilderTextureSignature.AppendAlignment(ref hash, column.TextureAlignment);
            LiveBuilderTextureSignature.AppendFits(ref hash, column.TextureFits);

            foreach (var material in MaterialsOf(column))
                LiveBuilderTextureSignature.AppendTile(ref hash, material);

            LiveBuilderArrayCopyPoses.AppendKey(ref hash, column.transform, column.transform);
            var worldKey = LiveBuilderTextureSignature.WorldKeyFor(quality, column.BuiltSignature, () => WorldKeyOf(column));
            return "c" + hash + worldKey;
        }

        // Мировой ключ (World): сдвиги осей колонны по модулю периодов рисунка; Object — пусто.
        private static string WorldKeyOf(LiveBuilderColumn column)
        {
            var space = LiveBuilderTextureSpace.Of(column.transform, column.TextureAlignment);

            if (!space.IsWorld)
                return "";

            var periods = new LiveBuilderTexturePeriods();

            foreach (var material in MaterialsOf(column))
                SplineWallSignature.AddPeriods(periods, column.TextureFits, material);

            return LiveBuilderTextureSignature.WorldKeyOf(space, periods, LiveBuilderTextureSignature.AxisShiftsOf(space));
        }

        // Видимые материалы бока, верха и низа.
        private static Material[] MaterialsOf(LiveBuilderColumn column)
        {
            var side = column.Material != null ? column.Material : BoxModelFactory.LoadDefaultMaterial();
            return new[] { side, column.TopMaterial != null ? column.TopMaterial : side, column.BottomMaterial != null ? column.BottomMaterial : side };
        }
    }
}
