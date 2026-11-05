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
    /// Поверхность в дыре выреза (<see cref="GeometryCutter"/>), который режет объект, тоже не ловится: в готовом меше её там нет,
    /// клик идёт сквозь дверь к следующей поверхности. Дно ниши и откос дыры — уже меш — закрывают то, что за ними.
    /// </summary>
    public static class BoxModelPicker
    {
        // Запас у границы тела выреза, м: дно ниши и откос лежат ровно на ней.
        private const float CutterHoleMargin = 0.01f;

        // Запас габарита объекта при отсеве лучом, м: поверхность ровно на краю габарита не теряется.
        private const float BoundsMargin = 0.01f;

        // Поверхности одного объекта под лучом — один список на все вызовы: поиск идёт на каждое движение мыши.
        private static readonly List<BoxModelSurfaceHit> ModelHitsScratch = new();

        // Последний ответ: подсветка грани и прицел спрашивают про тот же луч дважды за одно движение мыши — второй раз из памяти.
        // Только в пределах одного события окна сцены (тот же тип, место мыши, номер правки сцены, этаж и разрез): без окна сцены памяти нет.
        private static Ray lastRay;
        private static EventType lastEventType;
        private static Vector2 lastMousePosition;
        private static int lastSceneVersion = -1;
        private static int lastSectionKey;
        private static bool lastIsFound;
        private static BoxModelSurfaceHit lastHit;

        // Все попадания луча по коллайдерам, по дальности, — тоже на одно событие: подсветка, прицел и разбор под мышью пускают тот же луч.
        private static RaycastHit[] lastPhysicsHits;
        private static Ray lastPhysicsRay;
        private static EventType lastPhysicsEventType;
        private static Vector2 lastPhysicsMousePosition;
        private static int lastPhysicsSceneVersion = -1;


        public static bool TryPickSurface(Ray worldRay, out BoxModelSurfaceHit hit)
        {
            var current = Event.current;

            if (current == null)
                return PickSurface(worldRay, out hit);

            var sectionKey = LiveBuilderStoreys.IsSectionEnabled ? LiveBuilderStoreys.CurrentStorey + 1 : 0;
            var isSameQuestion = lastSceneVersion == BoxModelRebuilder.SceneVersion && lastSectionKey == sectionKey && lastEventType == current.type
                                 && lastMousePosition == current.mousePosition && lastRay.origin == worldRay.origin && lastRay.direction == worldRay.direction;

            if (isSameQuestion && (!lastIsFound || lastHit.Model != null))
            {
                hit = lastHit;
                return lastIsFound;
            }

            lastIsFound = PickSurface(worldRay, out hit);
            lastHit = hit;
            lastRay = worldRay;
            lastEventType = current.type;
            lastMousePosition = current.mousePosition;
            lastSceneVersion = BoxModelRebuilder.SceneVersion;
            lastSectionKey = sectionKey;
            return lastIsFound;
        }

        private static bool PickSurface(Ray worldRay, out BoxModelSurfaceHit hit)
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
            // иначе коробка, нарисованная на полу здания, легла бы на объект под ним. Меш объекта закрывает только в дыре выреза:
            // дно ниши и откос — грани меша, которых нет среди поверхностей.
            var isHiddenBehindOtherGeometry = TryRaycastVisible(worldRay, bestDistance, out var physicsHit)
                && physicsHit.distance < bestDistance - 0.01f
                && (!physicsHit.collider.TryGetComponent<BoxModel>(out var hitModel) || IsInCutterHole(CuttersOf(hitModel), physicsHit.point, CutterHoleMargin));

            return !isHiddenBehindOtherGeometry;
        }


        // Луч по коллайдерам сцены, пропуская спрятанное разрезом над этажом.
        public static bool TryRaycastVisible(Ray worldRay, float maxDistance, out RaycastHit visibleHit)
        {
            foreach (var hit in SortedPhysicsHits(worldRay))
            {
                if (hit.distance > maxDistance)
                    break;

                if (LiveBuilderStoreys.IsHiddenBySection(hit.point))
                    continue;

                visibleHit = hit;
                return true;
            }

            visibleHit = default;
            return false;
        }

        // Все попадания луча, ближние первыми; в пределах одного события окна сцены — из памяти, без окна сцены (проверки) — каждый раз заново.
        private static RaycastHit[] SortedPhysicsHits(Ray worldRay)
        {
            var current = Event.current;
            var isSameQuestion = current != null && lastPhysicsHits != null && lastPhysicsSceneVersion == BoxModelRebuilder.SceneVersion
                                 && lastPhysicsEventType == current.type && lastPhysicsMousePosition == current.mousePosition
                                 && lastPhysicsRay.origin == worldRay.origin && lastPhysicsRay.direction == worldRay.direction;

            if (isSameQuestion && AreCollidersAlive(lastPhysicsHits))
                return lastPhysicsHits;

            var hits = Physics.RaycastAll(worldRay, float.MaxValue, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));

            if (current == null)
                return hits;

            lastPhysicsHits = hits;
            lastPhysicsRay = worldRay;
            lastPhysicsEventType = current.type;
            lastPhysicsMousePosition = current.mousePosition;
            lastPhysicsSceneVersion = BoxModelRebuilder.SceneVersion;
            return hits;
        }

        // Коллайдер из памяти удалили в том же событии (клик удалил или пересобрал объект) — ответ уже не годится.
        private static bool AreCollidersAlive(RaycastHit[] hits)
        {
            foreach (var hit in hits)
                if (hit.collider == null)
                    return false;

            return true;
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

        // Лицевые поверхности объекта, через которые проходит луч; спрятанное разрезом над этажом и вырезанное вырезом — нет.
        private static void CollectHitsOnModel(BoxModel model, Ray worldRay, List<BoxModelSurfaceHit> hits)
        {
            var modelTransform = model.transform;
            var origin = modelTransform.InverseTransformPoint(worldRay.origin);
            // С масштабом, как и точка: при неравном масштабе без него луч в осях объекта уходит вбок. Расстояние меряется в мире — ниже.
            var direction = modelTransform.InverseTransformVector(worldRay.direction);

            // Луч мимо габарита объекта — ни одной его поверхности не задевает: поверхности не перебираем.
            var bounds = BoxModelRebuilder.GetSurfaceBounds(model);
            bounds.Expand(BoundsMargin);

            if (!bounds.IntersectRay(new Ray(origin, direction)))
                return;

            IReadOnlyList<BoxModelSurface> surfaces = BoxModelRebuilder.GetSurfaces(model);
            // Вырезы ищутся, только когда луч попал хоть в одну поверхность: поиск идёт на каждое движение мыши.
            List<GeometryCutter> cutters = null;

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

                if (LiveBuilderStoreys.IsHiddenBySection(worldPoint))
                    continue;

                cutters ??= CuttersOf(model);

                if (!IsInCutterHole(cutters, worldPoint, 0f))
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


        // ── Дыры вырезов ─────────────────────────────────────────────────

        // Вырезы, которые режут объект (<see cref="GeometryCutterLookup"/>).
        private static List<GeometryCutter> CuttersOf(BoxModel model)
        {
            return GeometryCutterLookup.CuttersFor(model, GeometryCutterLookup.WorldBoundsOf(model.GetComponent<MeshRenderer>()));
        }


        // Точка в мире — в дыре одного из вырезов, с запасом margin, м.
        private static bool IsInCutterHole(List<GeometryCutter> cutters, Vector3 worldPoint, float margin)
        {
            foreach (var cutter in cutters)
                if (GeometryCutterOutline.Contains(cutter, worldPoint, margin))
                    return true;

            return false;
        }
    }
}
