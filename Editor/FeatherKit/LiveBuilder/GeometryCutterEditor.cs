using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инспектор выреза: форма → размер → скругление (у прямоугольника; «Арка» — полукруг сверху) → этажи → вид → место в стене.
    /// Вырез в здании — копии по этажам задаёт привязка к зданию, здесь число только показано. Тонкая настройка гладкости — свёрнута в «Тонко».
    /// </summary>
    [CustomEditor(typeof(GeometryCutter))]
    [CanEditMultipleObjects]
    public class GeometryCutterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var cutter = (GeometryCutter)target;
            var isLinked = cutter.TryGetComponent<SplineBuildingStoreyLink>(out var link) && link.IsValid;

            LiveBuilderInspectorLayout.Section("Форма");
            LiveBuilderInspectorLayout.Properties(serializedObject, "shape");

            LiveBuilderInspectorLayout.Section("Размер");
            LiveBuilderInspectorLayout.Properties(serializedObject, "width", "height", "depth");

            if (cutter.Shape == GeometryCutterShape.Box)
            {
                LiveBuilderInspectorLayout.Section("Скругление");
                LiveBuilderInspectorLayout.Properties(serializedObject, "topCornerRadius", "bottomCornerRadius");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Арка", "Верх — полукругом: радиус верхних углов — половина ширины")))
                        SetTopRadius(cutter.Width / 2f);

                    if (GUILayout.Button(new GUIContent("Прямые углы", "Без скруглений")))
                    {
                        SetTopRadius(0f);
                        serializedObject.FindProperty("bottomCornerRadius").floatValue = 0f;
                    }
                }
            }

            LiveBuilderInspectorLayout.StoreyCopies(serializedObject, cutter, isLinked);

            LiveBuilderInspectorLayout.Section("Вид");
            LiveBuilderInspectorLayout.Properties(serializedObject, "revealMaterial");

            LiveBuilderInspectorLayout.Section("Место в стене");
            LiveBuilderInspectorLayout.Properties(serializedObject, "followsWallLine");

            if (LiveBuilderInspectorLayout.Foldout("GeometryCutter.Fine", "Тонко"))
                LiveBuilderInspectorLayout.Properties(serializedObject, cutter.Shape == GeometryCutterShape.Box ? "cornerSegments" : "circleSegments");

            serializedObject.ApplyModifiedProperties();
        }

        private void SetTopRadius(float radius)
        {
            serializedObject.FindProperty("topCornerRadius").floatValue = radius;
        }
    }
}
