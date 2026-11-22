using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Секция Level Storeys у старого здания без этажей-контейнеров, которое не стоит на этажах уровня: кнопка выровнять.
    /// Здание с этажами-контейнерами показывает этаж уровня в Construction (<see cref="SplineBuildingLevelFields"/>), там этой секции нет.
    /// </summary>
    public sealed class LiveBuilderLevelAlignmentCard : LiveBuilderSelectionCard
    {
        public override int Order => LevelStoreysOrder;

        public override string Title => "Level Storeys";


        public override bool ShowsFor(IReadOnlyList<GameObject> selected)
        {
            return selected.Count == 1 && selected[0].TryGetComponent<SplineBuilding>(out var building)
                                       && !SplineBuildingStoreys.HasStoreyContainers(building) && !SplineBuildingLevelAlignment.IsAlignedToLevel(building);
        }


        public override void Draw(IReadOnlyList<GameObject> selected)
        {
            LiveBuilderWindowControls.Note("The building storeys do not match the level storeys: its bottom is off a level floor or its storey height differs.");

            if (GUILayout.Button(new GUIContent("Put On Level Storeys", "Низ здания — на ближайший пол этажа уровня, высота этажа — как у уровня. Интерьер переедет вместе")))
                SplineBuildingLevelAlignment.AlignToLevel(selected[0].GetComponent<SplineBuilding>());
        }
    }
}
