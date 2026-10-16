using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// «Когда» сценария: следит за чем-то одним и запускает сценарий.
    /// Наследник подписывается в OnAttached, отписывается в OnReleased и зовёт Fire.
    /// </summary>
    [Serializable]
    public abstract class ScenarioEvent
    {
        /// <summary>Короткая подпись настроек для заголовка блока в инспекторе. Null — без неё.</summary>
        public virtual string Summary => null;

        protected Scenario Scenario { get; private set; }


        // ── Подписка ─────────────────────────────────────────────────────

        internal void Attach(Scenario scenario)
        {
            Scenario = scenario;
            OnAttached();
        }

        internal void Release()
        {
            OnReleased();
            Scenario = null;
        }


        protected abstract void OnAttached();

        protected abstract void OnReleased();

        protected void Fire(GameObject instigator)
        {
            if (Scenario != null)
                Scenario.TryRun(instigator);
        }
    }
}
