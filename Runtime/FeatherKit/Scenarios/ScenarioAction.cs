using System;
using System.Collections;
using UnityEngine;
using AppContext = FeatherKit.Contexts.AppContext;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// «Что сделать» сценария: один шаг. Может идти несколько кадров —
    /// следующий шаг начнётся, когда этот закончится.
    /// Шагу без ожидания проще наследовать ScenarioInstantAction.
    /// </summary>
    [Serializable]
    public abstract class ScenarioAction
    {
        /// <summary>Короткая подпись настроек для заголовка блока в инспекторе. Null — без неё.</summary>
        public virtual string Summary => null;


        public abstract IEnumerator Perform(ScenarioRun run);


        /// <summary>Пустить долгий шаг идти самого, не дожидаясь: сценарий сразу берёт следующий.</summary>
        protected static void RunAside(IEnumerator routine)
        {
            AppContext.Current.Coroutines.Run(ScenarioSteps.Flatten(routine));
        }
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
        [InspectorName("Включить")] TurnOn,
        [InspectorName("Выключить")] TurnOff,
        [InspectorName("Переключить")] Toggle
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
                ScenarioSwitch.TurnOn => "включить",
                ScenarioSwitch.TurnOff => "выключить",
                _ => "переключить"
            };
        }
    }
}
