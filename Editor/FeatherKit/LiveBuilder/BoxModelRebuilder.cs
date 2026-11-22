using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Пересобирает меш объектов из коробок, когда изменились коробки; когда проверять — решает очередь <see cref="LiveBuilderRebuildQueue"/>.
    /// - сверяет подпись коробок (<see cref="BoxModelSignature"/>) и вершины меша: лишних пересборок нет, отставший от сцены файл меша догоняется
    /// - чистовая сборка — с фаской рёбер (<see cref="BoxModelBevel"/>) и коллайдером, черновик — без них (<see cref="LiveBuilderBuildQuality"/>)
    /// - общий меш копий и префабов не перезаписывает (<see cref="BoxModelPrefabInstances"/>); поверхности и коробки расчёта — в <see cref="BoxModelBuildResults"/>
    /// - объект без единой коробки (пустая группа, <see cref="LiveBuilderGroups"/>) — без меша и без файла
    /// </summary>
    public static class BoxModelRebuilder
    {
        // Меши, чьи вершины уже сверены с коробками: номер меша → подпись, число вершин и габарит на момент сверки.
        private static readonly Dictionary<EntityId, string> VerifiedMeshes = new();

        // Габарит меша до вырезов, в осях объекта: по нему ищут вырезы, как при записи. По прорезанному мешу нельзя — объект целиком
        // в вырезе пустой, вырез «пропадает», подпись не сходится, и объект пересобирался бы на каждой правке. Помнится до перезагрузки скриптов;
        // есть запись — меш собран в эту сессию, и пустой он законно.
        private static readonly Dictionary<EntityId, Bounds> UncutBoundsByModel = new();


        // Номер правки сцены: поменялся — всё, что посчитано по сцене, устарело (например, что под мышью — <see cref="BoxModelPicker"/>).
        public static int SceneVersion => BoxModelBuildResults.SceneVersion;


        // ── Пересборка ───────────────────────────────────────────────────

        // Пересобирает объекты, у которых меш не совпадает с коробками.
        // Экземпляр префаба без своих правок берёт меш префаба. Чужой меш (копии, префаба) не перезаписывается — это решает запись (<see cref="LiveBuilderMeshWriter"/>).
        // Ошибка в одном объекте не останавливает остальные.
        public static void RebuildChangedModels()
        {
            RebuildChangedModels(LiveBuilderRebuildScope.WholeScene, LiveBuilderBuildQuality.Final);
        }


        public static void RebuildChangedModels(LiveBuilderRebuildScope scope, LiveBuilderBuildQuality quality)
        {
            foreach (var model in scope.Find<BoxModel>())
            {
                try
                {
                    if (TryUsePrefabGeometry(model))
                        continue;

                    BoxModelPieceTidying.Tidy(model);
                    bool isUpToDate;

                    using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.SignatureCheck))
                        isUpToDate = IsMeshUpToDate(model, model.GetComponent<MeshFilter>().sharedMesh, quality);

                    if (!isUpToDate)
                        Build(model, quality);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, model);
                }
            }
        }

        // Файл меша мог отстать от сцены при той же подписи — сверяем и вершины, раз на меш и подпись. Прорезанный или срезанный фаской меш
        // по вершинам не сверить — хватает подписи и непустого меша; пустой законен, если собран в эту сессию (объект целиком в вырезе).
        private static bool IsMeshUpToDate(BoxModel model, Mesh mesh, LiveBuilderBuildQuality quality)
        {
            var result = BoxModelBuildResults.Of(model);

            if (HasNothingToBuild(result))
                return mesh == null;

            var cutters = GeometryCutterLookup.CuttersFor(model, UncutWorldBoundsOf(model));
            var signature = result.Signature + WorldKeyOf(model, result, quality) + GeometryCutterLookup.KeyOf(model, cutters);

            if (!LiveBuilderDraftBuilds.IsBuilt(model, model.BuiltSignature, signature, quality) || mesh == null)
                return false;

            if (model.BuiltSignature != signature || cutters.Count > 0 || IsCutAfterSurfaces(model, result))
                return mesh.vertexCount > 0 || UncutBoundsByModel.ContainsKey(model.GetEntityId());

            var verifiedKey = result.Signature + "|" + mesh.vertexCount + "|" + mesh.bounds;

            if (VerifiedMeshes.TryGetValue(mesh.GetEntityId(), out var key) && key == verifiedKey)
                return true;

            if (!BoxModelMeshBuilder.IsBuiltFrom(mesh, result.Surfaces))
                return false;

            VerifiedMeshes[mesh.GetEntityId()] = verifiedKey;
            return true;
        }

        // Мировой ключ текстуры (<see cref="LiveBuilderTextureSignature"/>): черновик берёт тот, по которому собран, — перенос в жесте не пересобирает.
        private static string WorldKeyOf(BoxModel model, BoxModelBuildResult result, LiveBuilderBuildQuality quality)
        {
            return LiveBuilderTextureSignature.WorldKeyFor(quality, model.BuiltSignature, () => BoxModelSignature.WorldKeyOf(model, result.Boxes));
        }

        // Габарит до вырезов; не помнится (скрипты перезагрузились) — габарит готового меша.
        private static Bounds UncutWorldBoundsOf(BoxModel model)
        {
            return UncutBoundsByModel.TryGetValue(model.GetEntityId(), out var uncutBounds)
                ? GeometryCutterLookup.WorldBoundsOf(uncutBounds, model.transform)
                : GeometryCutterLookup.WorldBoundsOf(model.GetComponent<MeshRenderer>());
        }


        // Экземпляр префаба без своих правок берёт меш префаба (отставший префаб пересобирается) — сам тогда не собирается.
        public static bool TryUsePrefabGeometry(BoxModel model)
        {
            return BoxModelPrefabInstances.TryUsePrefabGeometry(model, () => IsPrefabMeshUpToDate(model), () => PrefabStateOf(model));
        }

        // Меш префаба собран по тем же коробкам, что у экземпляра без правок, и его файл не пустой.
        private static bool IsPrefabMeshUpToDate(BoxModel instance)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            var sourceMesh = source != null ? source.GetComponent<MeshFilter>().sharedMesh : null;
            var result = BoxModelBuildResults.Of(instance);

            // Пустая группа в префабе — меша нет и у него.
            if (HasNothingToBuild(result))
                return source != null && sourceMesh == null;

            // Подпись префаба — без части вырезов: свои вырезы префаба режут его меш, а экземпляр их не считает.
            return source != null && GeometryCutterLookup.WithoutCutterKey(source.BuiltSignature) == result.Signature + BoxModelSignature.WorldKeyOf(instance, result.Boxes)
                   && sourceMesh != null
                   && (IsCutAfterSurfaces(instance, result) ? sourceMesh.vertexCount > 0 : BoxModelMeshBuilder.IsBuiltFrom(sourceMesh, result.Surfaces));
        }

        // Чем сейчас собран префаб — если после пересборки он всё равно не сходится, второй раз его не трогаем.
        private static string PrefabStateOf(BoxModel instance)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            var sourceMesh = source != null ? source.GetComponent<MeshFilter>().sharedMesh : null;
            return (source != null ? source.BuiltSignature : "") + "|" + (sourceMesh != null ? sourceMesh.vertexCount : -1);
        }


        public static void Rebuild(BoxModel model)
        {
            Rebuild(model, LiveBuilderBuildQuality.Final);
        }


        // Черновик — без фаски и коллайдера, подпись в сцене не пишется: её запишет чистовая сборка (<see cref="LiveBuilderDraftBuilds"/>).
        // Зовут и сразу после правки, до того как о ней узнала очередь, — коробки собираются заново.
        public static void Rebuild(BoxModel model, LiveBuilderBuildQuality quality)
        {
            BoxModelBuildResults.Forget(model);
            Build(model, quality);
        }

        // Коробки объекта уже собраны после правки — проход очереди только что сверил по ним подпись.
        private static void Build(BoxModel model, LiveBuilderBuildQuality quality)
        {
            LiveBuilderRebuildProfiler.CountRebuiltConstruction();
            var result = BoxModelBuildResults.Of(model);

            if (HasNothingToBuild(result))
            {
                LiveBuilderDraftBuilds.Forget(model);
                ClearEmptyGroup(model, result.Signature);
                return;
            }

            var isFinal = quality == LiveBuilderBuildQuality.Final;
            var written = LiveBuilderMeshWriter.Write(model, null, mesh =>
            {
                var materials = FillWithBevel(mesh, model, result, isFinal);
                UncutBoundsByModel[model.GetEntityId()] = mesh.bounds;
                return materials;
            }, null, quality, result.Signature);
            var signature = result.Signature + WorldKeyOf(model, result, quality) + GeometryCutterLookup.KeyOf(model, written.Cutters);

            if (isFinal)
                FinishBuild(model, result, written, signature);
            else
                LiveBuilderDraftBuilds.Remember(model, signature);

            // Вырезы объекта режут его стены и здания — им тоже пора свериться.
            LiveBuilderRebuildQueue.RequestCheck(model.gameObject);
        }

        // Чистовая сборка: коллайдер и подпись в сцене.
        private static void FinishBuild(BoxModel model, BoxModelBuildResult result, LiveBuilderMeshWriteResult written, string signature)
        {
            var isSceneChanged = written.IsNewMesh || model.BuiltSignature != signature;

            using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.Collider))
                LiveBuilderMeshWriter.UpdateCollider(model, model.HasCollider, model.HasCollider ? ColliderMeshOf(written.Mesh, result.Ramps) : null);

            model.BuiltSignature = signature;
            LiveBuilderDraftBuilds.Forget(model);

            // Меш только догнал сцену — сцена та же, звёздочку «не сохранено» не ставим.
            if (isSceneChanged)
            {
                EditorUtility.SetDirty(model);
                LiveBuilderSyncEdits.RecordPrefabInstance(model);
            }
        }

        // Объект без единой коробки — группа, в которой пока только другие объекты: файла меша нет, меша и коллайдера тоже.
        // Появятся коробки — заведётся свой файл, как у нового объекта.
        private static void ClearEmptyGroup(BoxModel model, string signature)
        {
            var filter = model.GetComponent<MeshFilter>();
            var isSceneChanged = filter.sharedMesh != null || model.BuiltSignature != signature;

            if (filter.sharedMesh != null)
            {
                filter.sharedMesh = null;
                LiveBuilderSyncEdits.RecordPrefabInstance(filter);
            }

            LiveBuilderMeshWriter.UpdateCollider(model, model.HasCollider, null);
            model.BuiltSignature = signature;

            if (isSceneChanged)
            {
                EditorUtility.SetDirty(filter);
                EditorUtility.SetDirty(model);
                LiveBuilderSyncEdits.RecordPrefabInstance(model);
            }
        }


        // Меш из наружных поверхностей, рёбра срезаны фаской, если она задана и сборка чистовая, потом — скосы рёбер коробок:
        // без них черновик пандуса пропал бы. Вырезы режут потом — уже готовое.
        private static Material[] FillWithBevel(Mesh mesh, BoxModel model, BoxModelBuildResult result, bool hasBevel)
        {
            Material[] materials;

            using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.MeshFill))
                materials = BoxModelMeshBuilder.Fill(mesh, result.Surfaces, result.Boxes, model.DefaultMaterial,
                    LiveBuilderTextureSpace.Of(model.transform, model.TextureAlignment));

            var unwrapper = LiveBuilderFaceUnwrapper.ForModel(model, materials);

            if (hasBevel)
                using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.Bevel))
                    BoxModelBevel.Apply(mesh, result.Surfaces, model.BevelSize, model.BevelSegments, unwrapper);

            using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.Chamfer))
                BoxModelChamferCutter.Apply(mesh, result.Boxes, unwrapper);

            return materials;
        }

        // Меш для столкновений: сам меш, а у объекта с лестницами — меш со скатами, вторым мешем в том же файле.
        private static Mesh ColliderMeshOf(Mesh mesh, List<BoxModelRampShape> ramps)
        {
            if (ramps.Count == 0)
            {
                BoxModelMeshStorage.RemoveColliderMesh(mesh);
                return mesh;
            }

            var colliderMesh = BoxModelMeshStorage.GetOrCreateColliderMesh(mesh);
            BoxModelMeshBuilder.FillCollider(colliderMesh, mesh, ramps);
            EditorUtility.SetDirty(colliderMesh);
            return colliderMesh;
        }


        // ── Поверхности и коробки расчёта ────────────────────────────────

        // Наружные поверхности объекта в его осях, миллиметры; посчитаны заново, если коробки поменялись.
        public static IReadOnlyList<BoxModelSurface> GetSurfaces(BoxModel model)
        {
            return BoxModelBuildResults.Of(model).Surfaces;
        }


        // Коробки расчёта по порядку — номер поверхности (SourcePart) указывает сюда.
        public static IReadOnlyList<BoxModelSolverBox> GetSolverBoxes(BoxModel model)
        {
            return BoxModelBuildResults.Of(model).Boxes;
        }


        // Габарит наружных поверхностей объекта в его осях — отсев по лучу до перебора поверхностей.
        public static Bounds GetSurfaceBounds(BoxModel model)
        {
            return BoxModelBuildResults.Of(model).SurfaceBounds;
        }


        // Сцена поменялась — все результаты пересчитать при следующем вопросе. Зовёт очередь пересборки на любую правку.
        public static void ForgetCachedResults()
        {
            BoxModelBuildResults.ForgetAll();
        }


        // Только объекты в области: черновой проход очереди после сверок, которые правят числа без событий редактора.
        public static void ForgetCachedResults(LiveBuilderRebuildScope scope)
        {
            if (scope.IsWholeScene)
            {
                ForgetCachedResults();
                return;
            }

            foreach (var model in scope.Find<BoxModel>())
                BoxModelBuildResults.Forget(model);
        }


        // Объект или меш удалён, сцена закрыта — их сверки и габариты больше не нужны.
        public static void ForgetDestroyed()
        {
            LiveBuilderDestroyedObjects.RemoveFrom(VerifiedMeshes);
            LiveBuilderDestroyedObjects.RemoveFrom(UncutBoundsByModel);
        }


        // ── Предпросмотр ─────────────────────────────────────────────────

        // Заливает в меш предпросмотра объект так, будто в конце списка стоит ещё одна коробка. В файл не пишется.
        // Объекта нет — меш из одной этой коробки. Возвращает материалы по частям меша.
        public static Material[] FillPreviewMesh(Mesh mesh, BoxModel model, MillimeterBox extraBox, BoxModelPartMode extraMode)
        {
            return FillPreviewMesh(mesh, model, new[] { new BoxModelSolverBox(extraBox, extraMode, null, default) });
        }


        // То же для нескольких коробок сразу — комната со стенами, лестница.
        public static Material[] FillPreviewMesh(Mesh mesh, BoxModel model, IReadOnlyList<BoxModelSolverBox> extraBoxes)
        {
            var boxes = model != null ? new List<BoxModelSolverBox>(GetSolverBoxes(model)) : new List<BoxModelSolverBox>();
            boxes.AddRange(extraBoxes);

            var defaultMaterial = model != null ? model.DefaultMaterial : BoxModelFactory.LoadDefaultMaterial();
            var surfaces = model != null
                ? BoxModelBuildResults.SurfacesWithoutStoring(model, boxes)
                : BoxModelSurfaceFinder.FindOuterSurfaces(BoxModelSolver.Solve(boxes.ConvertAll(box => box.Shape)));
            var textureSpace = model != null ? LiveBuilderTextureSpace.Of(model.transform, model.TextureAlignment) : default;
            return BoxModelMeshBuilder.Fill(mesh, surfaces, boxes, defaultMaterial, textureSpace);
        }


        // ── Коробки объекта ──────────────────────────────────────────────

        // Включённые коробки объекта по порядку в иерархии, без повторов; коробки вложенного объекта и стен — их, не наши. Интерьер зданий — наш.
        public static List<BoxModelPart> CollectParts(BoxModel model)
        {
            var parts = new List<BoxModelPart>();

            foreach (var part in model.GetComponentsInChildren<BoxModelPart>(false))
                if (part.gameObject != model.gameObject && part.GetComponentInParent<BoxModel>() == model && part.GetComponentInParent<SplineWall>() == null)
                    parts.Add(part);

            return parts;
        }


        // Ни одной коробки или одни вырез-коробки (группа с проёмом): меша нет, как у пустой группы.
        private static bool HasNothingToBuild(BoxModelBuildResult result)
        {
            return result.Boxes.TrueForAll(box => box.Owner is GeometryCutter);
        }


        // Меш срезан после поверхностей — фаской или скосами рёбер: по вершинам его с коробками уже не сверить.
        private static bool IsCutAfterSurfaces(BoxModel model, BoxModelBuildResult result)
        {
            return BoxModelBevel.HasBevel(model) || result.Boxes.Exists(box => box.Chamfers.Count > 0);
        }

    }
}
