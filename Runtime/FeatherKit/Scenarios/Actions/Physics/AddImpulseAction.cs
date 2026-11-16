using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Толкнуть Rigidbody разом: в заданном направлении, от точки или к точке — ящик слетает с полки.</summary>
    [Serializable]
    [ScenarioBlock("Physics/Add Impulse", Description = "Push a Rigidbody at once")]
    public class AddImpulseAction : ScenarioInstantAction
    {
        [Tooltip("The object with the Rigidbody")]
        [ScenarioLabel("What to Push")]
        [SerializeField] private ScenarioTarget target = new ScenarioTarget();

        [ScenarioLabel("Direction")]
        [SerializeField] private ScenarioImpulseDirection directionMode;

        [Tooltip("Direction in world axes, for “World Direction”")]
        [ScenarioLabel("World Direction")]
        [SerializeField] private Vector3 worldDirection = Vector3.up;

        [Tooltip("The point to push away from or toward")]
        [ScenarioLabel("Point")]
        [SerializeField] private ScenarioPoint point = new ScenarioPoint(ScenarioPointKind.Player);

        [Tooltip("Impulse strength: mass × change of speed, N·s")]
        [ScenarioLabel("Strength")]
        [SerializeField] private ScenarioValue<float> strength = new ScenarioValue<float>(5f);


        public ScenarioTarget Target => target;

        public ScenarioPoint Point => point;

        public ScenarioValue<float> Strength => strength;

        public override string Summary => directionMode switch
        {
            ScenarioImpulseDirection.WorldDirection => $"{target.Summary} · {worldDirection}",
            ScenarioImpulseDirection.AwayFromPoint => $"{target.Summary} · away from {point.Summary}",
            _ => $"{target.Summary} · toward {point.Summary}"
        };


        protected override void Apply(ScenarioRun run)
        {
            var pushed = target.ResolveRequired(run);

            if (!pushed.TryGetComponent<Rigidbody>(out var body))
                throw new InvalidOperationException($"“{pushed.name}” has no Rigidbody");

            body.AddForce(DirectionFor(run, body.position) * strength.Get(run), ForceMode.Impulse);
        }

        private Vector3 DirectionFor(ScenarioRun run, Vector3 bodyPosition)
        {
            if (directionMode == ScenarioImpulseDirection.WorldDirection)
                return worldDirection.normalized;

            if (!point.TryGetPosition(run, out var pointPosition))
                throw new InvalidOperationException($"Point “{point.Summary}” not found");

            var away = (bodyPosition - pointPosition).normalized;

            return directionMode == ScenarioImpulseDirection.AwayFromPoint ? away : -away;
        }
    }


    /// <summary>Куда толкать. Новые — только в конец: в данных хранится число.</summary>
    public enum ScenarioImpulseDirection
    {
        [InspectorName("World Direction")] WorldDirection,
        [InspectorName("Away From Point")] AwayFromPoint,
        [InspectorName("Toward Point")] TowardPoint
    }
}
