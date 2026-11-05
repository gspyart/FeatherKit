using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инспектор выреза: форма → размер → сдвиг от точки объекта → скругление (у прямоугольника; «Арка» — полукруг сверху) → этажи →
    /// что и где режет (с подписью, внутри какой постройки) → вид → место в стене.
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

            LiveBuilderInspectorLayout.Section("Сдвиг");
            LiveBuilderInspectorLayout.Properties(serializedObject, "shapeOffset");

            // Выделено несколько — скругление видно, только если все прямоугольные.
            if (IsEveryTargetBox())
            {
                LiveBuilderInspectorLayout.Section("Скругление");
                LiveBuilderInspectorLayout.Properties(serializedObject, "topCornerRadius", "bottomCornerRadius");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Арка", "Верх — полукругом: радиус верхних углов — половина ширины")))
                        MakeEveryTargetArch();

                    if (GUILayout.Button(new GUIContent("Прямые углы", "Без скруглений")))
                    {
                        SetTopRadius(0f);
                        serializedObject.FindProperty("bottomCornerRadius").floatValue = 0f;
                    }
                }
            }

            LiveBuilderInspectorLayout.StoreyCopies(serializedObject, cutter, isLinked);

            LiveBuilderInspectorLayout.Section("Что режет");
            LiveBuilderInspectorLayout.Properties(serializedObject, "cutTargets", "scope");
            DrawScopeResult(cutter);

            LiveBuilderInspectorLayout.Section("Вид");
            LiveBuilderInspectorLayout.Properties(serializedObject, "revealMaterial");

            LiveBuilderInspectorLayout.Section("Место в стене");
            LiveBuilderInspectorLayout.Properties(serializedObject, "followsWallLine");

            if (LiveBuilderInspectorLayout.Foldout("GeometryCutter.Fine", "Тонко"))
            {
                if (IsAnyTargetBox())
                    LiveBuilderInspectorLayout.Properties(serializedObject, "cornerSegments");

                if (!IsEveryTargetBox())
                    LiveBuilderInspectorLayout.Properties(serializedObject, "circleSegments");
            }

            serializedObject.ApplyModifiedProperties();
        }

        // Где режет на деле: внутри какой постройки или всё вокруг. Своя группа без постройки над вырезом — ничего не режет.
        private void DrawScopeResult(GeometryCutter cutter)
        {
            if (targets.Length > 1)
                return;

            var group = GeometryCutterLookup.OwnGroupOf(cutter);
            var scope = cutter.Scope;

            if (scope == GeometryCutterScope.OwnGroup && group == null)
                EditorGUILayout.HelpBox("Над вырезом нет постройки — своей группы нет, он ничего не режет. Положи его в стену, здание или объект из коробок.",
                    MessageType.Warning);
            else if (scope == GeometryCutterScope.Everything || group == null)
                EditorGUILayout.LabelField("Режет всё, что пересекает", EditorStyles.wordWrappedMiniLabel);
            else
                EditorGUILayout.LabelField("Режет только внутри «" + group.name + "»", EditorStyles.wordWrappedMiniLabel);
        }


        private void SetTopRadius(float radius)
        {
            serializedObject.FindProperty("topCornerRadius").floatValue = radius;
        }

        // «Арка» каждому выделенному — по его ширине: общее поле записало бы всем радиус первого.
        private void MakeEveryTargetArch()
        {
            foreach (var each in targets)
            {
                var single = new SerializedObject(each);
                single.FindProperty("topCornerRadius").floatValue = ((GeometryCutter)each).Width / 2f;
                single.ApplyModifiedProperties();
            }

            serializedObject.Update();
        }


        private bool IsEveryTargetBox()
        {
            foreach (var each in targets)
                if (((GeometryCutter)each).Shape != GeometryCutterShape.Box)
                    return false;

            return true;
        }

        private bool IsAnyTargetBox()
        {
            foreach (var each in targets)
                if (((GeometryCutter)each).Shape == GeometryCutterShape.Box)
                    return true;

            return false;
        }
    }
}
