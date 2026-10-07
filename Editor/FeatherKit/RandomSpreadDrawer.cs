using FeatherKit.Randomness;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Рисует <see cref="RandomSpread"/> одной строкой «центр ± разброс ☐ Границы».
    /// Вторая строка «от … до …» появляется, только когда границы включены:
    /// выключенные поля в инспекторе только отвлекают.
    /// </summary>
    [CustomPropertyDrawer(typeof(RandomSpread))]
    public class RandomSpreadDrawer : PropertyDrawer
    {
        private const float SignWidth = 16f;
        private const float ToggleWidth = 80f;
        private const float BoundLabelWidth = 24f;
        private const float Gap = 4f;

        private static readonly GUIContent ClampLabel = new GUIContent("Границы", "Не выпускать результат за границы");
        private static readonly GUIContent FromLabel = new GUIContent("от");
        private static readonly GUIContent ToLabel = new GUIContent("до");


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var line = EditorGUIUtility.singleLineHeight;
            var isClamped = property.FindPropertyRelative("isClamped").boolValue;

            return isClamped ? line * 2f + EditorGUIUtility.standardVerticalSpacing : line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var center = property.FindPropertyRelative("center");
            var spread = property.FindPropertyRelative("spread");
            var isClamped = property.FindPropertyRelative("isClamped");
            var clampMin = property.FindPropertyRelative("clampMin");
            var clampMax = property.FindPropertyRelative("clampMax");

            EditorGUI.BeginProperty(position, label, property);

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var content = EditorGUI.PrefixLabel(line, label);

            // Вложенные поля не должны уезжать вправо вслед за отступом родителя.
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            DrawCenterAndSpread(content, center, spread, isClamped);

            if (isClamped.boolValue)
            {
                var second = new Rect(content.x, content.yMax + EditorGUIUtility.standardVerticalSpacing, content.width, content.height);
                DrawBounds(second, clampMin, clampMax);
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        private static void DrawCenterAndSpread(Rect rect, SerializedProperty center, SerializedProperty spread, SerializedProperty isClamped)
        {
            var fieldWidth = (rect.width - ToggleWidth - Gap - SignWidth) / 2f;

            var centerRect = new Rect(rect.x, rect.y, fieldWidth, rect.height);
            var signRect = new Rect(centerRect.xMax, rect.y, SignWidth, rect.height);
            var spreadRect = new Rect(signRect.xMax, rect.y, fieldWidth, rect.height);
            var toggleRect = new Rect(spreadRect.xMax + Gap, rect.y, ToggleWidth, rect.height);

            EditorGUI.PropertyField(centerRect, center, GUIContent.none);
            EditorGUI.LabelField(signRect, "±", EditorStyles.centeredGreyMiniLabel);
            EditorGUI.PropertyField(spreadRect, spread, GUIContent.none);
            spread.floatValue = Mathf.Max(0f, spread.floatValue);

            isClamped.boolValue = EditorGUI.ToggleLeft(toggleRect, ClampLabel, isClamped.boolValue);
        }

        private static void DrawBounds(Rect rect, SerializedProperty clampMin, SerializedProperty clampMax)
        {
            var fieldWidth = (rect.width - BoundLabelWidth * 2f - Gap) / 2f;

            var fromLabelRect = new Rect(rect.x, rect.y, BoundLabelWidth, rect.height);
            var minRect = new Rect(fromLabelRect.xMax, rect.y, fieldWidth, rect.height);
            var toLabelRect = new Rect(minRect.xMax + Gap, rect.y, BoundLabelWidth, rect.height);
            var maxRect = new Rect(toLabelRect.xMax, rect.y, fieldWidth, rect.height);

            EditorGUI.LabelField(fromLabelRect, FromLabel);
            EditorGUI.PropertyField(minRect, clampMin, GUIContent.none);
            EditorGUI.LabelField(toLabelRect, ToLabel);
            EditorGUI.PropertyField(maxRect, clampMax, GUIContent.none);
        }
    }
}
