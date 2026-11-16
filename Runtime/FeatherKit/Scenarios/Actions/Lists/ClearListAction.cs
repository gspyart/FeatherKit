using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>Очистить переменную-список графа.</summary>
    [Serializable]
    [ScenarioBlock("Lists/Clear List", Description = "Remove every object from a graph Object List")]
    public class ClearListAction : ScenarioInstantAction
    {
        [ScenarioLabel("List")]
        [SerializeField] private ScenarioVariableSlot<List<Object>> list = new ScenarioVariableSlot<List<Object>>();


        public ScenarioVariableSlot<List<Object>> List => list;


        protected override void Apply(ScenarioRun run)
        {
            var liveList = list.GetLive(run);

            if (liveList.Count == 0)
                return;

            liveList.Clear();
            list.MarkChanged(run);
        }
    }
}
