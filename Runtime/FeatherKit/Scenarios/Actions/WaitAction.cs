using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Пауза между шагами. Игровое время: на паузе игры ожидание тоже стоит.</summary>
    [Serializable]
    [ScenarioBlock("Control/Wait")]
    public class WaitAction : ScenarioAction
    {
        [Tooltip("How many seconds to wait")]
        [Min(0f)]
        [ScenarioLabel("Seconds")]
        [SerializeField] private float seconds = 1f;


        public override string Summary => $"{seconds:0.##} s";


        public override IEnumerator Perform(ScenarioRun run)
        {
            if (seconds > 0f)
                yield return new WaitForSeconds(seconds);
        }
    }
}
