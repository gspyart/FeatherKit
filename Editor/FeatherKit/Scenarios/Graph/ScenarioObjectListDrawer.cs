using System;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Поле «список объектов» (ScenarioObjectList): свой список или переменная Object List графа.
    /// - слева кнопка выбора, как у поля «значение или переменная»; свой список — строками под ней
    /// - тип элемента — из поля блока: в список и в выбор переменной попадает только подходящее
    /// </summary>
    [CustomPropertyDrawer(typeof(ScenarioObjectList<>))]
    internal class ScenarioObjectListDrawer : PropertyDrawer
    {
        private const string ItemsPropertyName = "items";


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return UsesVariable(property)
                ? EditorGUIUtility.singleLineHeight
                : ScenarioObjectListGUI.HeightOf(property.FindPropertyRelative(ItemsPropertyName));
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var itemType = ItemTypeOf(fieldInfo.FieldType);
            var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(itemType);
            var variableId = property.FindPropertyRelative(ScenarioVariablePicking.VariableIdPropertyName);
            var variable = UsesVariable(property) ? ScenarioVariablePicking.GraphOf(property)?.FindVariable(variableId.stringValue) : null;
            var problem = UsesVariable(property) ? ScenarioGraphValidation.VariableProblemOf(variable, listType) : null;

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var fieldRect = ScenarioVariableField.PrefixLabel(position, label, problem != null);
                var previousIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                var kindRect = ScenarioVariableField.KindButtonRect(fieldRect);
                var contentRect = ScenarioVariableField.ContentRect(fieldRect, kindRect);
                var buttonContent = problem != null ? new GUIContent(ScenarioIcons.Error, problem)
                    : variable != null ? new GUIContent(ScenarioIcons.IconOf(variable), $"Graph variable “{variable.Name}” — click to change")
                    : new GUIContent("=", "Own list — click to use an Object List variable instead");

                if (EditorGUI.DropdownButton(kindRect, buttonContent, FocusType.Passive, EditorStyles.miniButton))
                    ShowMenu(property, listType, label.text);

                if (UsesVariable(property))
                    ScenarioVariableField.DrawVariableName(contentRect, variable, problem);
                else
                    ScenarioObjectListGUI.Draw(contentRect, property.FindPropertyRelative(ItemsPropertyName), itemType);

                EditorGUI.indentLevel = previousIndent;
            }

            ScenarioVariablePicking.HandleVariableDrop(position, variableId, candidate => ScenarioVariableKinds.Fits(candidate, listType));
        }

        private static bool UsesVariable(SerializedProperty property)
        {
            return !string.IsNullOrEmpty(property.FindPropertyRelative(ScenarioVariablePicking.VariableIdPropertyName).stringValue);
        }

        // Поле может лежать в списке полей: тогда ScenarioObjectList<T> — у элемента.
        private static Type ItemTypeOf(Type fieldType)
        {
            while (fieldType != null && !(fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(ScenarioObjectList<>)))
                fieldType = fieldType.IsArray ? fieldType.GetElementType() : fieldType.IsGenericType ? fieldType.GetGenericArguments()[0] : null;

            return fieldType?.GetGenericArguments()[0] ?? typeof(Object);
        }


        private static void ShowMenu(SerializedProperty property, Type listType, string fieldName)
        {
            var serializedObject = property.serializedObject;
            var variableId = property.FindPropertyRelative(ScenarioVariablePicking.VariableIdPropertyName);
            var itemsPath = property.FindPropertyRelative(ItemsPropertyName).propertyPath;
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Own List"), string.IsNullOrEmpty(variableId.stringValue),
                () => ScenarioVariablePicking.SetVariableId(serializedObject, variableId.propertyPath, ""));
            menu.AddSeparator("");

            var graph = ScenarioVariablePicking.GraphOf(property);
            ScenarioVariablePicking.AddVariableItems(menu, variableId, ScenarioVariablePicking.FittingVariables(graph, listType));

            if (graph != null)
            {
                menu.AddSeparator("");
                ScenarioVariablePicking.AddNewVariableItem(menu, "New Variable…", variableId, listType, fieldName);

                if (string.IsNullOrEmpty(variableId.stringValue))
                    ScenarioVariablePicking.AddNewVariableItem(menu, "New Variable from Field", variableId, listType, fieldName,
                        value => ScenarioVariablePicking.CopyOwnValue(serializedObject.FindProperty(itemsPath), ScenarioVariableKind.ObjectList, value));
            }

            menu.ShowAsContext();
        }
    }


    /// <summary>
    /// Список объектов строками: шапка «N items» с «+», под ней поля с «−».
    /// Объекты можно бросить на шапку пачкой — каждый переводится в тип элемента (GameObject ↔ его компонент).
    /// </summary>
    internal static class ScenarioObjectListGUI
    {
        private const float SmallButtonWidth = 20f;


        public static float HeightOf(SerializedProperty items)
        {
            var lines = 1 + (items is { isArray: true } ? items.arraySize : 0);

            return lines * EditorGUIUtility.singleLineHeight + (lines - 1) * EditorGUIUtility.standardVerticalSpacing;
        }

        public static void Draw(Rect rect, SerializedProperty items, Type itemType)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight;
            var step = lineHeight + EditorGUIUtility.standardVerticalSpacing;
            var headerRect = new Rect(rect.x, rect.y, rect.width, lineHeight);

            DrawHeader(headerRect, items, itemType);

            for (var index = 0; index < items.arraySize; index++)
            {
                var rowRect = new Rect(rect.x, rect.y + step * (index + 1), rect.width, lineHeight);
                var fieldRect = new Rect(rowRect.x, rowRect.y, rowRect.width - SmallButtonWidth - ScenarioVariableField.Spacing, lineHeight);
                var removeRect = new Rect(rowRect.xMax - SmallButtonWidth, rowRect.y, SmallButtonWidth, lineHeight);

                ScenarioObjectFieldGUI.Draw(fieldRect, items.GetArrayElementAtIndex(index), itemType);

                // Без ExitGUI: тот, кто рисует список, после отрисовки ещё читает правку.
                if (GUI.Button(removeRect, new GUIContent("−", "Remove from the list"), EditorStyles.miniButton))
                {
                    items.DeleteArrayElementAtIndex(index);
                    break;
                }
            }
        }

        private static void DrawHeader(Rect rect, SerializedProperty items, Type itemType)
        {
            var labelRect = new Rect(rect.x, rect.y, rect.width - SmallButtonWidth - ScenarioVariableField.Spacing, rect.height);
            var addRect = new Rect(rect.xMax - SmallButtonWidth, rect.y, SmallButtonWidth, rect.height);
            var text = items.arraySize == 0 ? $"No {itemType.Name} — drop here" : items.arraySize == 1 ? "1 item" : $"{items.arraySize} items";

            EditorGUI.LabelField(labelRect, new GUIContent(text, ScenarioIcons.TypeIcon(itemType), $"A list of {itemType.Name}"), EditorStyles.helpBox);

            if (GUI.Button(addRect, new GUIContent("+", "Add an empty row"), EditorStyles.miniButton))
                items.arraySize++;

            HandleObjectsDrop(labelRect, items, itemType);
        }

        private static void HandleObjectsDrop(Rect rect, SerializedProperty items, Type itemType)
        {
            var currentEvent = Event.current;

            if (!rect.Contains(currentEvent.mousePosition) || DragAndDrop.objectReferences.Length == 0)
                return;

            if (currentEvent.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                var isGraphFile = EditorUtility.IsPersistent(items.serializedObject.targetObject);

                foreach (var dropped in DragAndDrop.objectReferences)
                {
                    var item = ScenarioVariableKinds.ConvertObject(dropped, itemType);

                    // Граф-файл объектов сцены не удержит — их место в переменной с привязкой.
                    if (item == null || (isGraphFile && !EditorUtility.IsPersistent(item)))
                        continue;

                    items.arraySize++;
                    items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = item;
                }

                currentEvent.Use();
            }
        }
    }
}
