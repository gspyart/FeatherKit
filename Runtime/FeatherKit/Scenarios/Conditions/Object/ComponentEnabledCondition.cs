using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Компонент включён: свет, коллайдер, рендерер, любой Behaviour. Объект выключен — компонент тоже не считается включённым.</summary>
    [Serializable]
    [ScenarioBlock("Object/Component Enabled", Description = "A component is switched on and its object is active")]
    public class ComponentEnabledCondition : ScenarioCondition
    {
        [Tooltip("Drag the component itself")]
        [ScenarioLabel("Component")]
        [SerializeField] private ScenarioValue<Component> component = new ScenarioValue<Component>();


        public ScenarioValue<Component> CheckedComponent => component;

        public override string Summary => component.UsesVariable ? "graph variable"
            : component.Value != null ? component.Value.GetType().Name : "no component selected";


        protected override bool Check(ScenarioRun run)
        {
            var checkedComponent = component.Get(run);

            if (checkedComponent == null)
                throw new InvalidOperationException("No component selected");

            return checkedComponent.gameObject.activeInHierarchy && SwitchableComponents.IsEnabled(checkedComponent);
        }
    }
}
