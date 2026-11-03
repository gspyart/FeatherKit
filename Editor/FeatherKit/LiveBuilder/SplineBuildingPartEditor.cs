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

            GUILayout.Label(KindName(part) + ". Собирается зданием — правки руками затрутся. Спрятать в окне сцены — «глазик» в иерархии.",
                EditorStyles.wordWrappedMiniLabel);

            if (building != null && GUILayout.Button("Здание «" + building.name + "»"))
                Selection.activeGameObject = building.gameObject;
        }


        private static string KindName(SplineBuildingPart part)
        {
            return part.Kind switch
            {
                SplineBuildingPartKind.Walls => "Стены этажа " + (part.Storey + 1),
                SplineBuildingPartKind.Floor => "Пол этажа " + (part.Storey + 1),
                _ => "Крыша"
            };
        }
    }
}
