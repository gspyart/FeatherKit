using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Запомнить, где сейчас точка, — в переменную Vector3: потом «Set Position» вернёт туда объект.</summary>
    [Serializable]
    [ScenarioBlock("Space/Remember Position", Description = "Store where a point is right now in a Vector3 variable")]
    public class RememberPositionAction : ScenarioInstantAction
    {
        [ScenarioLabel("Point")]
        [SerializeField] private ScenarioPoint point = new ScenarioPoint(ScenarioPointKind.Player);

        [ScenarioLabel("Put Into")]
        [SerializeField] private ScenarioVariableSlot<Vector3> position = new ScenarioVariableSlot<Vector3>();


        public ScenarioPoint Point => point;

        public ScenarioVariableSlot<Vector3> Position => position;

        public override string Summary => point.Summary;


        protected override void Apply(ScenarioRun run)
        {
            if (!point.TryGetPosition(run, out var worldPosition))
                throw new InvalidOperationException($"Point “{point.Summary}” not found");

            position.Set(run, worldPosition);
        }
    }
}
