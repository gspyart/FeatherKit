using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инспектор части здания: что это за часть и кнопка к самому зданию — числа меняются там, часть собирается сама.
    /// </summary>
    [CustomEditor(typeof(SplineBuildingPart))]
    public class SplineBuildingPartEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var part = (SplineBuildingPart)target;
            var building = part.GetComponentInParent<SplineBuilding>();

            GUILayout.Label(KindName(part) + ". Built by the building — hand edits are overwritten. Hide it in the scene view with the eye in the hierarchy.",
                EditorStyles.wordWrappedMiniLabel);

            if (building != null && GUILayout.Button("Building '" + building.name + "'"))
                Selection.activeGameObject = building.gameObject;
        }


        private static string KindName(SplineBuildingPart part)
        {
            return part.Kind switch
            {
                SplineBuildingPartKind.Walls => "Walls of storey " + (part.Storey + 1),
                SplineBuildingPartKind.Slab => "Slab under storey " + (part.Storey + 1),
                _ => "Roof"
            };
        }
    }
}
