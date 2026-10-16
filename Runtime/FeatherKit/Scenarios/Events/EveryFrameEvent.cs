using System;
using System.Collections;
using FeatherKit.Coroutines;
using UnityEngine;
using AppContext = FeatherKit.Contexts.AppContext;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Запуск каждый кадр или раз в несколько секунд, пока объект сценария включён, — как Update.
    /// Обычно в паре с условиями: сработает в тот кадр, когда они сойдутся. Идущий сценарий не перезапускается.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Объект/Каждый кадр")]
    public class EveryFrameEvent : ScenarioEvent
    {
        [Tooltip("Как часто, секунды игрового времени. 0 — каждый кадр")]
        [Min(0f)]
        [ScenarioLabel("Интервал, с")]
        [SerializeField] private float interval;

        [NonSerialized] private CoroutineToken ticking;


        public override string Summary => interval > 0f ? $"раз в {interval:0.##} с" : "каждый кадр";


        protected override void OnAttached()
        {
            ticking = AppContext.Current.Coroutines.Run(FireRepeatedly());
        }

        protected override void OnReleased()
        {
            ticking.Stop();
        }

        private IEnumerator FireRepeatedly()
        {
            var wait = interval > 0f ? new WaitForSeconds(interval) : null;

            while (Scenario != null)
            {
                yield return wait;

                Fire(null);
            }
        }
    }
}
