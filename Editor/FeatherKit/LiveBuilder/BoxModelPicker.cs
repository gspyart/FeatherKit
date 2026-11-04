using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Что под курсором на объекте из коробок: какой объект, какая поверхность и точка на ней в осях объекта.</summary>
    public readonly struct BoxModelSurfaceHit
    {
        public BoxModelSurfaceHit(BoxModel model, BoxModelSurface surface, Vector3 localPoint, float distance)
        {
            Model = model;
            Surface = surface;
            LocalPoint = localPoint;
            Distance = distance;
        }

        public BoxModel Model { get; }
        public BoxModelSurface Surface { get; }
        public Vector3 LocalPoint { get; }
        public float Distance { get; }
    }


    /// <summary>
    /// Ищет поверхность объекта из коробок под лучом мыши: по прямоугольникам наружных поверхностей, без физики —
    /// работает и у объекта без коллайдера. Прочая геометрия сцены перед объектом закрывает его — и здания, и стены по линии, даже лежащие в объекте из коробок.
    /// Спрятанное разрезом над этажом (<see cref="LiveBuilderStoreys"/>) не ловится: клик проходит сквозь него.
    /// </summary>
    public static class BoxModelPicker
    {
        // Поверхности одного объекта под лучом — один список на все вызовы: поиск идёт на каждое движение мыши.
        private static readonly List<BoxModelSurfaceHit> ModelHitsScratch = new();


        public static bool TryPickSurface(Ray worldRay, out BoxModelSurfaceHit hit)
        {
            hit = default;
            var bestDistance = float.MaxValue;

            foreach (var model in StageUtility.GetCurrentStageHandle().FindComponentsOfType<BoxModel>())
            {
                // Выключенный объект — как в Unity: не ловится и не служит опорой.
                if (!model.gameObject.activeInHierarchy || !TryPickOnModel(model, worldRay, out var modelHit) || modelHit.Distance >= bestDistance)
                    continue;

                hit = modelHit;
                bestDistance = modelHit.Distance;
            }

            if (bestDistance == float.MaxValue)
                return false;

            // Закрывает всё, кроме меша самого объекта из коробок — и здание или стена по линии внутри объекта тоже:
            // иначе коробка, нарисованная на полу здания, легла бы на объект под ним.
            var isHiddenBehindOtherGeometry = TryRaycastVisible(worldRay, bestDistance, out var physicsHit)
                && !physicsHit.collider.TryGetComponent<BoxModel>(out _)
                && physicsHit.distance < bestDistance - 0.01f;

            return !isHiddenBehindOtherGeometry;
        }


        // Луч по коллайдерам сцены, пропуская спрятанное разрезом над этажом.
        public static bool TryRaycastVisible(Ray worldRay, float maxDistance, out RaycastHit visibleHit)
        {
            var hits = Physics.RaycastAll(worldRay, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));

            foreach (var hit in hits)
                if (!LiveBuilderStoreys.IsHiddenBySection(hit.point))
                {
                    visibleHit = hit;
                    return true;
                }

            visibleHit = default;
            return false;
        }


        // Коробка расчёта, которая создала грань под мышью; по стенке дыры — вырезающая. Не нашлась — null.
        public static BoxModelSolverBox SourceBoxOf(BoxModelSurfaceHit hit)
        {
            var boxes = BoxModelRebuilder.GetSolverBoxes(hit.Model);
            var sourceBox = hit.Surface.SourcePart;
            return sourceBox >= 0 && sourceBox < boxes.Count ? boxes[sourceBox] : null;
        }


        // Коробка на сцене, которая создала грань под мышью; у ступени лестницы — null.
        public static BoxModelPart SourcePartOf(BoxModelSurfaceHit hit)
        {
            return SourceBoxOf(hit)?.OwnerPart;
        }


        // Все лицевые поверхности включённых объектов под лучом, от ближней к дальней, — и закрытые другими тоже: для повторного клика вглубь.
        public static List<BoxModelSurfaceHit> PickAllSurfaces(Ray worldRay)
        {
            var hits = new List<BoxModelSurfaceHit>();

            foreach (var model in StageUtility.GetCurrentStageHandle().FindComponentsOfType<BoxModel>())
                if (model.gameObject.activeInHierarchy)
                    CollectHitsOnModel(model, worldRay, hits);

            hits.Sort((first, second) => first.Distance.CompareTo(second.Distance));
            return hits;
        }


        // Ближайшая лицевая поверхность объекта под лучом.
        private static bool TryPickOnModel(BoxModel model, Ray worldRay, out BoxModelSurfaceHit hit)
        {
            hit = default;
            ModelHitsScratch.Clear();
            CollectHitsOnModel(model, worldRay, ModelHitsScratch);

            foreach (var modelHit in ModelHitsScratch)
                if (hit.Model == null || modelHit.Distance < hit.Distance)
                    hit = modelHit;

            return hit.Model != null;
        }

        // Лицевые поверхности объекта, через которые проходит луч; спрятанное разрезом над этажом — нет.
        private static void CollectHitsOnModel(BoxModel model, Ray worldRay, List<BoxModelSurfaceHit> hits)
        {
            var modelTransform = model.transform;
            var origin = modelTransform.InverseTransformPoint(worldRay.origin);
            // С масштабом, как и точка: при неравном масштабе без него луч в осях объекта уходит вбок. Расстояние меряется в мире — ниже.
            var direction = modelTransform.InverseTransformVector(worldRay.direction);
            IReadOnlyList<BoxModelSurface> surfaces = BoxModelRebuilder.GetSurfaces(model);

            foreach (var surface in surfaces)
            {
                var axis = BoxFaces.Axis(surface.Face);

                if (Vector3.Dot(direction, BoxFaces.Normal(surface.Face)) >= 0f)
                    continue;

                var along = (MillimeterBox.ToMeters(surface.Plane) - origin[axis]) / direction[axis];

                if (along <= 0f)
                    continue;

                var point = origin + direction * along;

                if (!Contains(surface, point))
                    continue;

                var worldPoint = modelTransform.TransformPoint(point);

                if (!LiveBuilderStoreys.IsHiddenBySection(worldPoint))
                    hits.Add(new BoxModelSurfaceHit(model, surface, point, Vector3.Distance(worldRay.origin, worldPoint)));
            }
        }

        private static bool Contains(BoxModelSurface surface, Vector3 localPoint)
        {
            var u = localPoint[BoxFaces.SurfaceAxisU(surface.Face)] * MillimeterBox.MillimetersPerMeter;
            var v = localPoint[BoxFaces.SurfaceAxisV(surface.Face)] * MillimeterBox.MillimetersPerMeter;
            var area = surface.Area;
            return u >= area.xMin && u <= area.xMax && v >= area.yMin && v <= area.yMax;
        }
    }
}
