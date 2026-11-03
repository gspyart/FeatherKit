using System;
using System.Collections;
using System.Collections.Generic;
using FeatherKit.Coroutines;
using UnityEngine;
using AppContext = FeatherKit.Contexts.AppContext;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Кто-то вошёл в триггер на объекте сценария, вышел из него или находится внутри.
    /// - гость считается один раз, сколько бы коллайдеров у него ни было
    /// - «Stay» — запуск каждый кадр: условия вроде «Player Looks at Point» ловятся, когда сойдутся
    /// - кто считается гостем, решает наследник
    /// </summary>
    [Serializable]
    public abstract class TriggerEvent : ScenarioEvent
    {
        [Tooltip("When to fire: on entering the trigger, on leaving it or every frame while the visitor is inside")]
        [ScenarioLabel("When")]
        [SerializeField] private TriggerCrossing crossing;

        // Сколько коллайдеров каждого гостя сейчас внутри.
        [NonSerialized] private Dictionary<GameObject, int> collidersInsideByVisitor;
        [NonSerialized] private List<GameObject> visitorsToFire;
        [NonSerialized] private CoroutineToken staying;


        public override string Summary => crossing switch
        {
            TriggerCrossing.Entered => "enter",
            TriggerCrossing.Exited => "exit",
            _ => "stay"
        };


        // ── Подписка ─────────────────────────────────────────────────────

        protected override void OnAttached()
        {
            collidersInsideByVisitor = new Dictionary<GameObject, int>();
            visitorsToFire = new List<GameObject>();

            Scenario.TriggerEntered += HandleEntered;
            Scenario.TriggerExited += HandleExited;
        }

        protected override void OnReleased()
        {
            Scenario.TriggerEntered -= HandleEntered;
            Scenario.TriggerExited -= HandleExited;

            staying.Stop();
            collidersInsideByVisitor = null;
            visitorsToFire = null;
        }


        // ── Вход и выход ─────────────────────────────────────────────────

        private void HandleEntered(Collider other)
        {
            if (!IsVisitor(other, out var visitor))
                return;

            collidersInsideByVisitor.TryGetValue(visitor, out var count);
            collidersInsideByVisitor[visitor] = count + 1;

            if (count > 0)
                return;

            if (crossing == TriggerCrossing.Entered)
                Fire(visitor);
            else if (crossing == TriggerCrossing.Staying && !staying.IsRunning)
                staying = AppContext.Current.Coroutines.Run(FireEveryFrameWhileInside());
        }

        private void HandleExited(Collider other)
        {
            if (!IsVisitor(other, out var visitor) || !collidersInsideByVisitor.TryGetValue(visitor, out var count))
                return;

            if (count > 1)
            {
                collidersInsideByVisitor[visitor] = count - 1;
                return;
            }

            collidersInsideByVisitor.Remove(visitor);

            if (crossing == TriggerCrossing.Exited)
                Fire(visitor);
            else if (crossing == TriggerCrossing.Staying && collidersInsideByVisitor.Count == 0)
                staying.Stop();
        }


        // Копия списка гостей: запуск сценария может выключить триггер и очистить словарь прямо в обходе.
        private IEnumerator FireEveryFrameWhileInside()
        {
            while (collidersInsideByVisitor != null && collidersInsideByVisitor.Count > 0)
            {
                visitorsToFire.Clear();
                visitorsToFire.AddRange(collidersInsideByVisitor.Keys);

                foreach (var visitor in visitorsToFire)
                {
                    // Удалённый гость выхода из триггера не присылает.
                    if (visitor == null)
                        collidersInsideByVisitor?.Remove(visitor);
                    else
                        Fire(visitor);
                }

                yield return null;
            }
        }


        /// <summary>Этот коллайдер — часть гостя? Гость — объект, который получат шаги как «Caller».</summary>
        protected abstract bool IsVisitor(Collider other, out GameObject visitor);
    }


    public enum TriggerCrossing
    {
        [InspectorName("Enter")] Entered,
        [InspectorName("Exit")] Exited,
        [InspectorName("Stay — Every Frame")] Staying
    }
}
