using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инспектор лестницы: куда идёт подъём — стрелками, как сверху, и кнопками поворота; всё внутри её коробки (<see cref="BoxModelStairsTurns"/>).
    /// Потом форма → вокруг → тонко. Тот же инспектор показывает окно Live Builder.
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
            LiveBuilderInspectorLayout.Properties(serializedObject, "style", "width", "height");

            using (new EditorGUI.DisabledScope(stairs.Style != BoxModelStairsStyle.SeparateSteps))
                LiveBuilderInspectorLayout.Properties(serializedObject, "stepThickness");

            DrawLength(stairs);

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

        // Длина марша: меняет глубину ступени, число ступеней — от высоты.
        private void DrawLength(BoxModelStairs stairs)
        {
            var length = EditorGUILayout.FloatField(new GUIContent("Length", "Длина марша, м. Ступеней — от высоты, глубина ступени подгоняется под длину"),
                stairs.Length);

            GUILayout.Label(stairs.StepCount + " ступеней по " + LiveBuilderSceneText.FormatMeters(stairs.StepDepth) + " м", EditorStyles.miniLabel);

            if (Mathf.Approximately(length, stairs.Length))
                return;

            serializedObject.ApplyModifiedProperties();

            foreach (var selected in targets)
            {
                var each = (BoxModelStairs)selected;
                Undo.RecordObject(each, "Change Stairs Length");
                each.StepDepth = length / each.StepCount;
            }

            serializedObject.Update();
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
    }
}
