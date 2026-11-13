using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Привязка: значение открытой переменной графа на этом объекте — «Set on Object».
    /// Так граф-файл получает объекты сцены: у каждого Scenario свои привязки.
    /// </summary>
    [Serializable]
    public class ScenarioVariableBinding
    {
        [SerializeField] private string variableId;
        [SerializeField] private ScenarioVariableValue value = new ScenarioVariableValue();


        public ScenarioVariableBinding(string variableId)
        {
            this.variableId = variableId;
        }


        /// <summary>Id открытой переменной графа — ScenarioVariableDefinition.Id.</summary>
        public string VariableId => variableId;

        public ScenarioVariableValue Value => value;
    }
}
