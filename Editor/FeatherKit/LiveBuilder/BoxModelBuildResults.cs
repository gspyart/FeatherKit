using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Что посчитано из коробок каждого объекта (<see cref="BoxModelBuildResult"/>) и память, из которой это считается быстро.
    /// - результат живёт до следующей правки сцены: его спрашивают на каждую отрисовку, наведение мыши и каждую стену объекта
    /// - поверхности — по геометрии коробок: смена материала их не пересчитывает
    /// - у объекта своя память солвера по коробкам и поверхностей по плоскостям: правка пересчитывает только задетое
    /// </summary>
    public static class BoxModelBuildResults
    {
        private static readonly Dictionary<EntityId, BoxModelBuildResult> ResultsByModel = new();

        // При каком номере правки посчитан результат объекта: номер тот же — сцена не менялась.
        private static readonly Dictionary<EntityId, int> VersionsByModel = new();

        private static readonly Dictionary<EntityId, BoxModelSolverCache> SolverCachesByModel = new();
        private static readonly Dictionary<EntityId, BoxModelSurfaceCache> SurfaceCachesByModel = new();

        private static int sceneVersion;


        // Номер правки сцены: поменялся — всё, что посчитано по сцене, устарело.
        public static int SceneVersion => sceneVersion;


        // ── Результат ────────────────────────────────────────────────────

        // С прошлой правки сцены — из памяти. Та же геометрия — те же поверхности, но коробки новые: их хозяева могли смениться
        // (удалил коробку и нарисовал такую же), а по хозяину выделяют кликом.
        public static BoxModelBuildResult Of(BoxModel model)
        {
            var modelId = model.GetEntityId();
            var hasCached = ResultsByModel.TryGetValue(modelId, out var cached);

            if (hasCached && VersionsByModel.TryGetValue(modelId, out var version) && version == sceneVersion)
                return cached;

            var ramps = new List<BoxModelRampShape>();
            List<BoxModelSolverBox> boxes;
            string signature;
            Hash128 geometrySignature;

            using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.SignatureCheck))
            {
                boxes = BoxModelShapeCollector.Collect(model, ramps);
                signature = BoxModelSignature.Of(model, boxes, ramps);
                geometrySignature = BoxModelSignature.GeometryOf(model, boxes);
            }

            var surfaces = hasCached && cached.GeometrySignature == geometrySignature ? cached.Surfaces : SolveSurfaces(model, boxes);
            var result = new BoxModelBuildResult(signature, geometrySignature, boxes, ramps, surfaces);
            ResultsByModel[modelId] = result;
            VersionsByModel[modelId] = sceneVersion;
            return result;
        }

        private static List<BoxModelSurface> SolveSurfaces(BoxModel model, List<BoxModelSolverBox> boxes)
        {
            var modelId = model.GetEntityId();
            List<BoxModelCell> cells;

            using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.Solver))
                cells = SolverCacheOf(modelId).Solve(boxes.ConvertAll(box => box.Shape), LiveBuilderCutRules.RuleOf(model),
                    LiveBuilderCutRules.SolverFilterOf(model, boxes));

            using (LiveBuilderRebuildProfiler.Measure(LiveBuilderRebuildStage.Surfaces))
                return SurfaceCacheOf(modelId).FindOuterSurfaces(cells);
        }


        // Поверхности объекта с чужими коробками (предпросмотр рисования): память объекта только читается.
        public static List<BoxModelSurface> SurfacesWithoutStoring(BoxModel model, List<BoxModelSolverBox> boxes)
        {
            var modelId = model.GetEntityId();
            var cells = SolverCacheOf(modelId).SolveWithoutStoring(boxes.ConvertAll(box => box.Shape), LiveBuilderCutRules.RuleOf(model),
                LiveBuilderCutRules.SolverFilterOf(model, boxes));
            return SurfaceCacheOf(modelId).FindOuterSurfacesWithoutStoring(cells);
        }


        private static BoxModelSolverCache SolverCacheOf(EntityId modelId)
        {
            if (!SolverCachesByModel.TryGetValue(modelId, out var cache))
                SolverCachesByModel[modelId] = cache = new BoxModelSolverCache();

            return cache;
        }

        private static BoxModelSurfaceCache SurfaceCacheOf(EntityId modelId)
        {
            if (!SurfaceCachesByModel.TryGetValue(modelId, out var cache))
                SurfaceCachesByModel[modelId] = cache = new BoxModelSurfaceCache();

            return cache;
        }


        // ── Устаревание ──────────────────────────────────────────────────

        // Сцена поменялась — все результаты пересчитать при следующем вопросе. Память солвера и поверхностей остаётся: по ней пересчёт быстрый.
        public static void ForgetAll()
        {
            sceneVersion++;
        }


        // Объект правят прямо сейчас, до того как о правке узнала очередь.
        public static void Forget(BoxModel model)
        {
            VersionsByModel.Remove(model.GetEntityId());
        }


        // Объект удалён или его сцена закрыта — его результат и память солвера больше не нужны. Возвращает, сколько объектов забыто.
        public static int ForgetDestroyed()
        {
            var forgotten = LiveBuilderDestroyedObjects.RemoveFrom(ResultsByModel);
            LiveBuilderDestroyedObjects.RemoveFrom(VersionsByModel);
            LiveBuilderDestroyedObjects.RemoveFrom(SolverCachesByModel);
            LiveBuilderDestroyedObjects.RemoveFrom(SurfaceCachesByModel);
            return forgotten;
        }
    }


    /// <summary>Что посчитано из коробок объекта: подписи, коробки расчёта, скаты лестниц и наружные поверхности.</summary>
    public sealed class BoxModelBuildResult
    {
        public BoxModelBuildResult(string signature, Hash128 geometrySignature, List<BoxModelSolverBox> boxes, List<BoxModelRampShape> ramps,
            List<BoxModelSurface> surfaces)
        {
            Signature = signature;
            GeometrySignature = geometrySignature;
            Boxes = boxes;
            Ramps = ramps;
            Surfaces = surfaces;
            SurfaceBounds = BoundsOf(surfaces);
        }


        // Всё, от чего зависит меш (<see cref="BoxModelSignature.Of"/>).
        public string Signature { get; }

        // Только то, от чего зависят поверхности: места и режимы коробок, а у нового правила — кто кого режет.
        public Hash128 GeometrySignature { get; }

        public List<BoxModelSolverBox> Boxes { get; }
        public List<BoxModelRampShape> Ramps { get; }
        public List<BoxModelSurface> Surfaces { get; }

        // Габарит всех наружных поверхностей в осях объекта, м: луч мимо него — ни одной поверхности не задевает.
        public Bounds SurfaceBounds { get; }


        private static Bounds BoundsOf(List<BoxModelSurface> surfaces)
        {
            var bounds = new Bounds();
            var hasBounds = false;

            foreach (var surface in surfaces)
            {
                var axis = BoxFaces.Axis(surface.Face);
                var axisU = BoxFaces.SurfaceAxisU(surface.Face);
                var axisV = BoxFaces.SurfaceAxisV(surface.Face);
                var min = Vector3.zero;
                var max = Vector3.zero;
                min[axis] = max[axis] = MillimeterBox.ToMeters(surface.Plane);
                min[axisU] = MillimeterBox.ToMeters(surface.Area.xMin);
                max[axisU] = MillimeterBox.ToMeters(surface.Area.xMax);
                min[axisV] = MillimeterBox.ToMeters(surface.Area.yMin);
                max[axisV] = MillimeterBox.ToMeters(surface.Area.yMax);

                if (!hasBounds)
                {
                    bounds.SetMinMax(min, max);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(min);
                    bounds.Encapsulate(max);
                }
            }

            return bounds;
        }
    }
}
