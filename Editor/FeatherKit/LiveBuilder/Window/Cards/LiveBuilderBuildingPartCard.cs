using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка «Часть здания» зоны «Выделено»: выделена собранная часть здания (группа этажа, стены, пол, крыша) — числа у самого здания,
    /// кнопка перейти к нему. Интерьер (перегородки, лестницы, проёмы) — не собранный, у него свои числа.
    /// </summary>
    public sealed class LiveBuilderBuildingPartCard : LiveBuilderSelectionCard
    {
        public override int Order => 90;


        public override bool Draw(GameObject selected)
        {
            if (selected == null || selected.GetComponent<SplineBuilding>() != null)
                return false;

            var building = selected.GetComponentInParent<SplineBuilding>();
            var isGenerated = selected.GetComponent<SplineBuildingPart>() != null
                || building != null && selected.transform.parent == building.transform && selected.name.StartsWith(SplineBuildingRebuilder.StoreyGroupPrefix);

            if (building == null || !isGenerated)
                return false;

            using (LiveBuilderWindowControls.Card("Часть здания"))
            {
                LiveBuilderWindowControls.Note("Собирается сама. Числа — у здания. Спрятать в окне сцены — «глазик» в иерархии.");

                if (GUILayout.Button("Здание «" + building.name + "»"))
                    Selection.activeGameObject = building.gameObject;
            }

            return true;
        }
    }
}
