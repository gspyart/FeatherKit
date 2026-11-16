using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Один случай ноды Switch on Counter: значение счётчика и свой выход.
    /// Id постоянный, выдаётся при добавлении: поменяли число — провод к выходу остаётся.
    /// </summary>
    [Serializable]
    public class ScenarioSwitchCase
    {
        public const string OutputIdPrefix = "case.";

        [SerializeField] private string id;
        [SerializeField] private int value;


        public ScenarioSwitchCase(int value)
        {
            id = ScenarioWaitCase.CreateId();
            this.value = value;
        }


        public string Id => id;

        public int Value
        {
            get => value;
            set => this.value = value;
        }

        /// <summary>Выход случая. Не хранится: собирается из id, подпись — значение.</summary>
        public ScenarioOutput Output => new ScenarioOutput(OutputIdPrefix + id, value.ToString());


        // Только для ноды: провода старого id после этого выход не найдут.
        internal void AssignNewId()
        {
            id = ScenarioWaitCase.CreateId();
        }
    }
}
