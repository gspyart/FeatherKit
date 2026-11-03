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
    [ScenarioBlock("Light/Fade Intensity")]
    public class FadeLightAction : ScenarioLongAction
    {
        [Tooltip("Which lights")]
        [ScenarioLabel("Lights")]
        [SerializeField] private List<Light> lights = new List<Light>();

        [Tooltip("Target brightness (Intensity). 0 — turn off")]
        [Min(0f)]
        [ScenarioLabel("Intensity")]
        [SerializeField] private float intensity;

        [Tooltip("Over how many seconds. 0 — instantly")]
        [Min(0f)]
        [ScenarioLabel("Seconds")]
        [SerializeField] private float duration = 1f;


        public override string Summary => $"{lights.Count} pcs → {intensity:0.##} over {duration:0.##} s";


        public override IEnumerator Perform(ScenarioRun run)
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
