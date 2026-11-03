using System;
using UnityEngine.Events;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Вызвать методы компонентов через обычный UnityEvent — запасной выход,
    /// когда готового блока нет. Переименованный метод тут теряется молча: частое — в свой блок.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Code/Call Methods (UnityEvent)")]
    public class UnityEventAction : ScenarioInstantAction
    {
        [ScenarioLabel("Calls")]
        [UnityEngine.SerializeField] private UnityEvent calls = new UnityEvent();


        public override string Summary
        {
            get
            {
                var count = calls.GetPersistentEventCount();

                return count == 0 ? "empty" : $"calls: {count}";
            }
        }


        protected override void Apply(ScenarioRun run)
        {
            calls.Invoke();
        }
    }
}
