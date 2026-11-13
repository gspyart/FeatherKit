using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Поле блока: своё значение или переменная графа.
    /// Объекты сцены в граф-файле живут только так — через открытую переменную и привязку на объекте.
    /// </summary>
    [Serializable]
    public class ScenarioValue<T>
    {
        [SerializeField] private T value;

        [Tooltip("The graph variable to read instead of the value. Empty — the value is used")]
        [SerializeField] private string variableId;


        public ScenarioValue()
        {
        }

        public ScenarioValue(T value)
        {
            this.value = value;
        }


        /// <summary>Своё значение — то, что лежит в поле, когда переменная не выбрана.</summary>
        public T Value
        {
            get => value;
            set => this.value = value;
        }

        public string VariableId
        {
            get => variableId;
            set => variableId = value;
        }

        public bool UsesVariable => !string.IsNullOrEmpty(variableId);


        public static ScenarioValue<T> FromVariable(string graphVariableId)
        {
            return new ScenarioValue<T> { variableId = graphVariableId };
        }

        public static implicit operator ScenarioValue<T>(T value)
        {
            return new ScenarioValue<T>(value);
        }


        /// <summary>Значение для запуска: своё или переменной графа того экземпляра, чей это запуск.</summary>
        public T Get(ScenarioRun run)
        {
            return UsesVariable ? run.Scenario.GraphVariables.Get<T>(variableId) : value;
        }
    }
}
