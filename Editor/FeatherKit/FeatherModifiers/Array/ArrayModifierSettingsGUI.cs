using System.Collections.Generic;
using FeatherKit.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Настройки модификатора Array (<see cref="ArrayModifier"/>) — одни и те же в окне Feather Modifiers и в инспекторе:
    /// сетка или круг переключателем, поля только выбранной раскладки, пропуски строкой «3, 7», сколько вышло копий.
    /// Правка — сразу пересборка, тем же шагом Ctrl+Z.
    /// </summary>
    public static class ArrayModifierSettingsGUI
    {
        private static readonly string[] LayoutNames = { "Grid", "Circle" };


        public static void Draw(ArrayModifier array)
        {
            var serializedArray = new SerializedObject(array);
            serializedArray.Update();
            EditorGUI.BeginChangeCheck();

            var layout = serializedArray.FindProperty("layout");
            layout.enumValueIndex = GUILayout.Toolbar(layout.enumValueIndex, LayoutNames);
            EditorGUILayout.Space(2f);

            if ((ArrayModifierLayout)layout.enumValueIndex == ArrayModifierLayout.Circle)
                DrawCircle(serializedArray);
            else
                DrawGrid(serializedArray);

            DrawSkippedCopies(serializedArray.FindProperty("skippedCopies"));

            if (EditorGUI.EndChangeCheck())
            {
                serializedArray.ApplyModifiedProperties();
                ArrayModifierBuilder.Rebuild(array);
            }

            DrawSummary(array);
        }

        private static void DrawGrid(SerializedObject serializedArray)
        {
            var counts = serializedArray.FindProperty("counts");
            var stepsFromSize = serializedArray.FindProperty("stepsFromSize");

            EditorGUILayout.PropertyField(counts, new GUIContent("Count", counts.tooltip));
            counts.vector3IntValue = Vector3Int.Max(counts.vector3IntValue, Vector3Int.one);
            EditorGUILayout.PropertyField(stepsFromSize, new GUIContent("Step From Edge", stepsFromSize.tooltip));

            var step = serializedArray.FindProperty(stepsFromSize.boolValue ? "gap" : "step");
            EditorGUILayout.PropertyField(step, new GUIContent(stepsFromSize.boolValue ? "Gap" : "Step", step.tooltip));
        }

        private static void DrawCircle(SerializedObject serializedArray)
        {
            var circleCount = serializedArray.FindProperty("circleCount");
            var facesCenter = serializedArray.FindProperty("facesCenter");

            EditorGUILayout.PropertyField(circleCount, new GUIContent("Count", circleCount.tooltip));
            EditorGUILayout.PropertyField(serializedArray.FindProperty("radius"));
            EditorGUILayout.PropertyField(serializedArray.FindProperty("arc"));
            EditorGUILayout.PropertyField(facesCenter, new GUIContent("Face Center", facesCenter.tooltip));
        }

        // Пропуски строкой: «3, 7». Числа разбираются, когда нажал Enter или ушёл из поля, — иначе запятая пропадала бы на лету.
        private static void DrawSkippedCopies(SerializedProperty skippedCopies)
        {
            var numbers = new List<string>();

            for (var index = 0; index < skippedCopies.arraySize; index++)
                numbers.Add(skippedCopies.GetArrayElementAtIndex(index).intValue.ToString());

            var text = EditorGUILayout.DelayedTextField(new GUIContent("Skip", skippedCopies.tooltip + ". Numbers separated by commas: 3, 7"),
                string.Join(", ", numbers));
            var parsed = ParseNumbers(text);

            if (string.Join(",", parsed) == string.Join(",", numbers))
                return;

            skippedCopies.arraySize = parsed.Count;

            for (var index = 0; index < parsed.Count; index++)
                skippedCopies.GetArrayElementAtIndex(index).intValue = parsed[index];
        }

        // Номера больше единицы, без повторов, по порядку: единица — сам объект, его не пропустить.
        private static List<int> ParseNumbers(string text)
        {
            var numbers = new SortedSet<int>();

            foreach (var part in text.Split(',', ' ', ';'))
                if (int.TryParse(part, out var number) && number > 1)
                    numbers.Add(number);

            return new List<int>(numbers);
        }

        // Сколько копий вышло, или почему их нет.
        private static void DrawSummary(ArrayModifier array)
        {
            var problem = ArrayModifierBuilder.ProblemOf(array);

            if (problem != null)
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
            else
                EditorGUILayout.LabelField("This object and " + array.Copies.Count + " copies", EditorStyles.miniLabel);
        }
    }
}
