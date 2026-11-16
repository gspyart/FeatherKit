using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Плавно разжечь или погасить свечение рендереров — глаза в темноте, тлеющая проводка.
    /// Цвет — через MaterialPropertyBlock. Эмиссия выключена в материале — рендерер получает копию с ней, одну на всё время;
    /// копии убираются, а исходные материалы возвращаются, когда объект сценария уничтожается.
    /// </summary>
    [Serializable]
    [ScenarioBlock("Rendering/Fade Emission", Description = "Fade the glow of renderers to a color and brightness")]
    public class FadeEmissionAction : ScenarioLongAction
    {
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

        [NonSerialized] private EmissionMaterialCopies materialCopies;


        public ScenarioObjectList<Renderer> Renderers => renderers;

        public ScenarioValue<Color> GlowColor => color;

        public override string Summary => $"× {intensity:0.##} over {duration:0.##} s";


        public override IEnumerator Perform(ScenarioRun run)
        {
            var targets = renderers.Get(run);
            materialCopies ??= new EmissionMaterialCopies(run.Scenario);
            var endColor = color.Get(run) * intensity;
            var startColors = new List<Color>(targets.Count);
            var block = new MaterialPropertyBlock();

            foreach (var renderer in targets)
            {
                if (renderer != null)
                    materialCopies.EnsureEmission(renderer);

                startColors.Add(renderer != null ? CurrentEmissionOf(renderer, block) : default);
            }

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                SetAll(targets, startColors, endColor, elapsed / duration, block);
                yield return null;
            }

            SetAll(targets, startColors, endColor, 1f, block);
        }

        private static Color CurrentEmissionOf(Renderer renderer, MaterialPropertyBlock block)
        {
            renderer.GetPropertyBlock(block);

            if (block.HasColor(EmissionColorId))
                return block.GetColor(EmissionColorId);

            var material = renderer.sharedMaterial;

            return material != null && material.HasProperty(EmissionColorId) ? material.GetColor(EmissionColorId) : Color.black;
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


    /// <summary>
    /// Копии материалов с включённой эмиссией — по одной на рендерер, на всё время жизни сценария.
    /// Ассет не меняется; при уничтожении сценария рендерерам возвращаются исходные материалы, копии удаляются.
    /// </summary>
    internal class EmissionMaterialCopies
    {
        private const string EmissionKeyword = "_EMISSION";

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly Dictionary<Renderer, Material[]> originalsByRenderer = new Dictionary<Renderer, Material[]>();
        private readonly List<Material> copies = new List<Material>();


        public EmissionMaterialCopies(Scenario owner)
        {
            owner.Destroying += RestoreAndDestroy;
        }


        /// <summary>Эмиссия у рендерера включена: уже была — ничего, нет — своя копия материала с ней, один раз.</summary>
        public void EnsureEmission(Renderer renderer)
        {
            if (originalsByRenderer.ContainsKey(renderer))
                return;

            var originals = renderer.sharedMaterials;

            if (!NeedsCopy(originals))
                return;

            var replaced = new Material[originals.Length];

            for (var index = 0; index < originals.Length; index++)
                replaced[index] = CopyWithEmission(originals[index]);

            originalsByRenderer[renderer] = originals;
            renderer.sharedMaterials = replaced;
        }

        private static bool NeedsCopy(Material[] materials)
        {
            foreach (var material in materials)
            {
                if (material != null && material.HasProperty(EmissionColorId) && !material.IsKeywordEnabled(EmissionKeyword))
                    return true;
            }

            return false;
        }

        private Material CopyWithEmission(Material original)
        {
            if (original == null || !original.HasProperty(EmissionColorId) || original.IsKeywordEnabled(EmissionKeyword))
                return original;

            var copy = new Material(original) { name = original.name + " (Emission)" };
            copy.EnableKeyword(EmissionKeyword);
            copies.Add(copy);

            return copy;
        }


        private void RestoreAndDestroy()
        {
            foreach (var pair in originalsByRenderer)
            {
                if (pair.Key != null)
                    pair.Key.sharedMaterials = pair.Value;
            }

            foreach (var copy in copies)
            {
                if (copy != null)
                    UnityEngine.Object.Destroy(copy);
            }

            originalsByRenderer.Clear();
            copies.Clear();
        }
    }
}
