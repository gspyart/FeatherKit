using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Включить или выключить объект целиком, со всеми детьми.</summary>
    [Serializable]
    [ScenarioBlock("Object/Activate or Deactivate Object")]
    public class SetObjectActiveAction : ScenarioInstantAction
    {
        [Tooltip("Which object to activate or deactivate")]
        [ScenarioLabel("Object")]
        [SerializeField] private ScenarioTarget target = new ScenarioTarget();


        [ScenarioLabel("Action")]
        [SerializeField] private ScenarioSwitch change;


        public override string Summary => $"{change.Describe()} {target.Summary}";


        protected override void Apply(ScenarioRun run)
        {
            var targetObject = target.ResolveRequired(run);

            targetObject.SetActive(change.StateAfter(targetObject.activeSelf));
        }
    }
}
