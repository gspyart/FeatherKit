using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Включить или выключить один компонент: свет, коллайдер, рендерер, скрипт.
    /// Объект остаётся включённым.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Объект/Включить или выключить компонент")]
    public class SetComponentEnabledAction : ScenarioInstantAction
    {
        [Tooltip("Компонент, который включается и выключается: свет, коллайдер, рендерер, скрипт. " +
                 "Перетащите сюда сам компонент из инспектора")]
        [ScenarioLabel("Компонент")]
        [SerializeField] private Component component;


        [ScenarioLabel("Действие")]
        [SerializeField] private ScenarioSwitch change;


        public override string Summary => component != null
            ? $"{change.Describe()} {component.GetType().Name} на {component.name}"
            : "компонент не выбран";


        protected override void Apply(ScenarioRun run)
        {
            SwitchableComponents.SetEnabled(component, change.StateAfter(SwitchableComponents.IsEnabled(component)));
        }
    }
}
