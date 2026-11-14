using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// «When» сценария: следит за чем-то одним и сообщает слушателю — ноде When, Wait For или списку «When».
    /// Наследник подписывается в OnAttached, отписывается в OnReleased и зовёт Fire.
    /// </summary>
    [Serializable]
    public abstract class ScenarioEvent
    {
        [NonSerialized] private IScenarioEventListener listener;


        /// <summary>Короткая подпись настроек для заголовка блока в инспекторе. Null — без неё.</summary>
        public virtual string Summary => null;

        public bool IsAttached => listener != null;

        /// <summary>Сценарий слушателя: его Start, триггеры и столкновения. Null — событие не подписано.</summary>
        protected Scenario Scenario => listener?.Scenario;


        // ── Подписка ─────────────────────────────────────────────────────

        /// <summary>Один блок — один слушатель: второй Attach без Release — ошибка, иначе событие ушло бы не туда.</summary>
        public void Attach(IScenarioEventListener eventListener)
        {
            if (eventListener == null)
                throw new ArgumentNullException(nameof(eventListener));

            if (listener != null)
                throw new InvalidOperationException(
                    $"Event block {GetType().Name} is already attached to {listener}. Release it before attaching again");

            listener = eventListener;

            try
            {
                OnAttached();
            }
            catch
            {
                listener = null;
                throw;
            }
        }

        /// <summary>Не подписанный блок отпускать нечего — повторный Release ничего не делает.</summary>
        public void Release()
        {
            if (listener == null)
                return;

            try
            {
                OnReleased();
            }
            finally
            {
                listener = null;
            }
        }


        protected abstract void OnAttached();

        protected abstract void OnReleased();

        protected void Fire(GameObject instigator)
        {
            listener?.HandleEventFired(this, instigator);
        }

        /// <summary>
        /// Fire «случаем» для событий, чей источник не рассылка FeatherKit: блоки этого типа с тем же Caller в одном кадре —
        /// один случай, и Wait For, начатый от него, его же не засчитает. Стук, начавший запуск, — не второй стук.
        /// </summary>
        protected void FireAsOwnOccurrence(GameObject instigator)
        {
            ScenarioEventOccurrences.BeginShared(GetType(), instigator);

            try
            {
                Fire(instigator);
            }
            finally
            {
                ScenarioEventOccurrences.End();
            }
        }
    }
}
