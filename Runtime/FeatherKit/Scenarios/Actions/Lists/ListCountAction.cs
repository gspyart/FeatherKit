using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>Сколько объектов в списке — в переменную Counter.</summary>
    [Serializable]
    [ScenarioBlock("Lists/List Count", Description = "Put the number of objects in a list into a Counter")]
    public class ListCountAction : ScenarioInstantAction
    {
        [ScenarioLabel("List")]
        [SerializeField] private ScenarioObjectList<Object> list = new ScenarioObjectList<Object>();

        [ScenarioLabel("Count To")]
        [SerializeField] private ScenarioVariableSlot<int> count = new ScenarioVariableSlot<int>();


        public ScenarioObjectList<Object> List => list;

        public ScenarioVariableSlot<int> Count => count;


        protected override void Apply(ScenarioRun run)
        {
            count.Set(run, list.Get(run).Count);
        }
    }
}
