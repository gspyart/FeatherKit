using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Переменная графа любого вида изменилась — блоком, другим запуском или списком на месте.</summary>
    [Serializable]
    [ScenarioBlock("Variables/Variable Changed", Description = "Fires when a graph variable gets a new value")]
    public class VariableChangedEvent : ScenarioEvent
    {
        [ScenarioLabel("Variable")]
        [SerializeField] private ScenarioVariableChoice variable = new ScenarioVariableChoice();

        // Переменные того экземпляра графа, где событие подписано: отписываться надо от них же.
        [NonSerialized] private ScenarioGraphVariables listenedVariables;


        public ScenarioVariableChoice Variable => variable;

        public override string Summary => variable.IsChosen ? null : "no variable chosen";


        protected override void OnAttached()
        {
            listenedVariables = Scenario.GraphVariables;
            listenedVariables.Changed += HandleVariableChanged;
        }

        protected override void OnReleased()
        {
            if (listenedVariables != null)
                listenedVariables.Changed -= HandleVariableChanged;

            listenedVariables = null;
        }

        private void HandleVariableChanged(ScenarioVariableDefinition changedVariable)
        {
            if (changedVariable.Id == variable.VariableId)
                Fire(null);
        }
    }
}
