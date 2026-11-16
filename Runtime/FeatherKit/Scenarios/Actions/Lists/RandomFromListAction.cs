using System;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace FeatherKit.Scenarios
{
    /// <summary>Случайный объект списка — в переменную Object. Пустой список — в переменную кладётся пусто.</summary>
    [Serializable]
    [ScenarioBlock("Lists/Random Item", Description = "Put a random object of a list into an Object variable")]
    public class RandomFromListAction : ScenarioInstantAction
    {
        [ScenarioLabel("List")]
        [SerializeField] private ScenarioObjectList<Object> list = new ScenarioObjectList<Object>();

        [ScenarioLabel("Put Into")]
        [SerializeField] private ScenarioVariableSlot<Object> item = new ScenarioVariableSlot<Object>();


        public ScenarioObjectList<Object> List => list;

        public ScenarioVariableSlot<Object> Item => item;


        protected override void Apply(ScenarioRun run)
        {
            var objects = list.Get(run);

            item.Set(run, objects.Count > 0 ? objects[Random.Range(0, objects.Count)] : null);
        }
    }
}
