using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Включить или выключить объект целиком, со всеми детьми.</summary>
    [Serializable]
    [ScenarioBlock("Объект/Включить или выключить объект")]
    public class SetObjectActiveAction : ScenarioInstantAction
    {
        [Tooltip("Какой объект включить или выключить")]
        [ScenarioLabel("Объект")]
        [SerializeField] private ScenarioTarget target = new ScenarioTarget();


        [ScenarioLabel("Действие")]
        [SerializeField] private ScenarioSwitch change;


        public override string Summary => $"{change.Describe()} {target.Summary}";


        protected override void Apply(ScenarioRun run)
        {
            var targetObject = target.ResolveRequired(run);

            targetObject.SetActive(change.StateAfter(targetObject.activeSelf));
        }
    }
}
