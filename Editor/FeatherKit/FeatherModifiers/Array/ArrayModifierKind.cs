using FeatherKit.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>Модификатор Array (<see cref="ArrayModifier"/>) в окне Feather Modifiers — первым. На копию массива его не ставят: она повторяет свой объект.</summary>
    public sealed class ArrayModifierKind : FeatherModifierKind
    {
        public override string Title => "Array";

        public override string Tooltip => "Copies of the selected object in a grid or a circle — windows along a facade, columns around a hall";

        public override int Order => 0;


        public override Component FindOn(GameObject target)
        {
            return target.GetComponent<ArrayModifier>();
        }


        public override void AddTo(GameObject target)
        {
            var array = Undo.AddComponent<ArrayModifier>(target);
            ArrayModifierBuilder.Rebuild(array);
        }


        public override void DrawSettings(Component modifier)
        {
            ArrayModifierSettingsGUI.Draw((ArrayModifier)modifier);
        }


        public override void Apply(Component modifier)
        {
            ArrayModifierBuilder.Apply((ArrayModifier)modifier);
        }


        public override void Remove(Component modifier)
        {
            ArrayModifierBuilder.Remove((ArrayModifier)modifier);
        }
    }
}
