using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Плавно разжечь или погасить свечение рендереров — глаза в темноте, тлеющая проводка.
    /// Цвет кладётся через MaterialPropertyBlock; у материала без включённой эмиссии она включается на его копии.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Rendering/Fade Emission", Description = "Fade the glow of renderers to a color and brightness")]
    public class FadeEmissionAction : ScenarioLongAction
    {
        private const string EmissionKeyword = "_EMISSION";

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [ScenarioLabel("Renderers")]
        [SerializeField] private ScenarioObjectList<Renderer> renderers = new ScenarioObjectList<Renderer>();

        [ScenarioLabel("Color")]
        [SerializeField] private ScenarioValue<Color> color = new ScenarioValue<Color>(Color.white);

        [Tooltip("Brightness multiplier. 0 — no glow")]
        [Min(0f)]
        [ScenarioLabel("Intensity")]
        [SerializeField] private float intensity = 1f;

        [Tooltip("Over how many seconds. 0 — instantly")]
        [Min(0f)]
        [ScenarioLabel("Seconds")]
        [SerializeField] private float duration = 1f;


        public ScenarioObjectList<Renderer> Renderers => renderers;

        public ScenarioValue<Color> GlowColor => color;

        public override string Summary => $"× {intensity:0.##} over {duration:0.##} s";


        public override IEnumerator Perform(ScenarioRun run)
        {
            var targets = renderers.Get(run);
            var endColor = color.Get(run) * intensity;
            var startColors = new List<Color>(targets.Count);
            var block = new MaterialPropertyBlock();

            foreach (var renderer in targets)
                startColors.Add(renderer != null ? CurrentEmissionOf(renderer, block) : default);

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                SetAll(targets, startColors, endColor, elapsed / duration, block);
                yield return null;
            }

            SetAll(targets, startColors, endColor, 1f, block);
        }

        private static Color CurrentEmissionOf(Renderer renderer, MaterialPropertyBlock block)
        {
            EnsureEmissionKeyword(renderer);
            renderer.GetPropertyBlock(block);

            if (block.HasColor(EmissionColorId))
                return block.GetColor(EmissionColorId);

            var material = renderer.sharedMaterial;

            return material != null && material.HasProperty(EmissionColorId) ? material.GetColor(EmissionColorId) : Color.black;
        }

        // Ключевое слово блоком не включить; копия материала — чтобы не поменять сам ассет.
        private static void EnsureEmissionKeyword(Renderer renderer)
        {
            var material = renderer.sharedMaterial;

            if (material != null && !material.IsKeywordEnabled(EmissionKeyword))
                renderer.material.EnableKeyword(EmissionKeyword);
        }

        // Рендерер мог исчезнуть посреди перехода вместе с объектом — такие пропускаем.
        private static void SetAll(IReadOnlyList<Renderer> targets, List<Color> startColors, Color endColor, float progress,
            MaterialPropertyBlock block)
        {
            for (var index = 0; index < targets.Count; index++)
            {
                if (targets[index] == null)
                    continue;

                targets[index].GetPropertyBlock(block);
                block.SetColor(EmissionColorId, Color.Lerp(startColors[index], endColor, progress));
                targets[index].SetPropertyBlock(block);
            }
        }
    }
}
