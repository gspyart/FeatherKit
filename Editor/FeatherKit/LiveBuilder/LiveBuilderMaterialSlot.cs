using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Одно место, где Live Builder хранит материал: сторона коробки, пол комнаты, стена по линии, наружные стены здания, стенки выреза.
    /// Меш постройки собирается из этих данных заново при каждой правке — красить надо их, а не меш: покраска меша слетела бы.
    /// Problem — почему сюда материал не положить (у стены тип — материал задаёт он); тогда TryAssign ничего не меняет.
    /// Найти такое место — <see cref="LiveBuilderMaterialSlots"/>.
    /// </summary>
    public readonly struct LiveBuilderMaterialSlot
    {
        private const string UndoName = "Assign Material";

        private readonly System.Action<Material> assign;


        public LiveBuilderMaterialSlot(Object owner, string description, System.Action<Material> assign, string problem = null)
        {
            Owner = owner;
            Description = description;
            Problem = problem;
            this.assign = assign;
        }


        // Чьи данные: коробка, комната, лестница, стена, здание, вырез или объект из коробок.
        public Object Owner { get; }

        // Коротко, что покрасится: «верх коробки», «пол здания».
        public string Description { get; }

        public string Problem { get; }

        public bool IsValid => Owner != null;


        // Положить материал одним шагом отмены; нельзя — false.
        public bool TryAssign(Material material)
        {
            if (Owner == null || Problem != null || assign == null)
                return false;

            Undo.RecordObject(Owner, UndoName);
            assign(material);
            EditorUtility.SetDirty(Owner);
            return true;
        }
    }
}
