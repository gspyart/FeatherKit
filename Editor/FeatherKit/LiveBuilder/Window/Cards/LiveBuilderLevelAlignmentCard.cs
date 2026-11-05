using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка «Этажи уровня» зоны «Выделено»: здание не на этажах уровня (поставлено раньше или сдвинуто) — его этажи не совпадают с окном,
    /// предупреждение и кнопка выровнять. Здание на этажах — карточки нет.
    /// </summary>
    public sealed class LiveBuilderLevelAlignmentCard : LiveBuilderSelectionCard
    {
        public override int Order => 60;


        public override bool Draw(GameObject selected)
        {
            if (OwnComponentOf(selected) is not SplineBuilding building || SplineBuildingLevelAlignment.IsAlignedToLevel(building))
                return false;

            using (LiveBuilderWindowControls.Card("Этажи уровня"))
            {
                EditorGUILayout.HelpBox("Этажи здания не совпадают с этажами уровня: низ здания не на полу этажа или высота этажа другая. " +
                                        "Тогда «этаж» в окне и в здании — разные.", MessageType.Warning);

                if (GUILayout.Button(new GUIContent("Поставить на этажи уровня", "Низ здания — на ближайший пол этажа уровня, высота этажа — как в окне. Интерьер переедет вместе")))
                    SplineBuildingLevelAlignment.AlignToLevel(building);
            }

            return true;
        }
    }
}
