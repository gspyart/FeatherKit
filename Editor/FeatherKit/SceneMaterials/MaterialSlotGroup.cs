using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Слоты выделения, в которых лежит один и тот же материал. Это строка вкладки
    /// «Выделение» и единица операций: новая копия заводится одна на группу, а не на каждый
    /// слот, — иначе пять камней с одним материалом получили бы пять одинаковых копий.
    /// </summary>
    public class MaterialSlotGroup
    {
        public readonly Material Material;
        public readonly List<MaterialSlot> Slots = new List<MaterialSlot>();

        private int ownerCount = -1;


        public MaterialSlotGroup(Material material)
        {
            Material = material;
        }


        /// <summary>Сколько разных объектов носят группу. Слоты после сборки не меняются, поэтому считаем раз.</summary>
        public int OwnerCount
        {
            get
            {
                if (ownerCount >= 0)
                    return ownerCount;

                var owners = new HashSet<GameObject>();

                foreach (var slot in Slots)
                    owners.Add(slot.Owner.gameObject);

                ownerCount = owners.Count;
                return ownerCount;
            }
        }


        /// <summary>Раскладывает слоты по материалам. Порядок групп — порядок первого появления.</summary>
        public static void Build(IEnumerable<MaterialSlot> slots, List<MaterialSlotGroup> into)
        {
            foreach (var slot in slots)
            {
                var group = Find(into, slot.Material);

                if (group == null)
                {
                    group = new MaterialSlotGroup(slot.Material);
                    into.Add(group);
                }

                group.Slots.Add(slot);
            }
        }

        // Линейный поиск, а не словарь: пустой слот — тоже группа, а ключом словаря null быть не может.
        private static MaterialSlotGroup Find(List<MaterialSlotGroup> groups, Material material)
        {
            foreach (var group in groups)
                if (group.Material == material)
                    return group;

            return null;
        }
    }
}
