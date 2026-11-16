using System;
using FeatherKit.Logging;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Точка внутри зоны-коллайдера: коробки, шара, капсулы или выпуклого меша. Зона по умолчанию — коллайдер объекта сценария.
    /// Невыпуклый меш проверяется по габаритам — грубо, с предупреждением один раз.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Space/Inside Zone", Description = "A point is inside a collider zone")]
    public class InsideZoneCondition : ScenarioCondition
    {
        // Ближе — уже внутри: ClosestPoint точки внутри возвращает её саму, с погрешностью округления.
        private const float InsideTolerance = 0.001f;

        [ScenarioLabel("Who")]
        [SerializeField] private ScenarioPoint who = new ScenarioPoint(ScenarioPointKind.Player);

        [Tooltip("The zone. Empty — the collider on the scenario object")]
        [ScenarioLabel("Zone")]
        [SerializeField] private ScenarioValue<Collider> zone = new ScenarioValue<Collider>();

        [NonSerialized] private bool hasWarnedConcaveMesh;


        public ScenarioPoint Who => who;

        public ScenarioValue<Collider> Zone => zone;

        public override string Summary => $"{who.Summary} in " + (zone.UsesVariable ? "graph variable"
            : zone.Value != null ? zone.Value.name : "this object");


        protected override bool Check(ScenarioRun run)
        {
            var zoneCollider = run.ComponentOrOwners(zone.Get(run));

            if (zoneCollider == null)
                throw new InvalidOperationException("No zone collider");

            if (!zoneCollider.enabled || !zoneCollider.gameObject.activeInHierarchy || !who.TryGetPosition(run, out var position))
                return false;

            if (zoneCollider is MeshCollider { convex: false })
            {
                WarnConcaveMeshOnce(zoneCollider);
                return zoneCollider.bounds.Contains(position);
            }

            return (zoneCollider.ClosestPoint(position) - position).sqrMagnitude < InsideTolerance * InsideTolerance;
        }

        private void WarnConcaveMeshOnce(Collider zoneCollider)
        {
            if (hasWarnedConcaveMesh)
                return;

            hasWarnedConcaveMesh = true;
            FLog.Warning(zoneCollider, ("Inside Zone: ", FLogColor.Bad), (zoneCollider.name, FLogColor.Value),
                (" is a non-convex mesh — checked by its bounds only. Tick Convex or use a box", FLogColor.Bad));
        }
    }
}
