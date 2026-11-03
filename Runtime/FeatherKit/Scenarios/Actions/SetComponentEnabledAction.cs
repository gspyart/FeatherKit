using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Включить или выключить один компонент: свет, коллайдер, рендерер, скрипт.
    /// Объект остаётся включённым.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Object/Enable or Disable Component")]
    public class SetComponentEnabledAction : ScenarioInstantAction
    {
        [Tooltip("A component that can be enabled and disabled: a light, collider, renderer, script. " +
                 "Drag the component itself here from the Inspector")]
        [ScenarioLabel("Component")]
        [SerializeField] private Component component;


        [ScenarioLabel("Action")]
        [SerializeField] private ScenarioSwitch change;


        public override string Summary => component != null
            ? $"{change.Describe()} {component.GetType().Name} on {component.name}"
            : "no component selected";


        protected override void Apply(ScenarioRun run)
        {
            SwitchableComponents.SetEnabled(component, change.StateAfter(SwitchableComponents.IsEnabled(component)));
        }
    }
}
