using FeatherKit.Randomness;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Рисует <see cref="RandomRange"/> двумя строками: «min … max» и ползунок уклона.
    /// Ползунок на отдельной строке, иначе на узком инспекторе его не ухватить.
    /// </summary>
    [CustomPropertyDrawer(typeof(RandomRange))]
    public class RandomRangeDrawer : PropertyDrawer
    {
        private const float DotsWidth = 16f;
        private const float BiasLabelWidth = 44f;

        private static readonly GUIContent BiasLabel = new GUIContent("Уклон", "0 — всегда min, 0.5 — равномерно, 1 — всегда max");


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var min = property.FindPropertyRelative("min");
            var max = property.FindPropertyRelative("max");
            var bias = property.FindPropertyRelative("bias");

            EditorGUI.BeginProperty(position, label, property);

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var content = EditorGUI.PrefixLabel(line, label);

            // Вложенные поля не должны уезжать вправо вслед за отступом родителя.
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            DrawMinMax(content, min, max);

            var second = new Rect(content.x, content.yMax + EditorGUIUtility.standardVerticalSpacing, content.width, content.height);
            DrawBias(second, bias);

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        private static void DrawMinMax(Rect rect, SerializedProperty min, SerializedProperty max)
        {
            var fieldWidth = (rect.width - DotsWidth) / 2f;

            var minRect = new Rect(rect.x, rect.y, fieldWidth, rect.height);
            var dotsRect = new Rect(minRect.xMax, rect.y, DotsWidth, rect.height);
            var maxRect = new Rect(dotsRect.xMax, rect.y, fieldWidth, rect.height);

            EditorGUI.PropertyField(minRect, min, GUIContent.none);
            EditorGUI.LabelField(dotsRect, "…", EditorStyles.centeredGreyMiniLabel);
            EditorGUI.PropertyField(maxRect, max, GUIContent.none);
        }

        private static void DrawBias(Rect rect, SerializedProperty bias)
        {
            var labelRect = new Rect(rect.x, rect.y, BiasLabelWidth, rect.height);
            var sliderRect = new Rect(labelRect.xMax, rect.y, rect.width - BiasLabelWidth, rect.height);

            EditorGUI.LabelField(labelRect, BiasLabel);
            bias.floatValue = EditorGUI.Slider(sliderRect, bias.floatValue, 0f, 1f);
        }
    }
}
