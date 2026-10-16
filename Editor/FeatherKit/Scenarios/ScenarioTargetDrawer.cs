using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>Цель блока в одну строку: выбор «этот объект / кто вызвал / игрок / объект», у «объекта» — поле справа.</summary>
    [CustomPropertyDrawer(typeof(ScenarioTarget))]
    internal class ScenarioTargetDrawer : PropertyDrawer
    {
        private const float KindWidth = 120f;
        private const float Spacing = 4f;


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var kind = property.FindPropertyRelative("kind");
            var specificObject = property.FindPropertyRelative("specificObject");

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var fieldRect = EditorGUI.PrefixLabel(position, label);
                var previousIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                if (kind.enumValueIndex == (int)ScenarioTargetKind.Specific)
                {
                    var kindRect = new Rect(fieldRect.x, fieldRect.y, Mathf.Min(KindWidth, fieldRect.width * 0.45f), fieldRect.height);
                    var objectRect = new Rect(kindRect.xMax + Spacing, fieldRect.y, fieldRect.xMax - kindRect.xMax - Spacing, fieldRect.height);

                    EditorGUI.PropertyField(kindRect, kind, GUIContent.none);
                    EditorGUI.PropertyField(objectRect, specificObject, GUIContent.none);
                }
                else
                {
                    EditorGUI.PropertyField(fieldRect, kind, GUIContent.none);
                }

                EditorGUI.indentLevel = previousIndent;
            }
        }
    }
}
