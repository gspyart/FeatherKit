using System;
using System.Collections;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// «Do» сценария: один шаг. Может идти несколько кадров —
    /// следующий шаг начнётся, когда этот закончится.
    /// Шагу без ожидания проще наследовать ScenarioInstantAction,
    /// долгому шагу, который можно пустить фоном, — ScenarioLongAction.
    /// </summary>
    [Serializable]
    public abstract class ScenarioAction
    {
        /// <summary>Короткая подпись настроек для заголовка блока в инспекторе. Null — без неё.</summary>
        public virtual string Summary => null;

        /// <summary>Следующий шаг ждёт конца этого. Не ждать умеет только ScenarioLongAction.</summary>
        public virtual bool WaitsForEnd => true;


        public abstract IEnumerator Perform(ScenarioRun run);
    }


    /// <summary>
    /// Долгий шаг с галкой «Wait Until Done»: снята — шаг уходит в фон, а сценарий сразу берёт следующий.
    /// Сам шаг всегда идёт до конца; ждать его или нет, решает сценарий, а не блок.
    /// </summary>
    [Serializable]
    public abstract class ScenarioLongAction : ScenarioAction
    {
        [Tooltip("The next step starts when this one is done. Unchecked — the step runs in the background and the scenario goes on at once")]
        [ScenarioLabel("Wait Until Done")]
        [SerializeField] private bool waitsForEnd = true;


        public override bool WaitsForEnd => waitsForEnd;
    }


    /// <summary>Шаг, который делается сразу, в том же кадре.</summary>
    [Serializable]
    public abstract class ScenarioInstantAction : ScenarioAction
    {
        public sealed override IEnumerator Perform(ScenarioRun run)
        {
            Apply(run);
            yield break;
        }


        protected abstract void Apply(ScenarioRun run);
    }


    /// <summary>Что сделать с тем, что включается и выключается.</summary>
    public enum ScenarioSwitch
    {
        [InspectorName("Turn On")] TurnOn,
        [InspectorName("Turn Off")] TurnOff,
        [InspectorName("Toggle")] Toggle
    }


    public static class ScenarioSwitchExtensions
    {
        public static bool StateAfter(this ScenarioSwitch change, bool isOn)
        {
            return change switch
            {
                ScenarioSwitch.TurnOn => true,
                ScenarioSwitch.TurnOff => false,
                _ => !isOn
            };
        }

        public static string Describe(this ScenarioSwitch change)
        {
            return change switch
            {
                ScenarioSwitch.TurnOn => "turn on",
                ScenarioSwitch.TurnOff => "turn off",
                _ => "toggle"
            };
        }
    }
}
