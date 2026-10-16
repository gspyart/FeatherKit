using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// «Если» сценария: одна проверка «да или нет» в момент запуска.
    /// Галка «НЕ» у каждого условия переворачивает ответ.
    /// </summary>
    [Serializable]
    public abstract class ScenarioCondition
    {
        [Tooltip("Наоборот: условие выполнено, когда проверка НЕ проходит")]
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
        [InspectorName("Все условия")] All,
        [InspectorName("Хотя бы одно")] Any
    }
}
