using System;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Цель блока в одну строку: слева узкая кнопка-иконка вида (This Object / Caller / Player / Object / переменная), дальше — поле.
    /// У «Object» поле объекта на весь остаток, у переменной — её имя, у остальных — подпись, на что действует блок.
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
            var variableId = property.FindPropertyRelative(ScenarioVariablePicking.VariableIdPropertyName);
            var variable = targetKind == ScenarioTargetKind.Variable
                ? ScenarioVariablePicking.GraphOf(property)?.FindVariable(variableId.stringValue)
                : null;
            var problem = targetKind == ScenarioTargetKind.Variable ? ScenarioGraphValidation.VariableProblemOf(variable, typeof(GameObject)) : null;

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var fieldRect = ScenarioVariableField.PrefixLabel(position, label, problem != null);
                var previousIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                var kindRect = new Rect(fieldRect.x, fieldRect.y, ScenarioVariableField.KindButtonWidth, fieldRect.height);
                var contentRect = new Rect(kindRect.xMax + Spacing, fieldRect.y, fieldRect.xMax - kindRect.xMax - Spacing, fieldRect.height);

                var buttonContent = problem != null ? new GUIContent(ScenarioIcons.Error, problem)
                    : new GUIContent(IconOf(targetKind), $"{TitleOf(targetKind)} — click to change");

                if (EditorGUI.DropdownButton(kindRect, buttonContent, FocusType.Passive, EditorStyles.miniButton))
                    ShowKindMenu(property, label.text);

                if (targetKind == ScenarioTargetKind.Specific)
                    ScenarioObjectFieldGUI.Draw(contentRect, specificObject, typeof(GameObject));
                else if (targetKind == ScenarioTargetKind.Variable)
                    ScenarioVariableField.DrawVariableName(contentRect, variable, problem);
                else
                    EditorGUI.LabelField(contentRect, new GUIContent(TitleOf(targetKind), IconOf(targetKind)), KindTextStyle);

                EditorGUI.indentLevel = previousIndent;
            }

            if (ScenarioVariablePicking.HandleVariableDrop(position, variableId, candidate => ScenarioVariableKinds.Fits(candidate, typeof(GameObject))))
                kind.enumValueIndex = (int)ScenarioTargetKind.Variable;
        }


        private static void ShowKindMenu(SerializedProperty property, string fieldName)
        {
            var kind = property.FindPropertyRelative("kind");
            var serializedObject = kind.serializedObject;
            var kindPath = kind.propertyPath;
            var menu = new GenericMenu();

            foreach (ScenarioTargetKind targetKind in Enum.GetValues(typeof(ScenarioTargetKind)))
            {
                if (targetKind == ScenarioTargetKind.Variable)
                    continue;

                var chosenKind = targetKind;
                menu.AddItem(new GUIContent(TitleOf(targetKind)), kind.enumValueIndex == (int)targetKind, () =>
                {
                    serializedObject.Update();
                    serializedObject.FindProperty(kindPath).enumValueIndex = (int)chosenKind;
                    serializedObject.ApplyModifiedProperties();
                });
            }

            AddVariableItems(menu, property, kindPath, fieldName);
            menu.ShowAsContext();
        }

        // Переменная — не пятый пункт вида, а сразу её имя: выбор вида и переменной одним кликом.
        private static void AddVariableItems(GenericMenu menu, SerializedProperty property, string kindPath, string fieldName)
        {
            var graph = ScenarioVariablePicking.GraphOf(property);

            if (graph == null)
                return;

            var variableId = property.FindPropertyRelative(ScenarioVariablePicking.VariableIdPropertyName);

            void ChooseVariableKind(SerializedProperty chosenId) =>
                chosenId.serializedObject.FindProperty(kindPath).enumValueIndex = (int)ScenarioTargetKind.Variable;

            menu.AddSeparator("");
            ScenarioVariablePicking.AddVariableItems(menu, variableId, ScenarioVariablePicking.FittingVariables(graph, typeof(GameObject)),
                ChooseVariableKind);
            ScenarioVariablePicking.AddNewVariableItem(menu, "New Variable…", variableId, typeof(GameObject), fieldName, null, ChooseVariableKind);
        }

        private static string TitleOf(ScenarioTargetKind kind)
        {
            return kind switch
            {
                ScenarioTargetKind.Self => "This Object",
                ScenarioTargetKind.Instigator => "Caller",
                ScenarioTargetKind.Player => "Player",
                ScenarioTargetKind.Variable => "Variable",
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
