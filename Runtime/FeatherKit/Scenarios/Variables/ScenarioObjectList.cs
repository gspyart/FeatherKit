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


        /// <summary>Живые объекты для запуска: свои или переменной графа, переведённые в T; уничтоженные пропускаются. Новый список на каждый вызов — можно держать через ожидания.</summary>
        public IReadOnlyList<T> Get(ScenarioRun run)
        {
            var list = new List<T>();
            GetInto(run, list);

            return list;
        }

        /// <summary>То же без выделения памяти: список заполняется заново.</summary>
        public void GetInto(ScenarioRun run, List<T> target)
        {
            target.Clear();

            if (!UsesVariable)
            {
                foreach (var item in items)
                {
                    if (item != null)
                        target.Add(item);
                }

                return;
            }

            var variables = run.Scenario.GraphVariables;

            foreach (var storedObject in variables.GetObjectListLive(variableId))
                target.Add((T)variables.ConvertObject(variableId, storedObject, typeof(T)));
        }
    }
}
