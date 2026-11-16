using System;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Поле «куда записать» (ScenarioVariableSlot): кнопка на всю ширину с именем переменной.
    /// - меню — подходящие типу слота переменные графа и «New Variable…»
    /// - не выбрана — красная подпись: блоку некуда писать
    /// </summary>
    [CustomPropertyDrawer(typeof(ScenarioVariableSlot<>))]
    internal class ScenarioVariableSlotDrawer : PropertyDrawer
    {
        private const string NotChosenText = "Choose a variable";


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var valueType = ValueTypeOf(fieldInfo.FieldType);
            var variableId = property.FindPropertyRelative(ScenarioVariablePicking.VariableIdPropertyName);
            var isChosen = !string.IsNullOrEmpty(variableId.stringValue);
            var variable = isChosen ? ScenarioVariablePicking.GraphOf(property)?.FindVariable(variableId.stringValue) : null;
            var problem = isChosen ? ScenarioGraphValidation.VariableProblemOf(variable, valueType) : "Nowhere to write: no variable chosen";

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var fieldRect = ScenarioVariableField.PrefixLabel(position, label, problem != null);
                var previousIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                var buttonContent = new GUIContent(variable != null ? variable.Name : isChosen ? "Variable not found" : NotChosenText,
                    variable != null && problem == null ? ScenarioIcons.IconOf(variable) : ScenarioIcons.Error,
                    problem ?? $"Writes to “{variable?.Name}” — click to change");

                var previousColor = GUI.contentColor;

                if (problem != null)
                    GUI.contentColor = ScenarioVariableField.ProblemColor;

                if (EditorGUI.DropdownButton(fieldRect, buttonContent, FocusType.Passive))
                    ShowMenu(variableId, valueType, label.text);

                GUI.contentColor = previousColor;
                EditorGUI.indentLevel = previousIndent;
            }

            ScenarioVariablePicking.HandleVariableDrop(position, variableId, candidate => ScenarioVariableKinds.Fits(candidate, valueType));
        }

        // Поле может лежать в списке: тогда ScenarioVariableSlot<T> — у элемента.
        private static Type ValueTypeOf(Type fieldType)
        {
            while (fieldType != null && !(fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(ScenarioVariableSlot<>)))
                fieldType = fieldType.IsArray ? fieldType.GetElementType() : fieldType.IsGenericType ? fieldType.GetGenericArguments()[0] : null;

            return fieldType?.GetGenericArguments()[0];
        }

        private static void ShowMenu(SerializedProperty variableId, Type valueType, string fieldName)
        {
            var menu = new GenericMenu();
            var graph = ScenarioVariablePicking.GraphOf(variableId);

            ScenarioVariablePicking.AddVariableItems(menu, variableId, ScenarioVariablePicking.FittingVariables(graph, valueType));

            if (graph != null)
            {
                menu.AddSeparator("");
                ScenarioVariablePicking.AddNewVariableItem(menu, "New Variable…", variableId, valueType, fieldName);
            }

            menu.ShowAsContext();
        }
    }
}
