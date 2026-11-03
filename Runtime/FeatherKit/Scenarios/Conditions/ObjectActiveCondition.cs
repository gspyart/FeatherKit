using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Объект включён на сцене — сам и все его родители.</summary>
    [Serializable]
    [ScenarioBlock("Object/Object Is Active")]
    public class ObjectActiveCondition : ScenarioCondition
    {
        [Tooltip("Which object to check")]
        [ScenarioLabel("Object")]
        [SerializeField] private ScenarioTarget target = new ScenarioTarget();


        public override string Summary => target.Summary;


        protected override bool Check(ScenarioRun run)
        {
            var targetObject = target.Resolve(run);

            return targetObject != null && targetObject.activeInHierarchy;
        }
    }
}
