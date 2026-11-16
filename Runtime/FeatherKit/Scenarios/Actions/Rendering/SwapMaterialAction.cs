using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Поставить рендерерам другой материал в заданный слот: чистая стена становится в надписях.</summary>
    [Serializable]
    [ScenarioBlock("Rendering/Swap Material", Description = "Give renderers another material in one slot")]
    public class SwapMaterialAction : ScenarioInstantAction
    {
        // Общий на все блоки: шаг мгновенный, и двух замен разом в одном кадре не бывает.
        private static readonly List<Material> MaterialsBuffer = new List<Material>();

        [ScenarioLabel("Renderers")]
        [SerializeField] private ScenarioObjectList<Renderer> renderers = new ScenarioObjectList<Renderer>();

        [ScenarioLabel("Material")]
        [SerializeField] private ScenarioValue<Material> material = new ScenarioValue<Material>();

        [Tooltip("Which material slot of the renderer, from 0")]
        [Min(0)]
        [ScenarioLabel("Slot")]
        [SerializeField] private int slotIndex;


        public ScenarioObjectList<Renderer> Renderers => renderers;

        public ScenarioValue<Material> NewMaterial => material;

        public override string Summary => material.UsesVariable ? "graph variable"
            : material.Value != null ? $"{material.Value.name} · slot {slotIndex}" : "no material selected";


        protected override void Apply(ScenarioRun run)
        {
            var newMaterial = material.Get(run);

            if (newMaterial == null)
                throw new InvalidOperationException("No material selected");

            foreach (var renderer in renderers.Get(run))
            {
                if (renderer == null)
                    continue;

                renderer.GetSharedMaterials(MaterialsBuffer);

                if (slotIndex < MaterialsBuffer.Count)
                {
                    MaterialsBuffer[slotIndex] = newMaterial;
                    renderer.SetSharedMaterials(MaterialsBuffer);
                }

                MaterialsBuffer.Clear();
            }
        }
    }
}
