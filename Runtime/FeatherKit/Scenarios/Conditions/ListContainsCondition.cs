using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>В списке есть объект. Объект и его компоненты считаются одним: ищем лампу — подходит и её объект.</summary>
    [Serializable]
    [ScenarioBlock("Lists/List Contains", Description = "The list has this object")]
    public class ListContainsCondition : ScenarioCondition
    {
        [ScenarioLabel("List")]
        [SerializeField] private ScenarioObjectList<Object> list = new ScenarioObjectList<Object>();

        [ScenarioLabel("Object")]
        [SerializeField] private ScenarioValue<Object> item = new ScenarioValue<Object>();


        public ScenarioObjectList<Object> List => list;

        public ScenarioValue<Object> Item => item;


        protected override bool Check(ScenarioRun run)
        {
            var sought = item.Get(run);

            foreach (var listed in list.Get(run))
            {
                if (ScenarioObjectMatching.IsSameObject(listed, sought))
                    return true;
            }

            return false;
        }
    }
}
