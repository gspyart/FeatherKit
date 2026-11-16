using System;
using System.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «For Each»: по очереди кладёт каждый объект списка в переменную и пускает Each, ждёт всё выросшее; затем Done.
    /// Обходит копию списка: правка списка посреди обхода на этот обход не влияет.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Flow/For Each", Description = "Run “Each” once for every object of a list, one after another")]
    public class ScenarioForEachNode : ScenarioFlowNode
    {
        public static readonly ScenarioOutput Each = new ScenarioOutput("each", "Each");
        public static readonly ScenarioOutput Done = new ScenarioOutput("done", "Done");

        [ScenarioLabel("List")]
        [SerializeField] private ScenarioObjectList<Object> list = new ScenarioObjectList<Object>();

        [Tooltip("The graph variable that gets the current object before each “Each”")]
        [ScenarioLabel("Current Item")]
        [SerializeField] private ScenarioVariableSlot<Object> item = new ScenarioVariableSlot<Object>();


        public ScenarioObjectList<Object> List => list;

        public ScenarioVariableSlot<Object> Item => item;


        protected override IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            // Get отдаёт новый список: правка переменной посреди обхода этот обход не меняет.
            foreach (var listItem in list.Get(run))
            {
                // Пока тело шло, объект могли уничтожить: мёртвому Each не нужен.
                if (listItem == null)
                    continue;

                if (item.IsChosen)
                    item.Set(run, listItem);

                yield return FireAndWait(run, Each);
            }

            Fire(run, Done);
        }
    }
}
