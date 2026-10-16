using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Пауза между шагами. Игровое время: на паузе игры ожидание тоже стоит.</summary>
    [Serializable]
    [ScenarioBlock("Управление/Подождать")]
    public class WaitAction : ScenarioAction
    {
        [Tooltip("Сколько секунд ждать")]
        [Min(0f)]
        [ScenarioLabel("Секунд")]
        [SerializeField] private float seconds = 1f;


        public override string Summary => $"{seconds:0.##} с";


        public override IEnumerator Perform(ScenarioRun run)
        {
            if (seconds > 0f)
                yield return new WaitForSeconds(seconds);
        }
    }
}
