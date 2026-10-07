using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Кто какой материал носит в открытых сценах: все слоты разом, счёт носителей
    /// и их выделение. Сценовому материалу это заменяет файл — «кто ещё делит эту копию»
    /// иначе не узнать, — а про проектный говорит, скольким в сцене уедет правка.
    /// Собирается заново по просьбе окна, само за сценой не следит.
    /// </summary>
    public class SceneMaterialUsage
    {
        private readonly List<MaterialSlot> slots = new List<MaterialSlot>();
        private readonly List<Material> sceneMaterials = new List<Material>();
        private readonly Dictionary<Material, List<MaterialSlot>> slotsByMaterial = new Dictionary<Material, List<MaterialSlot>>();
        private readonly Dictionary<Material, int> ownerCounts = new Dictionary<Material, int>();

        /// <summary>Сценовые копии открытых сцен, по одной на материал.</summary>
        public IReadOnlyList<Material> SceneMaterials => sceneMaterials;


        public void Refresh()
        {
            slots.Clear();
            sceneMaterials.Clear();
            slotsByMaterial.Clear();
            ownerCounts.Clear();

            SceneMaterialTools.CollectSceneSlots(slots);

            foreach (var slot in slots)
            {
                if (slot.Material == null)
                    continue;

                if (!slotsByMaterial.TryGetValue(slot.Material, out var materialSlots))
                {
                    materialSlots = new List<MaterialSlot>();
                    slotsByMaterial.Add(slot.Material, materialSlots);

                    if (slot.IsSceneMaterial)
                        sceneMaterials.Add(slot.Material);
                }

                materialSlots.Add(slot);
            }
        }


        // ── Носители ─────────────────────────────────────

        public int CountSlots(Material material) =>
            material != null && slotsByMaterial.TryGetValue(material, out var materialSlots) ? materialSlots.Count : 0;


        // Считаем лениво и запоминаем: спрашивают на каждой перерисовке, а слотов в сцене тысячи.
        public int CountOwners(Material material)
        {
            if (material == null || !slotsByMaterial.TryGetValue(material, out var materialSlots))
                return 0;

            if (ownerCounts.TryGetValue(material, out var count))
                return count;

            var owners = new HashSet<GameObject>();

            foreach (var slot in materialSlots)
                owners.Add(slot.Owner.gameObject);

            ownerCounts.Add(material, owners.Count);
            return owners.Count;
        }


        public GameObject FindFirstOwner(Material material) =>
            material != null && slotsByMaterial.TryGetValue(material, out var materialSlots)
                ? materialSlots[0].Owner.gameObject
                : null;


        public void SelectOwners(Material material)
        {
            if (material != null && slotsByMaterial.TryGetValue(material, out var materialSlots))
                SceneMaterialTools.SelectOwners(materialSlots);
        }
    }
}
