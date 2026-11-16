using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Создать копию префаба в точке; созданный объект можно положить в переменную.
    /// Stop сценария созданное не убирает — для этого есть «Destroy».
    /// </summary>
    [Serializable]
    [ScenarioBlock("Object/Spawn Prefab", Description = "Create a copy of a prefab at a point")]
    public class SpawnPrefabAction : ScenarioInstantAction
    {
        [ScenarioLabel("Prefab")]
        [SerializeField] private ScenarioValue<GameObject> prefab = new ScenarioValue<GameObject>();

        [ScenarioLabel("At Point")]
        [SerializeField] private ScenarioPoint point = new ScenarioPoint(ScenarioPointKind.ThisObject);

        [Tooltip("Turn the copy the same way as the point's object; unchecked — as the prefab is")]
        [ScenarioLabel("Match Point Rotation")]
        [SerializeField] private bool copiesRotation = true;

        [Tooltip("The parent of the copy. Empty — the scene root")]
        [ScenarioLabel("Parent")]
        [SerializeField] private ScenarioValue<Transform> parent = new ScenarioValue<Transform>();

        [Tooltip("The graph variable that gets the created object. Empty — not stored")]
        [ScenarioLabel("Put Into")]
        [SerializeField] private ScenarioVariableSlot<GameObject> spawned = new ScenarioVariableSlot<GameObject>();


        public ScenarioValue<GameObject> Prefab => prefab;

        public ScenarioPoint Point => point;

        public ScenarioVariableSlot<GameObject> Spawned => spawned;

        public override string Summary => (prefab.UsesVariable ? "graph variable" : prefab.Value != null ? prefab.Value.name : "no prefab")
                                          + " → " + point.Summary;


        protected override void Apply(ScenarioRun run)
        {
            var source = prefab.Get(run);

            if (source == null)
                throw new InvalidOperationException("No prefab selected");

            if (!point.TryGetPosition(run, out var position))
                throw new InvalidOperationException($"Point “{point.Summary}” not found");

            var pointTransform = copiesRotation ? point.ResolveTransform(run) : null;
            var rotation = pointTransform != null ? pointTransform.rotation : source.transform.rotation;
            var copy = Object.Instantiate(source, position, rotation, parent.Get(run));

            if (spawned.IsChosen)
                spawned.Set(run, copy);
        }
    }
}
