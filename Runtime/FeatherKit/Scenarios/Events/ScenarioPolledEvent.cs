using System;
using System.Collections;
using FeatherKit.Coroutines;
using UnityEngine;
using AppContext = FeatherKit.Contexts.AppContext;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Событие, которое само проверяет мир раз в Check Interval, пока подписано: взгляд, расстояние.
    /// - опрос идёт на общем раннере корутин, без выделения памяти в каждой проверке
    /// - проверки идут от запуска-«щупа» сценария: «This Object» и переменные графа есть, «Caller» — нет
    /// - на паузе игры (Time.timeScale = 0) проверок нет: Hold Time и пересечения не копятся, пока игра стоит
    /// </summary>
    [Serializable]
    public abstract class ScenarioPolledEvent : ScenarioEvent
    {
        [Tooltip("How often to check, in seconds of game time. 0 — every frame")]
        [Min(0f)]
        [ScenarioLabel("Check Interval, s")]
        [ScenarioInspectorOnly]
        [SerializeField] private float checkInterval = 0.1f;

        [NonSerialized] private CoroutineToken polling;
        [NonSerialized] private ScenarioRun probeRun;


        public float CheckInterval
        {
            get => checkInterval;
            set => checkInterval = Mathf.Max(0f, value);
        }


        protected sealed override void OnAttached()
        {
            probeRun = new ScenarioRun(Scenario, null);
            ResetPollState();
            polling = AppContext.Current.Coroutines.Run(Poll());
        }

        protected sealed override void OnReleased()
        {
            polling.Stop();
            probeRun = null;
        }

        // Первая проверка — после первого интервала: на включении объекта мир ещё может расставляться.
        // На паузе WaitForSeconds стоит сам, а «каждый кадр» — нет: паузу пропускаем явно.
        private IEnumerator Poll()
        {
            var wait = checkInterval > 0f ? new WaitForSeconds(checkInterval) : null;

            while (probeRun != null)
            {
                yield return wait;

                if (probeRun != null && Time.timeScale > 0f)
                    CheckNow(probeRun);
            }
        }


        /// <summary>Забыть прошлые проверки: подписка началась заново.</summary>
        protected abstract void ResetPollState();

        /// <summary>Одна проверка. Зовёт Fire, когда пора.</summary>
        protected abstract void CheckNow(ScenarioRun probe);
    }
}
