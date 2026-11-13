using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода «When»: с неё начинается запуск графа.
    /// - держит стопку событий; любое сработавшее — попытка запуска, вызвавший становится «Caller»
    /// - условия проверяет в момент срабатывания: сошлись — выход Then, нет — Not Met
    /// - решает, что делать с повторным срабатыванием, пока прошлый запуск идёт; умеет сработать один раз
    /// </summary>
    [Serializable]
    public class ScenarioWhenNode : ScenarioNode, IScenarioEventListener
    {
        public static readonly ScenarioOutput Then = new ScenarioOutput("then", "Then");
        public static readonly ScenarioOutput NotMet = new ScenarioOutput("notMet", "Not Met");

        [SerializeReference] private List<ScenarioEvent> events = new List<ScenarioEvent>();

        [SerializeField] private ScenarioConditionList conditions = new ScenarioConditionList();

        [Tooltip("What to do when an event fires while the previous run from this node is still going")]
        [ScenarioLabel("If Already Running")]
        [ScenarioInspectorOnly]
        [SerializeField] private ScenarioRerunRule rerunRule = ScenarioRerunRule.SkipIfRunning;

        [Tooltip("Fire only once; after that, events are ignored")]
        [ScenarioLabel("Only Once")]
        [ScenarioInspectorOnly]
        [SerializeField] private bool runsOnlyOnce;

        [NonSerialized] private Scenario owner;


        public override string DefaultTitle => "When";

        public IReadOnlyList<ScenarioEvent> Events => events;

        public ScenarioConditionList Conditions => conditions;

        public ScenarioRerunRule RerunRule
        {
            get => rerunRule;
            set => rerunRule = value;
        }

        public bool RunsOnlyOnce
        {
            get => runsOnlyOnce;
            set => runsOnlyOnce = value;
        }

        // IScenarioEventListener
        public Scenario Scenario => owner;


        // ── Сборка кодом ─────────────────────────────────────────────────

        public void AddEvent(ScenarioEvent scenarioEvent)
        {
            events.Add(scenarioEvent);
        }


        // ── Подписка ─────────────────────────────────────────────────────

        /// <summary>Слушать свои события, пока компонент сценария включён.</summary>
        internal void Attach(Scenario scenario)
        {
            owner = scenario;

            foreach (var scenarioEvent in events)
                scenarioEvent?.Attach(this);
        }

        internal void Release()
        {
            foreach (var scenarioEvent in events)
                scenarioEvent?.Release();

            owner = null;
        }


        // IScenarioEventListener
        public void HandleEventFired(ScenarioEvent firedEvent, GameObject instigator)
        {
            // TODO этап 1, рантайм: Only Once, правило повтора, условия → Then / Not Met.
        }
    }


    /// <summary>Событие ноды When сработало, а прошлый запуск от неё ещё идёт.</summary>
    public enum ScenarioRerunRule
    {
        [InspectorName("Skip")] SkipIfRunning,
        [InspectorName("Restart")] Restart,
        [InspectorName("Run Alongside")] RunAlongside
    }
}
