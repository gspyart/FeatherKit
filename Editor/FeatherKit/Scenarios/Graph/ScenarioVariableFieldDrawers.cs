using System;
using System.Collections.Generic;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Поле «своё значение или переменная графа» — одно на ScenarioValue, ScenarioFlagReference и ScenarioCounterReference.
    /// - слева узкая кнопка: «Own Value», подходящая переменная графа, «New Variable…», «New Variable from Field»
    /// - выбранная переменная — её имя; не найдена или не того вида — красным, причина в подсказке
    /// - переменную можно бросить на поле из панели переменных
    /// Вне графа (старые списки Scenario) переменных нет — только своё значение.
    /// </summary>
    internal static class ScenarioVariableField
    {
        public const float KindButtonWidth = 20f;
        public const float Spacing = 2f;

        private static GUIStyle variableNameStyle;


        private static GUIStyle VariableNameStyle => variableNameStyle ??= new GUIStyle(EditorStyles.textField)
        {
            fontStyle = FontStyle.Bold,
            imagePosition = ImagePosition.ImageLeft
        };

        // В светлой теме голубой на белом поле не читается — там тёмно-синий.
        private static Color VariableColor => EditorGUIUtility.isProSkin ? new Color(0.55f, 0.8f, 1f) : new Color(0.05f, 0.3f, 0.65f);

        public static Color ProblemColor => EditorGUIUtility.isProSkin ? new Color(1f, 0.4f, 0.4f) : new Color(0.75f, 0.1f, 0.1f);


        public static float HeightOf(SerializedProperty property, string valuePropertyName)
        {
            return UsesVariable(property)
                ? EditorGUIUtility.singleLineHeight
                : Mathf.Max(EditorGUIUtility.singleLineHeight,
                    EditorGUI.GetPropertyHeight(property.FindPropertyRelative(valuePropertyName), GUIContent.none, true));
        }


        /// <summary>valueType — какие переменные подходят полю; valueObjectType — тип объекта своего значения, если это объект.</summary>
        public static void Draw(Rect position, SerializedProperty property, GUIContent label, string valuePropertyName, Type valueType,
            Type valueObjectType)
        {
            var variableId = property.FindPropertyRelative(ScenarioVariablePicking.VariableIdPropertyName);
            var variable = UsesVariable(property) ? ScenarioVariablePicking.GraphOf(property)?.FindVariable(variableId.stringValue) : null;
            var problem = UsesVariable(property) ? ScenarioGraphValidation.VariableProblemOf(variable, valueType) : null;
            var ownValue = property.FindPropertyRelative(valuePropertyName);

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var fieldRect = PrefixLabel(position, label, problem != null);
                var previousIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                var kindRect = KindButtonRect(fieldRect);
                var contentRect = ContentRect(fieldRect, kindRect);

                if (EditorGUI.DropdownButton(kindRect, KindButtonContent(variable, problem), FocusType.Passive, EditorStyles.miniButton))
                    ShowVariableMenu(variableId, ownValue, valueType, label.text);

                if (UsesVariable(property))
                    DrawVariableName(contentRect, variable, problem);
                else
                    DrawValue(contentRect, ownValue, valueObjectType);

                EditorGUI.indentLevel = previousIndent;
            }

            ScenarioVariablePicking.HandleVariableDrop(position, variableId, candidate => ScenarioVariableKinds.Fits(candidate, valueType));
        }

        private static bool UsesVariable(SerializedProperty property)
        {
            return !string.IsNullOrEmpty(property.FindPropertyRelative(ScenarioVariablePicking.VariableIdPropertyName).stringValue);
        }

        private static GUIContent KindButtonContent(ScenarioVariableDefinition variable, string problem)
        {
            if (problem != null)
                return new GUIContent(ScenarioIcons.Error, problem);

            return variable != null
                ? new GUIContent(ScenarioIcons.IconOf(variable), $"Graph variable “{variable.Name}” — click to change")
                : new GUIContent("=", "Own value — click to use a graph variable instead");
        }

        private static void DrawValue(Rect rect, SerializedProperty value, Type valueObjectType)
        {
            if (value.propertyType == SerializedPropertyType.ObjectReference && valueObjectType != null)
                ScenarioObjectFieldGUI.Draw(rect, value, valueObjectType);
            else
                EditorGUI.PropertyField(rect, value, GUIContent.none, true);
        }


        private static void ShowVariableMenu(SerializedProperty variableId, SerializedProperty ownValue, Type valueType, string fieldName)
        {
            var serializedObject = variableId.serializedObject;
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Own Value"), string.IsNullOrEmpty(variableId.stringValue),
                () => ScenarioVariablePicking.SetVariableId(serializedObject, variableId.propertyPath, ""));
            menu.AddSeparator("");

            var graph = ScenarioVariablePicking.GraphOf(variableId);
            ScenarioVariablePicking.AddVariableItems(menu, variableId, ScenarioVariablePicking.FittingVariables(graph, valueType));

            if (graph != null && ScenarioVariableKinds.TryGetKind(valueType, out var kind))
            {
                menu.AddSeparator("");
                ScenarioVariablePicking.AddNewVariableItem(menu, "New Variable…", variableId, valueType, fieldName);

                if (string.IsNullOrEmpty(variableId.stringValue) && ScenarioVariablePicking.CanCopyOwnValue(ownValue, kind))
                {
                    var ownValuePath = ownValue.propertyPath;
                    ScenarioVariablePicking.AddNewVariableItem(menu, "New Variable from Field", variableId, valueType, fieldName,
                        value => ScenarioVariablePicking.CopyOwnValue(serializedObject.FindProperty(ownValuePath), kind, value));
                }
            }

            menu.ShowAsContext();
        }


        // ── Общие помощники ──────────────────────────────────────────────

        /// <summary>Подпись поля: при проблеме — красным.</summary>
        public static Rect PrefixLabel(Rect position, GUIContent label, bool hasProblem)
        {
            var previousColor = GUI.contentColor;

            if (hasProblem)
                GUI.contentColor = ProblemColor;

            var fieldRect = EditorGUI.PrefixLabel(position, label);
            GUI.contentColor = previousColor;

            return fieldRect;
        }

        public static Rect KindButtonRect(Rect fieldRect)
        {
            return new Rect(fieldRect.x, fieldRect.y, KindButtonWidth, EditorGUIUtility.singleLineHeight);
        }

        public static Rect ContentRect(Rect fieldRect, Rect kindRect)
        {
            return new Rect(kindRect.xMax + Spacing, fieldRect.y, fieldRect.xMax - kindRect.xMax - Spacing, fieldRect.height);
        }

        /// <summary>Имя выбранной переменной жирным и цветом переменных; не найдена или не подходит — красным, причина в подсказке.</summary>
        public static void DrawVariableName(Rect rect, ScenarioVariableDefinition variable, string problem)
        {
            var previousColor = GUI.contentColor;
            GUI.contentColor = problem != null ? ProblemColor : VariableColor;

            var text = variable != null ? variable.Name : "Variable not found";
            var tooltip = problem ?? $"{ScenarioVariablePicking.KindTextOf(variable)} variable";
            EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), new GUIContent(text, tooltip),
                VariableNameStyle);

            GUI.contentColor = previousColor;
        }
    }


    /// <summary>
    /// Поле объекта на узкой ноде: пустое подписано «None» с иконкой типа, а не «None (Scenario Flag)» —
    /// длинная подпись съедала всю ширину поля.
    /// </summary>
    internal static class ScenarioObjectFieldGUI
    {
        private const float PickerButtonWidth = 19f;
        private const string PickerButtonStyleName = "ObjectFieldButton";

        private static GUIStyle pickerButtonStyle;


        private static GUIStyle PickerButtonStyle => pickerButtonStyle ??= GUI.skin.FindStyle(PickerButtonStyleName) ?? EditorStyles.miniButton;


        // Пустое поле Unity рисует сам, а мы поверх — короткую подпись; клики и перетаскивание остаются его.
        public static void Draw(Rect rect, SerializedProperty property, Type objectType)
        {
            var allowsSceneObjects = !EditorUtility.IsPersistent(property.serializedObject.targetObject);

            using (new EditorGUI.PropertyScope(rect, GUIContent.none, property))
            {
                EditorGUI.BeginChangeCheck();
                var value = EditorGUI.ObjectField(rect, GUIContent.none, property.objectReferenceValue, objectType, allowsSceneObjects);

                if (EditorGUI.EndChangeCheck())
                    property.objectReferenceValue = value;
            }

            if (property.objectReferenceValue != null || Event.current.type != EventType.Repaint)
                return;

            var buttonRect = new Rect(rect.xMax - PickerButtonWidth, rect.y, PickerButtonWidth, rect.height);
            EditorStyles.objectField.Draw(rect, new GUIContent("None", ScenarioIcons.TypeIcon(objectType)), false, false, false, false);
            PickerButtonStyle.Draw(buttonRect, GUIContent.none, false, false, false, false);
        }
    }


    [CustomPropertyDrawer(typeof(ScenarioValue<>))]
    internal class ScenarioValueDrawer : PropertyDrawer
    {
        private const string ValuePropertyName = "value";


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return ScenarioVariableField.HeightOf(property, ValuePropertyName);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var valueType = ValueTypeOf(fieldInfo.FieldType);
            var valueObjectType = valueType != null && typeof(Object).IsAssignableFrom(valueType) ? valueType : null;

            ScenarioVariableField.Draw(position, property, label, ValuePropertyName, valueType, valueObjectType);
        }

        // Поле может быть и списком значений: тогда тип — у элемента.
        private static Type ValueTypeOf(Type fieldType)
        {
            if (fieldType.IsArray)
                fieldType = fieldType.GetElementType();
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
                fieldType = fieldType.GetGenericArguments()[0];

            return fieldType != null && fieldType.IsGenericType ? fieldType.GetGenericArguments()[0] : null;
        }
    }


    [CustomPropertyDrawer(typeof(ScenarioFlagReference))]
    internal class ScenarioFlagReferenceDrawer : PropertyDrawer
    {
        private const string FlagPropertyName = "flag";


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return ScenarioVariableField.HeightOf(property, FlagPropertyName);
        }

        // Поле — ассет флага, а переменные к нему подходят виды Flag: тип для выбора — bool.
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ScenarioVariableField.Draw(position, property, label, FlagPropertyName, typeof(bool), typeof(ScenarioFlag));
        }
    }


    [CustomPropertyDrawer(typeof(ScenarioCounterReference))]
    internal class ScenarioCounterReferenceDrawer : PropertyDrawer
    {
        private const string CounterPropertyName = "counter";


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return ScenarioVariableField.HeightOf(property, CounterPropertyName);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ScenarioVariableField.Draw(position, property, label, CounterPropertyName, typeof(int), typeof(ScenarioCounter));
        }
    }
}
