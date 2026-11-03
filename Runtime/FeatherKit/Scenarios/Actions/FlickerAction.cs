using System;
using System.Collections;
using System.Collections.Generic;
using FeatherKit.Randomness;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Неисправная лампа: компоненты рывками гаснут и загораются, потом остаются как задано.
    /// Все мигают вместе — свет, луч и светящийся плафон одной лампы не расходятся.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Light/Flicker")]
    public class FlickerAction : ScenarioLongAction
    {
        [Tooltip("What flickers: a light, a lamp shade or beam renderer, any component that can be switched on and off. Drag the components themselves")]
        [ScenarioLabel("What Flickers")]
        [SerializeField] private List<Component> components = new List<Component>();

        [Tooltip("How many seconds to flicker")]
        [Min(0f)]
        [ScenarioLabel("Seconds")]
        [SerializeField] private float duration = 1.5f;

        [Tooltip("How long one flash or dip lasts, seconds")]
        [ScenarioLabel("Flash, s")]
        [SerializeField] private RandomRange flashLength = new RandomRange(0.03f, 0.18f, 0.35f);

        [Tooltip("How to end: turn off, turn on or restore as before")]
        [ScenarioLabel("At the End")]
        [SerializeField] private FlickerEnding ending;


        public override string Summary
        {
            get
            {
                var end = ending switch
                {
                    FlickerEnding.TurnOff => "turn off",
                    FlickerEnding.TurnOn => "turn on",
                    _ => "as before"
                };

                return $"{components.Count} pcs · {duration:0.##} s → {end}";
            }
        }


        public override IEnumerator Perform(ScenarioRun run)
        {
            var initialStates = new bool[components.Count];

            for (var index = 0; index < components.Count; index++)
                initialStates[index] = components[index] != null && SwitchableComponents.IsEnabled(components[index]);

            var isLit = true;
            var endTime = Time.time + duration;

            while (Time.time < endTime)
            {
                isLit = !isLit;
                SetAll(index => isLit);

                yield return new WaitForSeconds(Mathf.Max(0.01f, flashLength.Value));
            }

            SetAll(index => ending switch
            {
                FlickerEnding.TurnOff => false,
                FlickerEnding.TurnOn => true,
                _ => initialStates[index]
            });
        }

        // Компонент мог исчезнуть посреди мигания вместе с объектом — такие пропускаем.
        private void SetAll(Func<int, bool> isEnabledAt)
        {
            for (var index = 0; index < components.Count; index++)
            {
                if (components[index] != null)
                    SwitchableComponents.SetEnabled(components[index], isEnabledAt(index));
            }
        }
    }


    public enum FlickerEnding
    {
        [InspectorName("Turn Off")] TurnOff,
        [InspectorName("Turn On")] TurnOn,
        [InspectorName("As Before")] AsBefore
    }
}
