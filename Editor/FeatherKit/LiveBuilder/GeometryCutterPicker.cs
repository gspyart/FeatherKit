using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Резчик дыры под лучом: кто он, где луч входит в его коробку и выходит из неё и где проходит сквозь саму дыру, м от начала луча.
    /// Решает, видно ли его дыру при ближайшей поверхности на таком-то расстоянии.
    /// </summary>
    public readonly struct GeometryCutterRayHit
    {
        // Ближе на столько — считается ближе, м: грани на одном месте не спорят.
        private const float Tolerance = 0.01f;


        public GeometryCutterRayHit(Component cutter, float entryDistance, float exitDistance, float holeCrossingDistance)
        {
            Cutter = cutter;
            EntryDistance = entryDistance;
            ExitDistance = exitDistance;
            HoleCrossingDistance = holeCrossingDistance;
        }


        public Component Cutter { get; }
        public float EntryDistance { get; }
        public float ExitDistance { get; }

        // Где луч проходит сквозь дыру: у выреза — через его середину по глубине, в пределах формы; не проходит — бесконечность.
        public float HoleCrossingDistance { get; }


        // Дыру видно, если перед коробкой резчика ничего нет и луч либо смотрит сквозь дыру, либо упирается в её откос (внутри коробки).
        // Луч, прошедший через пустую часть коробки мимо дыры к поверхности за ней, — не резчик: виднее та поверхность.
        public bool IsVisibleBefore(float nearestSurfaceDistance)
        {
            if (EntryDistance >= nearestSurfaceDistance + Tolerance)
                return false;

            var isNearestOnReveal = nearestSurfaceDistance <= ExitDistance + Tolerance;
            var isLookingThroughHole = HoleCrossingDistance <= nearestSurfaceDistance + Tolerance;
            return isNearestOnReveal || isLookingThroughHole;
        }
    }


    /// <summary>
    /// Выбор кликом по дыре того, кто её прорезал, но сам невидим: вырез (<see cref="GeometryCutter"/>, со всеми копиями вверх) или вырезающая коробка в здании.
    /// Дыру рисует меш самой стены или здания, а клик сквозь дыру улетает дальше. Здесь — по коробке, в которую вписан резчик:
    /// клик выделяет его, если перед ним ничего нет и клик — по дыре или её откосу, а не по пустой части коробки мимо дыры (<see cref="GeometryCutterRayHit"/>).
    /// Работает и в Live Builder, и в обычном выборе Unity (Move, Rotate), даже без значков (Gizmos).
    /// Вырезающие коробки обычного объекта из коробок сюда не входят: их и так выделяет клик по стенке дыры.
    /// </summary>
    [InitializeOnLoad]
    public static class GeometryCutterPicker
    {
        static GeometryCutterPicker()
        {
            HandleUtility.pickGameObjectCustomPasses += PickForUnitySelection;
        }


        // Резчик под лучом, в чью коробку луч входит раньше всех: вырез или вырезающая коробка в здании.
        public static bool TryPick(Ray ray, out GeometryCutterRayHit hit)
        {
            hit = new GeometryCutterRayHit(null, float.MaxValue, float.MaxValue, float.MaxValue);
            var stage = StageUtility.GetCurrentStageHandle();

            foreach (var cutter in stage.FindComponentsOfType<GeometryCutter>())
                if (cutter.isActiveAndEnabled)
                    ConsiderCutter(ray, cutter, ref hit);

            foreach (var model in stage.FindComponentsOfType<BoxModel>())
                if (model.gameObject.activeInHierarchy && model.GetComponentInChildren<SplineBuilding>() != null)
                    ConsiderBuildingCutBoxes(ray, model, ref hit);

            return hit.Cutter != null;
        }

        // Вырез со всеми копиями вверх — по коробке, в которую вписана его форма; дыра — середина коробки по глубине (Z выреза).
        private static void ConsiderCutter(Ray ray, GeometryCutter cutter, ref GeometryCutterRayHit hit)
        {
            var box = new Bounds(new Vector3(0f, cutter.Height / 2f, 0f), new Vector3(cutter.Width, cutter.Height, cutter.Depth));

            for (var copy = 0; copy < cutter.CopiesUp; copy++)
                Consider(ray, cutter, GeometryCutterOutline.CopyToWorld(cutter, copy), box, true, ref hit);
        }

        // Вырезающие коробки в зданиях этого объекта — со всеми копиями по этажам. Дыра у них — вся коробка: луч в ней — уже в дыре.
        private static void ConsiderBuildingCutBoxes(Ray ray, BoxModel model, ref GeometryCutterRayHit hit)
        {
            foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
            {
                if (box.Mode != BoxModelPartMode.Cut || box.Owner is not BoxModelPart part || part.GetComponentInParent<SplineBuilding>() == null)
                    continue;

                var min = box.Box.MinInMeters;
                var max = box.Box.MaxInMeters;
                Consider(ray, part, model.transform.localToWorldMatrix, new Bounds((min + max) / 2f, max - min), false, ref hit);
            }
        }

        // Луч входит в коробку резчика раньше найденного — он и выбран.
        private static void Consider(Ray ray, Component candidate, Matrix4x4 localToWorld, Bounds localBox, bool isHoleAtMiddleDepth, ref GeometryCutterRayHit hit)
        {
            var toLocal = localToWorld.inverse;
            var localRay = new Ray(toLocal.MultiplyPoint3x4(ray.origin), toLocal.MultiplyVector(ray.direction));

            if (!TryIntersectBox(localRay, localBox, out var localEntry, out var localExit))
                return;

            float WorldDistanceAt(float localDistance) => Vector3.Distance(ray.origin, localToWorld.MultiplyPoint3x4(localRay.GetPoint(localDistance)));

            var entry = WorldDistanceAt(localEntry);

            if (entry >= hit.EntryDistance)
                return;

            var crossing = float.MaxValue;

            if (!isHoleAtMiddleDepth)
                crossing = entry;
            else if (Mathf.Abs(localRay.direction.z) > 1e-6f)
            {
                var localCrossing = (localBox.center.z - localRay.origin.z) / localRay.direction.z;

                if (localCrossing >= localEntry && localCrossing <= localExit)
                    crossing = WorldDistanceAt(localCrossing);
            }

            hit = new GeometryCutterRayHit(candidate, entry, WorldDistanceAt(localExit), crossing);
        }

        // Где луч входит в коробку и выходит из неё, в его единицах; начало луча внутри — вход ноль.
        private static bool TryIntersectBox(Ray ray, Bounds box, out float entry, out float exit)
        {
            entry = float.MinValue;
            exit = float.MaxValue;

            for (var axis = 0; axis < 3; axis++)
            {
                var origin = ray.origin[axis];
                var direction = ray.direction[axis];

                if (Mathf.Abs(direction) < 1e-8f)
                {
                    if (origin < box.min[axis] || origin > box.max[axis])
                        return false;

                    continue;
                }

                var near = (box.min[axis] - origin) / direction;
                var far = (box.max[axis] - origin) / direction;

                if (near > far)
                    (near, far) = (far, near);

                entry = Mathf.Max(entry, near);
                exit = Mathf.Min(exit, far);

                if (entry > exit)
                    return false;
            }

            if (exit < 0f)
                return false;

            entry = Mathf.Max(entry, 0f);
            return true;
        }


        // Резчик под лучом, чью дыру видно: перед ним нет коллайдера, и клик — по дыре или её откосу.
        public static bool TryPickUnblocked(Ray ray, out Component picked)
        {
            picked = null;

            if (!TryPick(ray, out var hit))
                return false;

            var nearest = Physics.Raycast(ray, out var physicsHit, float.MaxValue, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? physicsHit.distance
                : float.MaxValue;

            if (!hit.IsVisibleBefore(nearest))
                return false;

            picked = hit.Cutter;
            return true;
        }


        // Выбор кликом Unity: резчик под мышью, если его дыру видно; иначе — как обычно.
        private static GameObject PickForUnitySelection(Camera camera, int layers, Vector2 position, GameObject[] ignore, GameObject[] filter, out int materialIndex)
        {
            materialIndex = -1;
            var ray = Camera.current != null ? HandleUtility.GUIPointToWorldRay(position) : camera.ScreenPointToRay(HandleUtility.GUIPointToScreenPixelCoordinate(position));

            if (!TryPickUnblocked(ray, out var picked))
                return null;

            var pickedObject = picked.gameObject;
            var isIgnored = ignore != null && System.Array.IndexOf(ignore, pickedObject) >= 0;
            var isFilteredOut = filter != null && System.Array.IndexOf(filter, pickedObject) < 0;
            return isIgnored || isFilteredOut || (layers & (1 << pickedObject.layer)) == 0 ? null : pickedObject;
        }
    }
}
