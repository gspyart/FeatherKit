using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>Добавить объект в переменную-список графа. Без «Allow Repeats» уже лежащий в списке второй раз не кладётся.</summary>
    [Serializable]
    [ScenarioBlock("Lists/Add to List", Description = "Add an object to a graph Object List")]
    public class AddToListAction : ScenarioInstantAction
    {
        [ScenarioLabel("List")]
        [SerializeField] private ScenarioVariableSlot<List<Object>> list = new ScenarioVariableSlot<List<Object>>();

        [ScenarioLabel("Object")]
        [SerializeField] private ScenarioValue<Object> item = new ScenarioValue<Object>();

        [Tooltip("Add the object even if the list already has it")]
        [ScenarioLabel("Allow Repeats")]
        [SerializeField] private bool allowsRepeats;


        public ScenarioVariableSlot<List<Object>> List => list;

        public ScenarioValue<Object> Item => item;

        public override string Summary => item.UsesVariable ? "graph variable" : item.Value != null ? item.Value.name : "no object selected";


        protected override void Apply(ScenarioRun run)
        {
            var added = item.Get(run);

            if (added == null)
                throw new InvalidOperationException("No object to add");

            var liveList = list.GetLive(run);
            var stored = ScenarioGraphVariables.ConvertForStorage(ListDefinition(run), added);

            if (!allowsRepeats && liveList.Contains(stored))
                return;

            liveList.Add(stored);
            list.MarkChanged(run);
        }

        private ScenarioVariableDefinition ListDefinition(ScenarioRun run)
        {
            run.Scenario.GraphVariables.TryGetDefinition(list.VariableId, out var definition);

            return definition;
        }
    }
}
