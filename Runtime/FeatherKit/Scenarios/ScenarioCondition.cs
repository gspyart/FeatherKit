using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// «If» сценария: одна проверка «да или нет» в момент запуска.
    /// Галка «NOT» у каждого условия переворачивает ответ.
    /// </summary>
    [Serializable]
    public abstract class ScenarioCondition
    {
        [Tooltip("Inverted: the condition is met when the check does NOT pass")]
        [SerializeField] private bool isInverted;


        public bool IsInverted => isInverted;

        /// <summary>Короткая подпись настроек для заголовка блока в инспекторе. Null — без неё.</summary>
        public virtual string Summary => null;


        public bool IsMet(ScenarioRun run)
        {
            return Check(run) != isInverted;
        }


        protected abstract bool Check(ScenarioRun run);
    }


    public enum ScenarioConditionsMode
    {
        [InspectorName("All Conditions")] All,
        [InspectorName("At Least One")] Any
    }
}
