using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Случайно, с заданным шансом: при каждой проверке бросок заново.</summary>
    [Serializable]
    [ScenarioBlock("Логика/Шанс")]
    public class ChanceCondition : ScenarioCondition
    {
        [Tooltip("Шанс в процентах: 0 — никогда, 100 — всегда")]
        [Range(0f, 100f)]
        [ScenarioLabel("Шанс, %")]
        [SerializeField] private float percent = 50f;


        public override string Summary => $"{percent:0.#}%";


        protected override bool Check(ScenarioRun run)
        {
            return UnityEngine.Random.value * 100f < percent;
        }
    }
}
