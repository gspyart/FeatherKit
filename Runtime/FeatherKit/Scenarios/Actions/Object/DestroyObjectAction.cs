using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>Удалить объект. Удалить объект самого сценария — оборвать сценарий: шаги после этого не выполнятся.</summary>
    [Serializable]
    [ScenarioBlock("Object/Destroy", Description = "Remove an object from the scene")]
    public class DestroyObjectAction : ScenarioInstantAction
    {
        [ScenarioLabel("What to Destroy")]
        [SerializeField] private ScenarioTarget target = new ScenarioTarget();


        public ScenarioTarget Target => target;

        public override string Summary => target.Summary;


        protected override void Apply(ScenarioRun run)
        {
            Object.Destroy(target.ResolveRequired(run));
        }
    }
}
