using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Поле блока «куда записать»: переменная графа, в которую блок кладёт результат — число, объект, точку.
    /// Своего значения нет — только переменная; не выбрана — блоку некуда писать.
    /// </summary>
    [Serializable]
    public class ScenarioVariableSlot<T>
    {
        [Tooltip("The graph variable this block writes to")]
        [SerializeField] private string variableId;


        public string VariableId
        {
            get => variableId;
            set => variableId = value;
        }

        /// <summary>Переменная выбрана — блоку есть куда писать.</summary>
        public bool IsChosen => !string.IsNullOrEmpty(variableId);


        public static ScenarioVariableSlot<T> FromVariable(string graphVariableId)
        {
            return new ScenarioVariableSlot<T> { variableId = graphVariableId };
        }


        public T Get(ScenarioRun run)
        {
            return run.Scenario.GraphVariables.Get<T>(RequireChosen());
        }

        public void Set(ScenarioRun run, T value)
        {
            run.Scenario.GraphVariables.Set(RequireChosen(), value);
        }


        /// <summary>Живой список переменной Object List — блоки списков меняют его на месте, затем MarkChanged.</summary>
        public List<Object> GetLive(ScenarioRun run)
        {
            return run.Scenario.GraphVariables.GetObjectListLive(RequireChosen());
        }

        /// <summary>Значение поменяли на месте, мимо Set: сообщить слушателям «Variable Changed».</summary>
        public void MarkChanged(ScenarioRun run)
        {
            run.Scenario.GraphVariables.MarkChanged(RequireChosen());
        }


        private string RequireChosen()
        {
            if (!IsChosen)
                throw new InvalidOperationException("No variable chosen to write to");

            return variableId;
        }
    }
}
