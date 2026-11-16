using System;
using System.Collections.Generic;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Поле «переменная графа любого вида» (у Set Variable, Compare Variable, Variable Changed): кнопка на всю ширину.
    /// - меню — переменные разрешённых видов и «New Variable…» по видам; без ограничения — все виды
    /// - работает с любым полем, где есть variableId: сам выбор переменной блокам не нужен знать
    /// </summary>
    internal static class ScenarioVariableChoiceField
    {
        private const string NotChosenText = "Choose a variable";

        private static readonly ScenarioVariableKind[] AllKinds = (ScenarioVariableKind[])Enum.GetValues(typeof(ScenarioVariableKind));


        public static void Draw(Rect position, SerializedProperty property, GUIContent label, IReadOnlyCollection<ScenarioVariableKind> allowedKinds)
        {
            var kinds = allowedKinds is { Count: > 0 } ? allowedKinds : AllKinds;
            var variableId = property.FindPropertyRelative(ScenarioVariablePicking.VariableIdPropertyName);
            var isChosen = !string.IsNullOrEmpty(variableId.stringValue);
            var variable = isChosen ? ScenarioVariablePicking.GraphOf(property)?.FindVariable(variableId.stringValue) : null;
            var problem = ProblemOf(isChosen, variable, kinds);

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var fieldRect = ScenarioVariableField.PrefixLabel(position, label, problem != null);
                var previousIndent = EditorGUI.indentLevel;
                var previousColor = GUI.contentColor;
                EditorGUI.indentLevel = 0;

                if (problem != null)
                    GUI.contentColor = ScenarioVariableField.ProblemColor;

                var content = new GUIContent(variable != null ? variable.Name : isChosen ? "Variable not found" : NotChosenText,
                    variable != null ? ScenarioIcons.IconOf(variable) : null,
                    problem ?? $"{ScenarioVariablePicking.KindTextOf(variable)} variable — click to change");

                if (EditorGUI.DropdownButton(fieldRect, content, FocusType.Passive))
                    ShowMenu(variableId, kinds, label.text);

                GUI.contentColor = previousColor;
                EditorGUI.indentLevel = previousIndent;
            }

            ScenarioVariablePicking.HandleVariableDrop(position, variableId, candidate => kinds.Contains(candidate.Kind));
        }

        private static string ProblemOf(bool isChosen, ScenarioVariableDefinition variable, IReadOnlyCollection<ScenarioVariableKind> kinds)
        {
            if (!isChosen)
                return "No variable chosen";

            if (variable == null)
                return "Variable not found";

            return kinds.Contains(variable.Kind) ? null : $"“{variable.Name}” is a {variable.Kind}, this field takes {string.Join(" or ", kinds)}";
        }

        private static void ShowMenu(SerializedProperty variableId, IReadOnlyCollection<ScenarioVariableKind> kinds, string fieldName)
        {
            var menu = new GenericMenu();
            var graph = ScenarioVariablePicking.GraphOf(variableId);
            var fitting = graph != null ? graph.Variables.Where(variable => variable != null && kinds.Contains(variable.Kind)).ToList()
                : new List<ScenarioVariableDefinition>();

            ScenarioVariablePicking.AddVariableItems(menu, variableId, fitting);

            if (graph != null)
            {
                menu.AddSeparator("");

                foreach (var kind in kinds)
                    ScenarioVariablePicking.AddNewVariableItem(menu, $"New Variable…/{ObjectNames.NicifyVariableName(kind.ToString())}", variableId,
                        kind, null, fieldName);
            }

            menu.ShowAsContext();
        }
    }


    [CustomPropertyDrawer(typeof(ScenarioVariableChoice))]
    internal class ScenarioVariableChoiceDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var allowedKinds = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<ScenarioAllowedVariableKindsAttribute>(fieldInfo);
            ScenarioVariableChoiceField.Draw(position, property, label, allowedKinds?.Kinds?.ToList());
        }
    }
}
