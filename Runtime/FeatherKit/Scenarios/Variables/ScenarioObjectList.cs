using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Поле блока «список объектов»: свой список или переменная Object List графа.
    /// Объекты сцены в граф-файле живут только через переменную и привязку на объекте.
    /// </summary>
    [Serializable]
    public class ScenarioObjectList<T> where T : Object
    {
        [SerializeField] private List<T> items = new List<T>();

        [Tooltip("The graph's Object List variable to use instead of the own list. Empty — the own list is used")]
        [SerializeField] private string variableId;

        // Переведённый в T список переменной: один на поле, перезаписывается следующим Get.
        [NonSerialized] private List<T> convertedItems;


        public ScenarioObjectList()
        {
        }

        public ScenarioObjectList(IEnumerable<T> items)
        {
            this.items = new List<T>(items);
        }


        /// <summary>Свой список — то, что лежит в поле, когда переменная не выбрана.</summary>
        public List<T> Items => items;

        public string VariableId
        {
            get => variableId;
            set => variableId = value;
        }

        public bool UsesVariable => !string.IsNullOrEmpty(variableId);


        public static ScenarioObjectList<T> FromVariable(string graphVariableId)
        {
            return new ScenarioObjectList<T> { variableId = graphVariableId };
        }


        /// <summary>
        /// Список для запуска: свой или переменной графа, объекты переведены в T.
        /// Список переменной живёт до следующего Get этого поля — обходить надо копию, если между делом ждёшь.
        /// </summary>
        public IReadOnlyList<T> Get(ScenarioRun run)
        {
            if (!UsesVariable)
                return items;

            var converted = run.Scenario.GraphVariables.Get<List<T>>(variableId);

            convertedItems ??= new List<T>();
            convertedItems.Clear();
            convertedItems.AddRange(converted);

            return convertedItems;
        }
    }
}
