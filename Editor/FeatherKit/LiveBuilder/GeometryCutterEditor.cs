using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инспектор выреза: форма, размер, сдвиг, скругление и место в стене → вид (общий материал и стороны) →
    /// что и где режет (свёрнуто, пока режет как по умолчанию) → тонкая гладкость в «Fine».
    /// Панель берёт его частями: главное — в Construction, что режет — в секцию Cut (<see cref="LiveBuilderCutCard"/>).
    /// </summary>
    [CustomEditor(typeof(GeometryCutter))]
    [CanEditMultipleObjects]
    public class GeometryCutterEditor : Editor, ILiveBuilderCompactInspector
    {
        // Полный инспектор: главное → вид → что режет → тонко.
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var cutter = (GeometryCutter)target;
            DrawShapeAndSize();
            DrawCutMethod(cutter);

            LiveBuilderInspectorLayout.Section("Look");
            LiveBuilderInspectorLayout.Properties(serializedObject, "revealMaterial");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sideMaterials"), new GUIContent("Side Materials"), true);

            if (LiveBuilderInspectorLayout.Foldout("GeometryCutter.Targets", "Cutting", IsAnyTargetCuttingNonDefault()))
                DrawCutting(cutter);

            if (LiveBuilderInspectorLayout.Foldout("GeometryCutter.Fine", "Fine"))
            {
                if (IsAnyTargetBox())
                    LiveBuilderInspectorLayout.Properties(serializedObject, "cornerSegments");

                if (!IsEveryTargetBox())
                    LiveBuilderInspectorLayout.Properties(serializedObject, "circleSegments");
            }

            serializedObject.ApplyModifiedProperties();
        }


        // ILiveBuilderCompactInspector: секция Construction панели — форма, размер, место, Make Box.
        public void DrawMainFields()
        {
            serializedObject.Update();
            DrawShapeAndSize();
            DrawCutMethod((GeometryCutter)target);
            serializedObject.ApplyModifiedProperties();
        }


        // Секция Cut панели: где и что режет — всегда раскрыто.
        public void DrawCutFields()
        {
            serializedObject.Update();
            DrawCutting((GeometryCutter)target);
            serializedObject.ApplyModifiedProperties();
        }


        // Форма → размер → сдвиг → скругление (у прямоугольника; «Arch» — полукруг сверху) → место в стене.
        private void DrawShapeAndSize()
        {
            LiveBuilderInspectorLayout.Properties(serializedObject, "shape", "width", "height", "depth", "shapeOffset");

            // Выделено несколько — скругление видно, только если все прямоугольные.
            if (IsEveryTargetBox())
            {
                LiveBuilderInspectorLayout.Properties(serializedObject, "topCornerRadius", "bottomCornerRadius");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Arch", "Верх — полукругом: радиус верхних углов — половина ширины")))
                        MakeEveryTargetArch();

                    if (GUILayout.Button(new GUIContent("Square Corners", "Без скруглений")))
                    {
                        SetTopRadius(0f);
                        serializedObject.FindProperty("bottomCornerRadius").floatValue = 0f;
                    }
                }
            }

            LiveBuilderInspectorLayout.Properties(serializedObject, "followsWallLine");
        }


        private void DrawCutting(GeometryCutter cutter)
        {
            DrawScopeSwitch();
            LiveBuilderInspectorLayout.Properties(serializedObject, "cutTargets");
            DrawScopeResult(cutter);
        }

        // Где режет — два варианта. Старое «Своя группа» показывается как «Own object» и перезаписывается только по клику.
        private void DrawScopeSwitch()
        {
            var scope = serializedObject.FindProperty("scope");
            var isEverything = scope.intValue == (int)GeometryCutterScope.EverythingItTouches;

            EditorGUI.showMixedValue = scope.hasMultipleDifferentValues;
            var choice = LiveBuilderWindowControls.Switch(new GUIContent("Where it cuts",
                    "Own object — ближайшая постройка над вырезом и всё в ней, во вложенные объекты из коробок не заходит. " +
                    "Everything it touches — всё, чего касается, через любые объекты"),
                isEverything ? 1 : 0, "Own object", "Everything it touches");
            EditorGUI.showMixedValue = false;

            if (choice != (isEverything ? 1 : 0))
                scope.intValue = (int)(choice == 1 ? GeometryCutterScope.EverythingItTouches : GeometryCutterScope.OwnObject);
        }

        // Что вырез достаёт на деле. «Own object» без постройки над ним не режет ничего — прежнее «Авто» в корне обновление данных сделало «Everything it touches».
        private void DrawScopeResult(GeometryCutter cutter)
        {
            if (targets.Length > 1)
                return;

            var ownConstruction = LiveBuilderCutRules.OwnConstructionOf(cutter.transform);

            if (cutter.Scope == GeometryCutterScope.EverythingItTouches)
                EditorGUILayout.LabelField("Cuts everything it touches.", EditorStyles.wordWrappedMiniLabel);
            else if (ownConstruction == null)
                EditorGUILayout.HelpBox("No object above this cutter, so it cuts nothing. " +
                                        "Put it inside an object or pick «Everything it touches».", MessageType.Warning);
            else
                EditorGUILayout.LabelField("Cuts only inside «" + ownConstruction.name + "», not inside nested objects.", EditorStyles.wordWrappedMiniLabel);
        }


        // Как режет свой объект: коробка по его осям — точно, как вырезающая коробка, и её можно превратить в коробку; остальное — вычитанием из меша.
        private void DrawCutMethod(GeometryCutter cutter)
        {
            if (targets.Length > 1)
                return;

            if (!GeometryCutterSolverBoxes.IsSolverPiece(cutter))
            {
                EditorGUILayout.LabelField("Cuts by subtracting from meshes.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            EditorGUILayout.LabelField("A box along its object's axes: cuts the object's boxes exactly; walls and slabs too, " +
                                       "unless it has its own side materials or is thinner than a wall (then a niche).", EditorStyles.wordWrappedMiniLabel);

            // После отрисовки: превращение уничтожает этот вырез, а инспектор ещё пишет в него.
            if (GUILayout.Button(new GUIContent("Make Box", "Вырез становится коробкой объекта той же формы, места и материалов. Один шаг Ctrl+Z")))
                EditorApplication.delayCall += BoxModelActions.ToggleAddCutOfSelected;
        }


        // Хоть один выделенный режет не всё или не «Own object» — группа «Cutting» открыта сама.
        private bool IsAnyTargetCuttingNonDefault()
        {
            foreach (var each in targets)
            {
                var eachCutter = (GeometryCutter)each;

                if (eachCutter.CutTargets != LiveBuilderCutTargets.Everything || eachCutter.Scope != GeometryCutterScope.OwnObject)
                    return true;
            }

            return false;
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
