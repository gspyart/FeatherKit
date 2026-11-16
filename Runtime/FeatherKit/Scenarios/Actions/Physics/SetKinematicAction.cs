using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Включить или выключить Is Kinematic у Rigidbody: висевший ящик падает, упавший замирает.</summary>
    [Serializable]
    [ScenarioBlock("Physics/Set Kinematic", Description = "Switch a Rigidbody between moved by physics and held in place")]
    public class SetKinematicAction : ScenarioInstantAction
    {
        [Tooltip("The object with the Rigidbody")]
        [ScenarioLabel("Object")]
        [SerializeField] private ScenarioTarget target = new ScenarioTarget();

        [Tooltip("Turn On — the body is held in place; Turn Off — physics moves it")]
        [ScenarioLabel("Kinematic")]
        [SerializeField] private ScenarioSwitch change;


        public ScenarioTarget Target => target;

        public override string Summary => $"{change.Describe()} kinematic · {target.Summary}";


        protected override void Apply(ScenarioRun run)
        {
            var bodyObject = target.ResolveRequired(run);

            if (!bodyObject.TryGetComponent<Rigidbody>(out var body))
                throw new InvalidOperationException($"“{bodyObject.name}” has no Rigidbody");

            body.isKinematic = change.StateAfter(body.isKinematic);
        }
    }
}
