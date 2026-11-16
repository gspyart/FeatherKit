using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Поставить объект в точку сразу — например, туда, что запомнил «Remember Position».
    /// Character Controller и Rigidbody переносятся честно, без отката физикой на старое место.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Transform/Set Position", Description = "Put an object at a point at once")]
    public class SetPositionAction : ScenarioInstantAction
    {
        [ScenarioLabel("What to Move")]
        [SerializeField] private ScenarioTarget target = new ScenarioTarget();

        [ScenarioLabel("Point")]
        [SerializeField] private ScenarioPoint point = new ScenarioPoint(ScenarioPointKind.Object);

        [Tooltip("Also turn the object the same way as the point's object")]
        [ScenarioLabel("Match Point Rotation")]
        [SerializeField] private bool copiesRotation;


        public ScenarioTarget Target => target;

        public ScenarioPoint Point => point;

        public override string Summary => $"{target.Summary} → {point.Summary}";


        protected override void Apply(ScenarioRun run)
        {
            var moved = target.ResolveRequired(run);

            if (!point.TryGetPosition(run, out var position))
                throw new InvalidOperationException($"Point “{point.Summary}” not found");

            var pointTransform = copiesRotation ? point.ResolveTransform(run) : null;
            var rotation = pointTransform != null ? pointTransform.rotation : moved.transform.rotation;

            ScenarioTeleport.Place(moved, position, rotation);
        }
    }


    /// <summary>Перенос объекта, который не откатывает физика: Character Controller выключается на перенос, Rigidbody — следом.</summary>
    public static class ScenarioTeleport
    {
        public static void Place(GameObject moved, Vector3 position, Quaternion rotation)
        {
            var controller = moved.GetComponent<CharacterController>();
            var wasControllerEnabled = controller != null && controller.enabled;

            if (wasControllerEnabled)
                controller.enabled = false;

            moved.transform.SetPositionAndRotation(position, rotation);

            if (moved.TryGetComponent<Rigidbody>(out var body))
            {
                body.position = position;
                body.rotation = rotation;
            }

            if (wasControllerEnabled)
                controller.enabled = true;
        }
    }
}
