using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Секция Construction у собранной части здания (стены, перекрытие, крыша, группа этажа старого здания): числа — у самого здания,
    /// кнопка перейти к нему. Этаж нового здания и то, что в нём (перегородки, лестницы, проёмы), — не собранные части.
    /// </summary>
    public sealed class LiveBuilderBuildingPartCard : LiveBuilderSelectionCard
    {
        public override int Order => ConstructionOrder;

        public override string Title => "Construction";


        public override bool ShowsFor(IReadOnlyList<GameObject> selected)
        {
            return selected.Count == 1 && AreAllOfKind(selected, LiveBuilderSelectionKind.BuildingPart);
        }


        public override void Draw(IReadOnlyList<GameObject> selected)
        {
            var building = selected[0].GetComponentInParent<SplineBuilding>();

            if (building == null)
                return;

            LiveBuilderWindowControls.Note("Built by the building — change its numbers. Hide it in the Scene view with the eye in the Hierarchy.");

            if (GUILayout.Button(new GUIContent("Select Building", "Выделить здание «" + building.name + "»")))
                Selection.activeGameObject = building.gameObject;
        }
    }
}
