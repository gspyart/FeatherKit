using System;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Цель блока в одну строку: слева узкая кнопка-иконка вида (This Object / Caller / Player / Object), дальше — поле.
    /// У «Object» поле объекта на весь остаток, у остальных — подпись, на что действует блок.
    /// </summary>
    [CustomPropertyDrawer(typeof(ScenarioTarget))]
    internal class ScenarioTargetDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        private static GUIStyle kindTextStyle;


        private static GUIStyle KindTextStyle => kindTextStyle ??= new GUIStyle(EditorStyles.textField) { imagePosition = ImagePosition.ImageLeft };


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var kind = property.FindPropertyRelative("kind");
            var specificObject = property.FindPropertyRelative("specificObject");
            var targetKind = (ScenarioTargetKind)kind.enumValueIndex;

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var fieldRect = EditorGUI.PrefixLabel(position, label);
                var previousIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                var kindRect = new Rect(fieldRect.x, fieldRect.y, ScenarioVariableField.KindButtonWidth, fieldRect.height);
                var contentRect = new Rect(kindRect.xMax + Spacing, fieldRect.y, fieldRect.xMax - kindRect.xMax - Spacing, fieldRect.height);

                if (EditorGUI.DropdownButton(kindRect, new GUIContent(IconOf(targetKind), $"{TitleOf(targetKind)} — click to change"),
                        FocusType.Passive, EditorStyles.miniButton))
                    ShowKindMenu(kind);

                if (targetKind == ScenarioTargetKind.Specific)
                    ScenarioObjectFieldGUI.Draw(contentRect, specificObject, typeof(GameObject));
                else
                    EditorGUI.LabelField(contentRect, new GUIContent(TitleOf(targetKind), IconOf(targetKind)), KindTextStyle);

                EditorGUI.indentLevel = previousIndent;
            }
        }


        private static void ShowKindMenu(SerializedProperty kind)
        {
            var serializedObject = kind.serializedObject;
            var kindPath = kind.propertyPath;
            var menu = new GenericMenu();

            foreach (ScenarioTargetKind targetKind in Enum.GetValues(typeof(ScenarioTargetKind)))
            {
                var chosenKind = targetKind;
                menu.AddItem(new GUIContent(TitleOf(targetKind)), kind.enumValueIndex == (int)targetKind, () =>
                {
                    serializedObject.Update();
                    serializedObject.FindProperty(kindPath).enumValueIndex = (int)chosenKind;
                    serializedObject.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }

        private static string TitleOf(ScenarioTargetKind kind)
        {
            return kind switch
            {
                ScenarioTargetKind.Self => "This Object",
                ScenarioTargetKind.Instigator => "Caller",
                ScenarioTargetKind.Player => "Player",
                _ => "Object"
            };
        }

        private static Texture IconOf(ScenarioTargetKind kind)
        {
            return kind switch
            {
                ScenarioTargetKind.Self => ScenarioIcons.TypeIcon(typeof(Transform)),
                ScenarioTargetKind.Instigator => ScenarioIcons.Caller,
                ScenarioTargetKind.Player => ScenarioIcons.Player,
                _ => ScenarioIcons.TypeIcon(typeof(GameObject))
            };
        }
    }
}
