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
    /// Без «ждать конца» мигание идёт само, а сценарий сразу берёт следующий шаг.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Свет/Мигание")]
    public class FlickerAction : ScenarioAction
    {
        [Tooltip("Что мигает: свет, рендерер плафона или луча, любой включаемый компонент. Перетащите сами компоненты")]
        [ScenarioLabel("Что мигает")]
        [SerializeField] private List<Component> components = new List<Component>();

        [Tooltip("Сколько секунд мигать")]
        [Min(0f)]
        [ScenarioLabel("Секунд")]
        [SerializeField] private float duration = 1.5f;

        [Tooltip("Сколько длится одна вспышка или провал, секунды")]
        [ScenarioLabel("Вспышка, с")]
        [SerializeField] private RandomRange flashLength = new RandomRange(0.03f, 0.18f, 0.35f);

        [Tooltip("Чем закончить: погаснуть, загореться или вернуть как было")]
        [ScenarioLabel("В конце")]
        [SerializeField] private FlickerEnding ending;

        [Tooltip("Следующий шаг начнётся, когда мигание закончится")]
        [ScenarioLabel("Ждать конца")]
        [SerializeField] private bool waitsForEnd;


        public override string Summary
        {
            get
            {
                var end = ending switch
                {
                    FlickerEnding.TurnOff => "погаснуть",
                    FlickerEnding.TurnOn => "загореться",
                    _ => "как было"
                };

                return $"{components.Count} шт · {duration:0.##} с → {end}" + (waitsForEnd ? " · ждать конца" : "");
            }
        }


        public override IEnumerator Perform(ScenarioRun run)
        {
            if (waitsForEnd)
                yield return Flicker();
            else
                RunAside(Flicker());
        }

        private IEnumerator Flicker()
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
        [InspectorName("Погаснуть")] TurnOff,
        [InspectorName("Загореться")] TurnOn,
        [InspectorName("Как было")] AsBefore
    }
}
