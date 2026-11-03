using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Выбор кликом по дыре того, кто её прорезал, но сам невидим: вырез (<see cref="GeometryCutter"/>, со всеми копиями вверх) или вырезающая коробка в здании.
    /// Дыру рисует меш самой стены или здания, а клик сквозь дыру улетает дальше. Здесь — по коробке, в которую вписан резчик:
    /// клик выделяет его, если перед ним ничего нет. Работает и в Live Builder, и в обычном выборе Unity (Move, Rotate), даже без значков (Gizmos).
    /// Вырезающие коробки обычного объекта из коробок сюда не входят: их и так выделяет клик по стенке дыры.
    /// </summary>
    [InitializeOnLoad]
    public static class HoleCutterPicker
    {
        static HoleCutterPicker()
        {
            HandleUtility.pickGameObjectCustomPasses += PickForUnitySelection;
        }


        // Ближайший резчик под лучом: вырез или вырезающая коробка в здании. Расстояние — до входа в его коробку, м.
        public static bool TryPick(Ray ray, out Component picked, out float distance)
        {
            picked = null;
            distance = float.MaxValue;
            var stage = StageUtility.GetCurrentStageHandle();

            foreach (var cutter in stage.FindComponentsOfType<GeometryCutter>())
                if (cutter.isActiveAndEnabled)
                    ConsiderCutter(ray, cutter, ref picked, ref distance);

            foreach (var model in stage.FindComponentsOfType<BoxModel>())
                if (model.GetComponentInChildren<SplineBuilding>() != null)
                    ConsiderBuildingCutBoxes(ray, model, ref picked, ref distance);

            return picked != null;
        }

        // Вырез со всеми копиями вверх — по коробке, в которую вписана его форма.
        private static void ConsiderCutter(Ray ray, GeometryCutter cutter, ref Component picked, ref float distance)
        {
            var box = new Bounds(new Vector3(0f, cutter.Height / 2f, 0f), new Vector3(cutter.Width, cutter.Height, cutter.Depth));

            for (var copy = 0; copy < cutter.CopiesUp; copy++)
                Consider(ray, cutter, GeometryCutterOutline.CopyToWorld(cutter, copy), box, ref picked, ref distance);
        }

        // Вырезающие коробки в зданиях этого объекта — со всеми копиями по этажам.
        private static void ConsiderBuildingCutBoxes(Ray ray, BoxModel model, ref Component picked, ref float distance)
        {
            foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
            {
                if (box.Mode != BoxModelPartMode.Cut || box.Owner is not BoxModelPart part || part.GetComponentInParent<SplineBuilding>() == null)
                    continue;

                var min = (Vector3)box.Box.Min / MillimeterBox.MillimetersPerMeter;
                var max = (Vector3)box.Box.Max / MillimeterBox.MillimetersPerMeter;
                Consider(ray, part, model.transform.localToWorldMatrix, new Bounds((min + max) / 2f, max - min), ref picked, ref distance);
            }
        }

        // Луч входит в коробку резчика ближе найденного — он и выбран.
        private static void Consider(Ray ray, Component candidate, Matrix4x4 localToWorld, Bounds localBox, ref Component picked, ref float distance)
        {
            var toLocal = localToWorld.inverse;
            var localRay = new Ray(toLocal.MultiplyPoint3x4(ray.origin), toLocal.MultiplyVector(ray.direction));

            if (!localBox.IntersectRay(localRay, out var localDistance))
                return;

            var worldDistance = Vector3.Distance(ray.origin, localToWorld.MultiplyPoint3x4(localRay.GetPoint(localDistance)));

            if (worldDistance >= distance)
                return;

            distance = worldDistance;
            picked = candidate;
        }


        // Резчик под лучом, перед которым нет коллайдера: его дыру и видно.
        public static bool TryPickUnblocked(Ray ray, out Component picked)
        {
            if (!TryPick(ray, out picked, out var distance))
                return false;

            if (!Physics.Raycast(ray, out _, Mathf.Max(0f, distance - 0.01f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return true;

            picked = null;
            return false;
        }


        // Выбор кликом Unity: резчик под мышью, если перед ним нет коллайдера; иначе — как обычно.
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
