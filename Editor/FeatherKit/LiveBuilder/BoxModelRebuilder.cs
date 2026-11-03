using System.Collections.Generic;
using System.Text;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Пересобирает меш объектов из коробок, когда изменились коробки: правка, перенос, порядок в списке, повторы, лестницы, Ctrl+Z.
    /// Когда проверять — решает общая очередь <see cref="LiveBuilderRebuildQueue"/>.
    /// Сравнивает «подпись» коробок расчёта с той, по которой собран меш, и число вершин меша — лишних пересборок нет,
    /// а отставший от сцены файл меша догоняется при открытии сцены. Хранит наружные поверхности и коробки расчёта для выбора кликом.
    /// Фаска рёбер (<see cref="BoxModelBevel"/>) — часть сборки: после поверхностей, до вырезов.
    /// Общий меш копий и префабов не перезаписывает — экземпляры префабов разбирает <see cref="BoxModelPrefabInstances"/>.
    /// </summary>
    [InitializeOnLoad]
    public static class BoxModelRebuilder
    {
        public const string AddPartName = "Add Box";
        public const string CutPartName = "Cut Box";

        private const string EditorOnlyTag = "EditorOnly";

        private static readonly Dictionary<EntityId, BoxModelBuildResult> BuildResultsByModel = new();

        // При каком номере правки посчитан результат объекта. Пока номер тот же — сцена не менялась, считать заново незачем:
        // результат спрашивают на каждую отрисовку, наведение мыши и каждую стену объекта.
        private static readonly Dictionary<EntityId, int> BuildVersionsByModel = new();
        private static int sceneVersion;

        // Меши, чьи вершины уже сверены с коробками: номер меша → подпись, число вершин и габарит на момент сверки.
        private static readonly Dictionary<EntityId, string> VerifiedMeshes = new();


        static BoxModelRebuilder()
        {
            EditorSceneManager.sceneSaving += OnSceneSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;
        }


        // ── Пересборка ───────────────────────────────────────────────────

        // Пересобирает объекты, у которых меш не совпадает с коробками.
        // Экземпляр префаба без своих правок берёт меш префаба. Меш, который делят копии или префаб, не перезаписывается:
        // изменённая копия получает свой файл, остальные остаются как были.
        public static void RebuildChangedModels()
        {
            var models = FindModels();
            var meshUserCounts = LiveBuilderMeshWriter.CountMeshUsers(models);

            foreach (var model in models)
            {
                if (BoxModelPrefabInstances.TryUsePrefabGeometry(model, () => IsPrefabMeshUpToDate(model), () => PrefabStateOf(model)))
                    continue;

                TidyPieces(model);

                if (IsMeshUpToDate(model, model.GetComponent<MeshFilter>().sharedMesh))
                    continue;

                LiveBuilderMeshWriter.ReleaseIfShared(model, meshUserCounts);
                Rebuild(model);
            }
        }

        // Меш префаба собран по тем же коробкам, что у экземпляра без правок, и его файл не пустой.
        private static bool IsPrefabMeshUpToDate(BoxModel instance)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            var sourceMesh = source != null ? source.GetComponent<MeshFilter>().sharedMesh : null;
            var result = GetBuildResult(instance);

            return source != null && source.BuiltSignature == result.Signature && sourceMesh != null
                   && (HasBevel(instance) ? sourceMesh.vertexCount > 0 : BoxModelMeshBuilder.IsBuiltFrom(sourceMesh, result.Surfaces));
        }

        // Чем сейчас собран префаб — если после пересборки он всё равно не сходится, второй раз его не трогаем.
        private static string PrefabStateOf(BoxModel instance)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            var sourceMesh = source != null ? source.GetComponent<MeshFilter>().sharedMesh : null;
            return (source != null ? source.BuiltSignature : "") + "|" + (sourceMesh != null ? sourceMesh.vertexCount : -1);
        }

        // Меш собран по этим коробкам. Одной подписи мало: файл меша может отстать от сцены (не сохранился, откатили в git) —
        // подпись совпадает, а меш пустой или старый. Поэтому сверяем ещё и сами вершины.
        // Сверка вершин — один раз на меш и подпись: дальше хватает числа вершин и габарита, иначе каждая правка в сцене перебирала бы все меши.
        // Прорезанный вырезами или со срезанными рёбрами меш с коробками по вершинам уже не сверить — хватает подписи и непустого меша.
        private static bool IsMeshUpToDate(BoxModel model, Mesh mesh)
        {
            var result = GetBuildResult(model);
            var cutters = GeometryCutterLookup.CuttersFor(model, GeometryCutterLookup.WorldBoundsOf(model.GetComponent<MeshRenderer>()));
            var signature = result.Signature + GeometryCutterLookup.KeyOf(model, cutters);

            if (model.BuiltSignature != signature || mesh == null)
                return false;

            if (cutters.Count > 0 || HasBevel(model))
                return mesh.vertexCount > 0;

            var verifiedKey = result.Signature + "|" + mesh.vertexCount + "|" + mesh.bounds;

            if (VerifiedMeshes.TryGetValue(mesh.GetEntityId(), out var key) && key == verifiedKey)
                return true;

            if (!BoxModelMeshBuilder.IsBuiltFrom(mesh, result.Surfaces))
                return false;

            VerifiedMeshes[mesh.GetEntityId()] = verifiedKey;
            return true;
        }


        public static void Rebuild(BoxModel model)
        {
            // Зовут и сразу после правки, до того как о ней узнала очередь, — считаем заново.
            BuildVersionsByModel.Remove(model.GetEntityId());
            var result = GetBuildResult(model);
            var written = LiveBuilderMeshWriter.Write(model, null, mesh => FillWithBevel(mesh, model, result));
            var signature = result.Signature + GeometryCutterLookup.KeyOf(model, written.Cutters);
            var isSceneChanged = written.IsNewMesh || model.BuiltSignature != signature;

            LiveBuilderMeshWriter.UpdateCollider(model, model.HasCollider, model.HasCollider ? ColliderMeshOf(written.Mesh, result.Ramps) : null);
            model.BuiltSignature = signature;

            // Меш только догнал сцену — сцена та же, звёздочку «не сохранено» не ставим.
            if (isSceneChanged)
                EditorUtility.SetDirty(model);

            // Вырезы объекта режут его стены и здания — им тоже пора свериться.
            LiveBuilderRebuildQueue.RequestCheck();
        }

        // Меш из наружных поверхностей, рёбра срезаны фаской, если она задана. Вырезы режут потом — уже готовую фаску.
        private static Material[] FillWithBevel(Mesh mesh, BoxModel model, BoxModelBuildResult result)
        {
            var materials = BoxModelMeshBuilder.Fill(mesh, result.Surfaces, result.Boxes, model.DefaultMaterial);
            BoxModelBevel.Apply(mesh, result.Surfaces, model.BevelSize, model.BevelSegments);
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
            var signature = SignatureOf(model, boxes, ramps);

            var surfaces = hasCached && cached.Signature == signature
                ? cached.Surfaces
                : BoxModelSurfaceFinder.FindOuterSurfaces(BoxModelSolver.Solve(boxes.ConvertAll(box => box.Shape)));

            var result = new BoxModelBuildResult(signature, boxes, ramps, surfaces);
            BuildResultsByModel[modelId] = result;
            BuildVersionsByModel[modelId] = sceneVersion;
            return result;
        }


        // ── Предпросмотр ─────────────────────────────────────────────────

        // Меш объекта так, будто в конце списка стоит ещё одна коробка. В файл не пишется — вызывающий сам его уничтожает.
        // Объекта нет — меш из одной этой коробки.
        public static Mesh BuildPreviewMesh(BoxModel model, MillimeterBox extraBox, BoxModelPartMode extraMode, out Material[] materials)
        {
            return BuildPreviewMesh(model, new[] { new BoxModelSolverBox(extraBox, extraMode, null, default) }, out materials);
        }


        // То же для нескольких коробок сразу — комната со стенами, лестница.
        public static Mesh BuildPreviewMesh(BoxModel model, IReadOnlyList<BoxModelSolverBox> extraBoxes, out Material[] materials)
        {
            var boxes = model != null ? new List<BoxModelSolverBox>(GetSolverBoxes(model)) : new List<BoxModelSolverBox>();
            boxes.AddRange(extraBoxes);

            var defaultMaterial = model != null ? model.DefaultMaterial : BoxModelFactory.LoadDefaultMaterial();
            var surfaces = BoxModelSurfaceFinder.FindOuterSurfaces(BoxModelSolver.Solve(boxes.ConvertAll(box => box.Shape)));
            return BoxModelMeshBuilder.Build("Box Model Preview", surfaces, boxes, defaultMaterial, out materials);
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


        // Угол коробки с наименьшими координатами в осях объекта, м.
        public static Vector3 LocalMinOf(BoxModel model, BoxModelPart part)
        {
            return model.transform.InverseTransformPoint(part.transform.position);
        }


        // Коробки, комнаты, лестницы и группы повтора не поворачиваются и не масштабируются; всё, кроме групп, в сборку не попадает.
        private static void TidyPieces(BoxModel model)
        {
            foreach (var part in CollectParts(model))
            {
                TidyTransform(part.transform);
                MarkEditorOnly(part.gameObject);
                RenameByMode(part);
            }

            foreach (var stairs in model.GetComponentsInChildren<BoxModelStairs>(false))
            {
                TidyTransform(stairs.transform);
                MarkEditorOnly(stairs.gameObject);
            }

            foreach (var room in model.GetComponentsInChildren<BoxModelRoom>(false))
            {
                TidyTransform(room.transform);
                MarkEditorOnly(room.gameObject);
            }

            foreach (var repeat in model.GetComponentsInChildren<BoxModelRepeat>(false))
                TidyTransform(repeat.transform);
        }

        private static void TidyTransform(Transform pieceTransform)
        {
            if (pieceTransform.localRotation != Quaternion.identity)
                pieceTransform.localRotation = Quaternion.identity;

            if (pieceTransform.localScale != Vector3.one)
                pieceTransform.localScale = Vector3.one;
        }

        private static void MarkEditorOnly(GameObject piece)
        {
            if (!piece.CompareTag(EditorOnlyTag))
                piece.tag = EditorOnlyTag;
        }

        // Имя по умолчанию следует за режимом: «Add Box (1)», переключённая на вырезание, становится «Cut Box (1)». Свои имена не трогаем.
        private static void RenameByMode(BoxModelPart part)
        {
            var wrongPrefix = part.Mode == BoxModelPartMode.Add ? CutPartName : AddPartName;
            var rightPrefix = part.Mode == BoxModelPartMode.Add ? AddPartName : CutPartName;

            if (part.name.StartsWith(wrongPrefix))
                part.name = rightPrefix + part.name.Substring(wrongPrefix.Length);
        }


        // Всё, от чего зависит меш: коробки расчёта по порядку, их место, режим и материалы, скаты, материал объекта, фаска.
        private static string SignatureOf(BoxModel model, List<BoxModelSolverBox> boxes, List<BoxModelRampShape> ramps)
        {
            var signature = new StringBuilder();
            signature.Append(BoxModelSideMaterials.KeyOf(model.DefaultMaterial)).Append(model.HasCollider ? "c" : "n");

            foreach (var box in boxes)
                signature.Append('|').Append((int)box.Mode)
                    .Append(':').Append(box.Box.Min.ToString()).Append(box.Box.Max.ToString())
                    .Append(':').Append(box.Materials.Key);

            foreach (var ramp in ramps)
                signature.Append("|r").Append(MillimeterBox.ToMillimeters(ramp.Corners[0]).ToString())
                    .Append(MillimeterBox.ToMillimeters(ramp.Corners[6]).ToString());

            // Без фаски подпись прежняя — старые объекты не пересобираются.
            if (HasBevel(model))
                signature.Append("|b").Append(model.BevelSize).Append('x').Append(model.BevelSegments);

            return signature.ToString();
        }


        private static bool HasBevel(BoxModel model)
        {
            return model.BevelSize > 0f;
        }


        private static BoxModel[] FindModels()
        {
            return StageUtility.GetCurrentStageHandle().FindComponentsOfType<BoxModel>();
        }


        // ── События редактора ────────────────────────────────────────────

        // Меш пишется на диск вместе со сценой — иначе сцена сохранена, а файл меша остался старым.
        private static void OnSceneSaving(Scene scene, string path)
        {
            foreach (var root in scene.GetRootGameObjects())
                SaveMeshFiles(root);
        }

        // То же для префаба.
        private static void OnPrefabSaving(GameObject prefabRoot)
        {
            SaveMeshFiles(prefabRoot);
        }

        // Меши объектов, стен и зданий под root — на диск. Зовут сохранение сцены, префаба и пересборка префаба.
        public static void SaveMeshFiles(GameObject root)
        {
            foreach (var model in root.GetComponentsInChildren<BoxModel>(true))
                SaveMeshFile(model.GetComponent<MeshFilter>().sharedMesh);

            foreach (var wall in root.GetComponentsInChildren<SplineWall>(true))
                SaveMeshFile(wall.GetComponent<MeshFilter>().sharedMesh);

            foreach (var part in root.GetComponentsInChildren<SplineBuildingPart>(true))
                SaveMeshFile(part.GetComponent<MeshFilter>().sharedMesh);
        }

        private static void SaveMeshFile(Mesh mesh)
        {
            BoxModelMeshStorage.AddLightmapUVs(mesh);

            if (BoxModelMeshStorage.IsBoxModelMeshFile(mesh))
                AssetDatabase.SaveAssetIfDirty(mesh);
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
