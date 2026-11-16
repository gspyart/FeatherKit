using UnityEngine;
using UnityEngine.AI;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Перенос объекта, который не откатывают навигация и физика.
    /// - агент навигации — через Warp; Character Controller выключается на перенос
    /// - Rigidbody получает позицию и поворот сам, по желанию — с остановкой
    /// </summary>
    public static class ScenarioTeleport
    {
        public static void Place(GameObject moved, Vector3 position, Quaternion rotation, bool resetsVelocity)
        {
            if (moved.TryGetComponent<NavMeshAgent>(out var agent) && agent.enabled && agent.isOnNavMesh)
            {
                agent.Warp(position);
                moved.transform.rotation = rotation;
                return;
            }

            var controller = moved.GetComponent<CharacterController>();
            var wasControllerEnabled = controller != null && controller.enabled;

            if (wasControllerEnabled)
                controller.enabled = false;

            moved.transform.SetPositionAndRotation(position, rotation);

            if (moved.TryGetComponent<Rigidbody>(out var body))
                PlaceBody(body, position, rotation, resetsVelocity);

            if (wasControllerEnabled)
                controller.enabled = true;
        }

        // Интерполированное тело иначе дорисует пару кадров на старом месте.
        private static void PlaceBody(Rigidbody body, Vector3 position, Quaternion rotation, bool resetsVelocity)
        {
            body.position = position;
            body.rotation = rotation;

            if (!resetsVelocity || body.isKinematic)
                return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
