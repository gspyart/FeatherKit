using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Плавно изменить яркость ламп до заданной — погасить свет за пару секунд или разжечь.
    /// Работает с Realtime- и Mixed-светом: запечённый в карту света от этого не изменится.
    /// Старое поле «lights» переносится в новое при загрузке.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Light/Fade Intensity", Description = "Smoothly change the brightness of lights")]
    public class FadeLightAction : ScenarioLongAction, ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector] private List<Light> lights = new List<Light>();

        [Tooltip("Which lights")]
        [ScenarioLabel("Lights")]
        [SerializeField] private ScenarioObjectList<Light> lightList = new ScenarioObjectList<Light>();

        [Tooltip("Target brightness (Intensity). 0 — turn off")]
        [Min(0f)]
        [ScenarioLabel("Intensity")]
        [SerializeField] private float intensity;

        [Tooltip("Over how many seconds. 0 — instantly")]
        [Min(0f)]
        [ScenarioLabel("Seconds")]
        [SerializeField] private float duration = 1f;


        public ScenarioObjectList<Light> LightList => lightList;

        public override string Summary => (lightList.UsesVariable ? "graph variable" : $"{lightList.Items.Count} pcs")
                                          + $" → {intensity:0.##} over {duration:0.##} s";


        public override IEnumerator Perform(ScenarioRun run)
        {
            var lights = lightList.Get(run);
            var startIntensities = new float[lights.Count];

            for (var index = 0; index < lights.Count; index++)
                startIntensities[index] = lights[index] != null ? lights[index].intensity : 0f;

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                SetAll(lights, startIntensities, elapsed / duration);
                yield return null;
            }

            SetAll(lights, startIntensities, 1f);
        }

        // Лампа могла исчезнуть посреди перехода вместе с объектом — такие пропускаем.
        private void SetAll(IReadOnlyList<Light> lights, float[] startIntensities, float progress)
        {
            for (var index = 0; index < lights.Count; index++)
            {
                if (lights[index] != null)
                    lights[index].intensity = Mathf.Lerp(startIntensities[index], intensity, progress);
            }
        }


        // ISerializationCallbackReceiver
        public void OnBeforeSerialize()
        {
        }

        // ISerializationCallbackReceiver
        public void OnAfterDeserialize()
        {
            lightList ??= new ScenarioObjectList<Light>();

            if (lights != null && lights.Count > 0 && lightList.Items.Count == 0 && !lightList.UsesVariable)
                lightList.Items.AddRange(lights);

            lights?.Clear();
        }
    }
}
