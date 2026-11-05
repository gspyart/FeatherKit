using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Пересобирает меш объектов из коробок, когда изменились коробки: правка, перенос, порядок в списке, повторы, лестницы, Ctrl+Z.
    /// Когда проверять — решает общая очередь <see cref="LiveBuilderRebuildQueue"/>.
    /// Сравнивает «подпись» коробок расчёта (<see cref="BoxModelSignature"/>) с той, по которой собран меш, и число вершин меша — лишних пересборок нет,
    /// а отставший от сцены файл меша догоняется при открытии сцены. Хранит наружные поверхности и коробки расчёта для выбора кликом.
    /// Фаска рёбер (<see cref="BoxModelBevel"/>) — часть сборки: после поверхностей, до вырезов.
    /// Общий меш копий и префабов не перезаписывает — экземпляры префабов разбирает <see cref="BoxModelPrefabInstances"/>.
    /// Объект без единой коробки (пустая группа, <see cref="LiveBuilderGroups"/>) — без меша и без файла.
    /// </summary>
    public static class BoxModelRebuilder
    {
        private static readonly Dictionary<EntityId, BoxModelBuildResult> BuildResultsByModel = new();

        // При каком номере правки посчитан результат объекта. Пока номер тот же — сцена не менялась, считать заново незачем:
        // результат спрашивают на каждую отрисовку, наведение мыши и каждую стену объекта.
        private static readonly Dictionary<EntityId, int> BuildVersionsByModel = new();
        private static int sceneVersion;

        // Меши, чьи вершины уже сверены с коробками: номер меша → подпись, число вершин и габарит на момент сверки.
        private static readonly Dictionary<EntityId, string> VerifiedMeshes = new();

        // Габарит меша до вырезов, в осях объекта: по нему ищут вырезы, как при записи. По прорезанному мешу нельзя — объект целиком
        // в вырезе пустой, вырез «пропадает», подпись не сходится, и объект пересобирался бы на каждой правке. Помнится до перезагрузки скриптов;
        // есть запись — меш собран в эту сессию, и пустой он законно.
        private static readonly Dictionary<EntityId, Bounds> UncutBoundsByModel = new();


        // ── Пересборка ───────────────────────────────────────────────────

        // Пересобирает объекты, у которых меш не совпадает с коробками.
        // Экземпляр префаба без своих правок берёт меш префаба. Чужой меш (копии, префаба) не перезаписывается — это решает запись (<see cref="LiveBuilderMeshWriter"/>).
        // Ошибка в одном объекте не останавливает остальные.
        public static void RebuildChangedModels()
        {
            foreach (var model in FindModels())
            {
                try
                {
                    if (TryUsePrefabGeometry(model))
                        continue;

                    BoxModelPieceTidying.Tidy(model);

                    if (!IsMeshUpToDate(model, model.GetComponent<MeshFilter>().sharedMesh))
                        Rebuild(model);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, model);
                }
            }
        }

        // Меш собран по этим коробкам. Одной подписи мало: файл меша может отстать от сцены (не сохранился, откатили в git) —
        // подпись совпадает, а меш пустой или старый. Поэтому сверяем ещё и сами вершины.
        // Сверка вершин — один раз на меш и подпись: дальше хватает числа вершин и габарита, иначе каждая правка в сцене перебирала бы все меши.
        // Прорезанный вырезами или со срезанными рёбрами меш с коробками по вершинам уже не сверить — хватает подписи и непустого меша.
        // Пустой — тоже готовый, если его собрали в эту сессию: объект целиком в вырезе.
        // Пустая группа — без коробок совсем — готова, когда меша нет вовсе: файла ей не заводим.
        private static bool IsMeshUpToDate(BoxModel model, Mesh mesh)
        {
            var result = GetBuildResult(model);

            if (result.Boxes.Count == 0)
                return mesh == null;

            var cutters = GeometryCutterLookup.CuttersFor(model, UncutWorldBoundsOf(model));
            var signature = result.Signature + GeometryCutterLookup.KeyOf(model, cutters);

            if (model.BuiltSignature != signature || mesh == null)
                return false;

            if (cutters.Count > 0 || IsCutAfterSurfaces(model, result))
                return mesh.vertexCount > 0 || UncutBoundsByModel.ContainsKey(model.GetEntityId());

            var verifiedKey = result.Signature + "|" + mesh.vertexCount + "|" + mesh.bounds;

            if (VerifiedMeshes.TryGetValue(mesh.GetEntityId(), out var key) && key == verifiedKey)
                return true;

            if (!BoxModelMeshBuilder.IsBuiltFrom(mesh, result.Surfaces))
                return false;

            VerifiedMeshes[mesh.GetEntityId()] = verifiedKey;
            return true;
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
            var result = GetBuildResult(instance);

            // Пустая группа в префабе — меша нет и у него.
            if (result.Boxes.Count == 0)
                return source != null && sourceMesh == null;

            // Подпись префаба — без части вырезов: свои вырезы префаба режут его меш, а экземпляр их не считает.
            return source != null && GeometryCutterLookup.WithoutCutterKey(source.BuiltSignature) == result.Signature && sourceMesh != null
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
            // Зовут и сразу после правки, до того как о ней узнала очередь, — считаем заново.
            BuildVersionsByModel.Remove(model.GetEntityId());
            var result = GetBuildResult(model);

            if (result.Boxes.Count == 0)
            {
                ClearEmptyGroup(model, result.Signature);
                return;
            }

            var written = LiveBuilderMeshWriter.Write(model, null, mesh =>
            {
                var materials = FillWithBevel(mesh, model, result);
                UncutBoundsByModel[model.GetEntityId()] = mesh.bounds;
                return materials;
            });
            var signature = result.Signature + GeometryCutterLookup.KeyOf(model, written.Cutters);
            var isSceneChanged = written.IsNewMesh || model.BuiltSignature != signature;

            LiveBuilderMeshWriter.UpdateCollider(model, model.HasCollider, model.HasCollider ? ColliderMeshOf(written.Mesh, result.Ramps) : null);
            model.BuiltSignature = signature;

            // Меш только догнал сцену — сцена та же, звёздочку «не сохранено» не ставим.
            if (isSceneChanged)
            {
                EditorUtility.SetDirty(model);
                LiveBuilderSyncEdits.RecordPrefabInstance(model);
            }

            // Вырезы объекта режут его стены и здания — им тоже пора свериться.
            LiveBuilderRebuildQueue.RequestCheck();
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


        // Меш из наружных поверхностей, рёбра срезаны фаской, если она задана, потом — скосы рёбер коробок. Вырезы режут потом — уже готовое.
        private static Material[] FillWithBevel(Mesh mesh, BoxModel model, BoxModelBuildResult result)
        {
            var materials = BoxModelMeshBuilder.Fill(mesh, result.Surfaces, result.Boxes, model.DefaultMaterial);
            BoxModelBevel.Apply(mesh, result.Surfaces, model.BevelSize, model.BevelSegments);
            BoxModelChamferCutter.Apply(mesh, result.Boxes);
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
            return GetBuildResult(model).Surfaces;
        }


        // Коробки расчёта по порядку — номер поверхности (SourcePart) указывает сюда.
        public static IReadOnlyList<BoxModelSolverBox> GetSolverBoxes(BoxModel model)
        {
            return GetBuildResult(model).Boxes;
        }


        // Сцена поменялась — все результаты пересчитать при следующем вопросе. Зовёт очередь пересборки на любую правку.
        public static void ForgetCachedResults()
        {
            sceneVersion++;
        }


        // Всё, что считается из коробок объекта. С прошлой правки сцены — из памяти; та же подпись — те же поверхности,
        // но коробки — новые: их хозяева могли смениться (удалил коробку и нарисовал такую же), а по хозяину выделяют кликом.
        private static BoxModelBuildResult GetBuildResult(BoxModel model)
        {
            var modelId = model.GetEntityId();
            var hasCached = BuildResultsByModel.TryGetValue(modelId, out var cached);

            if (hasCached && BuildVersionsByModel.TryGetValue(modelId, out var version) && version == sceneVersion)
                return cached;

            var ramps = new List<BoxModelRampShape>();
            var boxes = BoxModelShapeCollector.Collect(model, ramps);
            var signature = BoxModelSignature.Of(model, boxes, ramps);

            var surfaces = hasCached && cached.Signature == signature
                ? cached.Surfaces
                : BoxModelSurfaceFinder.FindOuterSurfaces(BoxModelSolver.Solve(boxes.ConvertAll(box => box.Shape)));

            var result = new BoxModelBuildResult(signature, boxes, ramps, surfaces);
            BuildResultsByModel[modelId] = result;
            BuildVersionsByModel[modelId] = sceneVersion;
            return result;
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
            var surfaces = BoxModelSurfaceFinder.FindOuterSurfaces(BoxModelSolver.Solve(boxes.ConvertAll(box => box.Shape)));
            return BoxModelMeshBuilder.Fill(mesh, surfaces, boxes, defaultMaterial);
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


        // Меш срезан после поверхностей — фаской или скосами рёбер: по вершинам его с коробками уже не сверить.
        private static bool IsCutAfterSurfaces(BoxModel model, BoxModelBuildResult result)
        {
            return BoxModelBevel.HasBevel(model) || result.Boxes.Exists(box => box.Chamfers.Count > 0);
        }


        private static BoxModel[] FindModels()
        {
            return StageUtility.GetCurrentStageHandle().FindComponentsOfType<BoxModel>();
        }
    }


    /// <summary>Что посчитано из коробок объекта: подпись, коробки расчёта, скаты лестниц и наружные поверхности.</summary>
    public sealed class BoxModelBuildResult
    {
        public BoxModelBuildResult(string signature, List<BoxModelSolverBox> boxes, List<BoxModelRampShape> ramps, List<BoxModelSurface> surfaces)
        {
            Signature = signature;
            Boxes = boxes;
            Ramps = ramps;
            Surfaces = surfaces;
        }


        public string Signature { get; }
        public List<BoxModelSolverBox> Boxes { get; }
        public List<BoxModelRampShape> Ramps { get; }
        public List<BoxModelSurface> Surfaces { get; }
    }
}
