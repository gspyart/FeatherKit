using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инспектор лестницы: куда идёт подъём — стрелками, как сверху, и кнопками поворота; всё внутри её коробки (<see cref="BoxModelStairsTurns"/>).
    /// Потом форма → вокруг → тонко. У лестницы с площадкой — ещё площадка, зазор между маршами и сторона первого марша;
    /// сменил форму или глубину площадки — коробка та же, марши под неё. Тот же инспектор показывает окно Live Builder.
    /// </summary>
    [CustomEditor(typeof(BoxModelStairs))]
    [CanEditMultipleObjects]
    public class BoxModelStairsEditor : Editor
    {
        // Порядок стрелок в переключателе: как стороны на виде сверху по часовой — вперёд, вправо, назад, влево.
        private static readonly BoxModelStairsDirection[] DirectionOrder =
        {
            BoxModelStairsDirection.PositiveZ, BoxModelStairsDirection.PositiveX, BoxModelStairsDirection.NegativeZ, BoxModelStairsDirection.NegativeX
        };


        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var stairs = (BoxModelStairs)target;

            LiveBuilderInspectorLayout.Section("Подъём");
            DrawDirection(stairs);
            DrawTurnButtons();

            LiveBuilderInspectorLayout.Section("Форма");
            DrawShapeKeepingBox();
            LiveBuilderInspectorLayout.Properties(serializedObject, "style", "width", "height");

            using (new EditorGUI.DisabledScope(stairs.Style != BoxModelStairsStyle.SeparateSteps))
                LiveBuilderInspectorLayout.Properties(serializedObject, "stepThickness");

            DrawLength(stairs);

            if (stairs.IsSwitchback)
                DrawLanding(stairs);

            LiveBuilderInspectorLayout.Section("Вокруг");
            LiveBuilderInspectorLayout.Properties(serializedObject, "cutsAbove", "material");

            if (LiveBuilderInspectorLayout.Foldout("BoxModelStairs.Fine", "Тонко"))
                LiveBuilderInspectorLayout.Properties(serializedObject, "stepHeight", "stepDepth");

            serializedObject.ApplyModifiedProperties();
        }


        // Стрелки — куда идёт подъём, если смотреть сверху по осям объекта. Сменил — коробка та же, ступени под неё.
        private void DrawDirection(BoxModelStairs stairs)
        {
            var current = System.Array.IndexOf(DirectionOrder, stairs.Direction);
            var chosen = LiveBuilderWindowControls.Switch(
                new GUIContent("Вверх к", "Куда идёт подъём, вид сверху: ↑ — к +Z объекта, → — к +X. Коробка лестницы стоит на месте, ступени перестраиваются под неё"),
                current, "↑ +Z", "→ +X", "↓ −Z", "← −X");

            if (chosen == current)
                return;

            serializedObject.ApplyModifiedProperties();

            foreach (var selected in targets)
                BoxModelStairsTurns.Turn((BoxModelStairs)selected, DirectionOrder[chosen]);

            serializedObject.Update();
        }

        // Длина лестницы: меняет глубину ступени, число ступеней — от высоты; у лестницы с площадкой площадка остаётся своей.
        private void DrawLength(BoxModelStairs stairs)
        {
            var length = EditorGUILayout.FloatField(new GUIContent("Length", "Длина лестницы по подъёму, м. Ступеней — от высоты, глубина ступени подгоняется под длину"),
                stairs.Length);

            GUILayout.Label(stairs.StepCount + " ступеней по " + LiveBuilderSceneText.FormatMeters(stairs.StepDepth) + " м", EditorStyles.miniLabel);

            if (Mathf.Approximately(length, stairs.Length))
                return;

            serializedObject.ApplyModifiedProperties();

            foreach (var selected in targets)
            {
                var each = (BoxModelStairs)selected;
                Undo.RecordObject(each, "Change Stairs Length");
                each.StepDepth = each.StepDepthForLength(length);
            }

            serializedObject.Update();
        }


        // Форма — прямая или с площадкой — в той же коробке: длина лестницы прежняя, ступени под неё.
        private void DrawShapeKeepingBox()
        {
            KeepingLength(() => LiveBuilderInspectorLayout.Properties(serializedObject, "shape"), "Change Stairs Shape");
        }


        // Площадка, зазор между маршами и сторона первого марша; глубина площадки — в той же коробке, марши короче или длиннее.
        private void DrawLanding(BoxModelStairs stairs)
        {
            KeepingLength(() => LiveBuilderInspectorLayout.Properties(serializedObject, "landingDepth"), "Change Stairs Landing");
            LiveBuilderInspectorLayout.Properties(serializedObject, "flightGap", "isFirstFlightOnOtherSide");
            GUILayout.Label("Марш шириной " + LiveBuilderSceneText.FormatMeters(stairs.FlightWidth) + " м: " + stairs.FirstFlightStepCount + " ступеней до площадки, " +
                            stairs.SecondFlightStepCount + " после", EditorStyles.miniLabel);
        }


        private void DrawTurnButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("↻ Повернуть", "На четверть оборота по часовой, если смотреть сверху, в той же коробке. Tab в окне сцены — то же")))
                    TurnAll(BoxModelStairsTurns.Clockwise);

                if (GUILayout.Button(new GUIContent("⇅ Развернуть", "Подъём в обратную сторону в той же коробке: низ и верх меняются местами")))
                    TurnAll(BoxModelStairsTurns.Opposite);
            }
        }

        private void TurnAll(System.Func<BoxModelStairsDirection, BoxModelStairsDirection> turn)
        {
            foreach (var selected in targets)
            {
                var stairs = (BoxModelStairs)selected;
                BoxModelStairsTurns.Turn(stairs, turn(stairs.Direction));
            }
        }


        // Поле поменялось — длина лестницы та же, что до него: глубина ступени подгоняется.
        private void KeepingLength(System.Action drawField, string undoName)
        {
            var lengthsBefore = new float[targets.Length];

            for (var index = 0; index < targets.Length; index++)
                lengthsBefore[index] = ((BoxModelStairs)targets[index]).Length;

            EditorGUI.BeginChangeCheck();
            drawField();

            if (!EditorGUI.EndChangeCheck())
                return;

            serializedObject.ApplyModifiedProperties();

            for (var index = 0; index < targets.Length; index++)
            {
                var each = (BoxModelStairs)targets[index];
                Undo.RecordObject(each, undoName);
                each.StepDepth = each.StepDepthForLength(lengthsBefore[index]);
            }

            serializedObject.Update();
        }
    }
}
