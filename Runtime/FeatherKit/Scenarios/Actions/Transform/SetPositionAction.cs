using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Поставить объект в точку сразу — например, туда, что запомнил «Remember Position».
    /// Агент навигации, Character Controller и Rigidbody переносятся честно, без отката на старое место.
    /// Для игрока — «Teleport Player» игры: он заодно обновляет камеру и состояние движения.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Transform/Set Position", Description = "Put an object at a point at once")]
    public class SetPositionAction : ScenarioInstantAction
    {
        [Tooltip("Which object to move. For the player use the game's “Teleport Player”")]
        [ScenarioLabel("What to Move")]
        [SerializeField] private ScenarioTarget target = new ScenarioTarget();

        [ScenarioLabel("Point")]
        [SerializeField] private ScenarioPoint point = new ScenarioPoint(ScenarioPointKind.Object);

        [Tooltip("Also turn the object the same way as the point's object")]
        [ScenarioLabel("Match Point Rotation")]
        [SerializeField] private bool copiesRotation;

        [Tooltip("Stop a Rigidbody after the move: it does not keep flying the way it went")]
        [ScenarioLabel("Reset Velocity")]
        [SerializeField] private bool resetsVelocity = true;


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

            ScenarioTeleport.Place(moved, position, rotation, resetsVelocity);
        }
    }
}
