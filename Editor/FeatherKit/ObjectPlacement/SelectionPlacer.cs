using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Переставляет выделенные в сцене объекты: на точку поверхности или к камере сцены.
    /// Каждая перестановка отменяется через Ctrl+Z одним шагом.
    /// </summary>
    public static class SelectionPlacer
    {
        // ── Что переставляем ─────────────────────────────

        // Только верхние из выделенных: ребёнок выделенного объекта и так едет вместе с ним.
        private static Transform[] Targets => Selection.transforms;

        public static bool HasTargets => Targets.Length > 0;


        // ── К камере ─────────────────────────────────────

        public static void MoveToCamera(SceneView sceneView, bool takeCameraRotation)
        {
            if (sceneView == null)
                return;

            var camera = sceneView.camera.transform;
            var targets = Targets;
            Undo.RecordObjects(targets, takeCameraRotation ? "Перенести к камере с поворотом" : "Перенести к камере");

            foreach (var target in targets)
            {
                target.position = camera.position;

                if (takeCameraRotation)
                    target.rotation = camera.rotation;
            }
        }


        // ── На поверхность ───────────────────────────────

        // Объект наклоняется так, чтобы его «верх» смотрел от поверхности, а куда он
        // был развёрнут — сохраняется. Затем встаёт низом на точку, а не тонет в ней центром.
        public static void PlaceOnSurface(Vector3 point, Vector3 normal)
        {
            var targets = Targets;
            Undo.RecordObjects(targets, "Поставить на поверхность");

            foreach (var target in targets)
            {
                target.rotation = Quaternion.FromToRotation(target.up, normal) * target.rotation;
                target.position = point;
                target.position += normal * PivotHeightAboveBottom(target, normal);
            }
        }

        // Насколько точка опоры объекта выше его самого низа вдоль up. Низ считаем по
        // мешам объекта и детей; мешей нет — опорой служит сама точка опоры.
        private static float PivotHeightAboveBottom(Transform target, Vector3 up)
        {
            var corners = MeshBoundsCorners.CollectInWorld(target);
            float lowest = 0f;

            for (int i = 0; i < corners.Count; i++)
            {
                float height = Vector3.Dot(corners[i] - target.position, up);
                lowest = i == 0 ? height : Mathf.Min(lowest, height);
            }

            return -lowest;
        }
    }
}
