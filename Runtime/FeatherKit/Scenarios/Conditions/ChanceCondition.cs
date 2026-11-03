using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Случайно, с заданным шансом: при каждой проверке бросок заново.</summary>
    [Serializable]
    [ScenarioBlock("Logic/Chance")]
    public class ChanceCondition : ScenarioCondition
    {
        [Tooltip("Chance in percent: 0 — never, 100 — always")]
        [Range(0f, 100f)]
        [ScenarioLabel("Chance, %")]
        [SerializeField] private float percent = 50f;


        public override string Summary => $"{percent:0.#}%";


        protected override bool Check(ScenarioRun run)
        {
            return UnityEngine.Random.value * 100f < percent;
        }
    }
}
