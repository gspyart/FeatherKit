using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Плавно изменить яркость ламп до заданной — погасить свет за пару секунд или разжечь.
    /// Работает с Realtime- и Mixed-светом: запечённый в карту света от этого не изменится.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Свет/Плавная яркость")]
    public class FadeLightAction : ScenarioAction
    {
        [Tooltip("Какие лампы")]
        [ScenarioLabel("Лампы")]
        [SerializeField] private List<Light> lights = new List<Light>();

        [Tooltip("Какой станет яркость (Intensity). 0 — погаснуть")]
        [Min(0f)]
        [ScenarioLabel("Яркость")]
        [SerializeField] private float intensity;

        [Tooltip("За сколько секунд. 0 — сразу")]
        [Min(0f)]
        [ScenarioLabel("Секунд")]
        [SerializeField] private float duration = 1f;

        [Tooltip("Следующий шаг начнётся, когда яркость дойдёт до заданной")]
        [ScenarioLabel("Ждать конца")]
        [SerializeField] private bool waitsForEnd;


        public override string Summary => $"{lights.Count} шт → {intensity:0.##} за {duration:0.##} с"
                                          + (waitsForEnd ? " · ждать конца" : "");


        public override IEnumerator Perform(ScenarioRun run)
        {
            if (waitsForEnd)
                yield return Fade();
            else
                RunAside(Fade());
        }

        private IEnumerator Fade()
        {
            var startIntensities = new float[lights.Count];

            for (var index = 0; index < lights.Count; index++)
                startIntensities[index] = lights[index] != null ? lights[index].intensity : 0f;

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                SetAll(startIntensities, elapsed / duration);
                yield return null;
            }

            SetAll(startIntensities, 1f);
        }

        // Лампа могла исчезнуть посреди перехода вместе с объектом — такие пропускаем.
        private void SetAll(float[] startIntensities, float progress)
        {
            for (var index = 0; index < lights.Count; index++)
            {
                if (lights[index] != null)
                    lights[index].intensity = Mathf.Lerp(startIntensities[index], intensity, progress);
            }
        }
    }
}
