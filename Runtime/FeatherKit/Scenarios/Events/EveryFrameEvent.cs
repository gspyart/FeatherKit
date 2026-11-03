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
    [ScenarioBlock("Object/Every Frame")]
    public class EveryFrameEvent : ScenarioEvent
    {
        [Tooltip("How often, in seconds of game time. 0 — every frame")]
        [Min(0f)]
        [ScenarioLabel("Interval, s")]
        [SerializeField] private float interval;

        [NonSerialized] private CoroutineToken ticking;


        public override string Summary => interval > 0f ? $"every {interval:0.##} s" : "every frame";


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
