using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>Убрать объект из переменной-списка графа — все его повторы. Нет в списке — ничего не происходит.</summary>
    [Serializable]
    [ScenarioBlock("Lists/Remove from List", Description = "Remove an object from a graph Object List")]
    public class RemoveFromListAction : ScenarioInstantAction
    {
        [ScenarioLabel("List")]
        [SerializeField] private ScenarioVariableSlot<List<Object>> list = new ScenarioVariableSlot<List<Object>>();

        [ScenarioLabel("Object")]
        [SerializeField] private ScenarioValue<Object> item = new ScenarioValue<Object>();


        public ScenarioVariableSlot<List<Object>> List => list;

        public ScenarioValue<Object> Item => item;

        public override string Summary => item.UsesVariable ? "graph variable" : item.Value != null ? item.Value.name : "no object selected";


        protected override void Apply(ScenarioRun run)
        {
            var liveList = list.GetLive(run);
            var removedCount = ScenarioObjectMatching.RemoveMatches(liveList, item.Get(run));

            if (removedCount > 0)
                list.MarkChanged(run);
        }
    }
}
