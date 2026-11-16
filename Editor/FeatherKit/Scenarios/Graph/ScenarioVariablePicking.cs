using System;
using System.Collections.Generic;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Как поле блока выбирает переменную графа — общее для всех полей с переменной.
    /// - граф, в котором лежит поле; подходящие ему переменные; пункты меню выбора
    /// - «New Variable…» и «New Variable from Field»: переменная по типу поля, в граф-файле объект сцены — сразу Set on Object
    /// - переменная, которую сейчас тащат из панели переменных, — для подсветки и броска на поле
    /// </summary>
    internal static class ScenarioVariablePicking
    {
        public const string VariableIdPropertyName = "variableId";

        // Так GraphView кладёт перетаскиваемое выделение (и строку панели переменных) в DragAndDrop.
        private const string DragSelectionKey = "DragSelection";
        private const string GraphPathPrefix = ScenarioGraphHost.GraphPropertyPath + ".";
        private const string NewVariableUndoName = "Add Scenario Variable";


        // ── Граф поля ────────────────────────────────────────────────────

        /// <summary>Граф, в котором лежит поле: граф-файл или встроенный граф сценария. Null — поле вне графа.</summary>
        public static ScenarioGraph GraphOf(SerializedProperty property)
        {
            if (!property.propertyPath.StartsWith(GraphPathPrefix, StringComparison.Ordinal))
                return null;

            return property.serializedObject.targetObject switch
            {
                ScenarioGraphAsset asset => asset.Graph,
                Scenario { GraphSource: ScenarioGraphSource.Embedded } scenario => scenario.Graph,
                _ => null
            };
        }

        public static List<ScenarioVariableDefinition> FittingVariables(ScenarioGraph graph, Type valueType)
        {
            return graph == null
                ? new List<ScenarioVariableDefinition>()
                : graph.Variables.Where(variable => ScenarioVariableKinds.Fits(variable, valueType)).ToList();
        }


        // ── Меню выбора ──────────────────────────────────────────────────

        /// <summary>Пункты переменных: выбор пишет id в поле variableId. Пусто — серый пункт с причиной.</summary>
        public static void AddVariableItems(GenericMenu menu, SerializedProperty variableId, IReadOnlyList<ScenarioVariableDefinition> variables,
            Action<SerializedProperty> onChosen = null)
        {
            var serializedObject = variableId.serializedObject;
            var variableIdPath = variableId.propertyPath;
            var currentId = variableId.stringValue;

            foreach (var variable in variables)
            {
                var chosenId = variable.Id;
                menu.AddItem(new GUIContent($"{variable.Name}   ({KindTextOf(variable)})"), chosenId == currentId,
                    () => SetVariableId(serializedObject, variableIdPath, chosenId, onChosen));
            }

            if (variables.Count == 0)
                menu.AddDisabledItem(new GUIContent(GraphOf(variableId) == null ? "Graph variables work only inside a graph" : "No fitting graph variables"));
        }

        /// <summary>«New Variable…»: переменная под тип поля, сразу выбранная в нём. fillValue — начальное значение из поля.</summary>
        public static void AddNewVariableItem(GenericMenu menu, string itemText, SerializedProperty variableId, Type valueType, string variableName,
            Action<ScenarioVariableValue> fillValue = null, Action<SerializedProperty> onChosen = null)
        {
            if (GraphOf(variableId) == null || !ScenarioVariableKinds.TryGetKind(valueType, out var kind))
                return;

            AddNewVariableItem(menu, itemText, variableId, kind, ObjectTypeFor(valueType, kind), variableName, fillValue, onChosen);
        }

        public static void AddNewVariableItem(GenericMenu menu, string itemText, SerializedProperty variableId, ScenarioVariableKind kind,
            Type objectType, string variableName, Action<ScenarioVariableValue> fillValue = null, Action<SerializedProperty> onChosen = null)
        {
            var serializedObject = variableId.serializedObject;
            var variableIdPath = variableId.propertyPath;

            menu.AddItem(new GUIContent(itemText), false, () =>
            {
                var created = CreateVariable(serializedObject, kind, objectType, variableName, fillValue);

                if (created != null)
                    SetVariableId(serializedObject, variableIdPath, created.Id, onChosen);
            });
        }

        // Тип объекта у переменной — тип поля, у списка — тип элемента: так она сразу подходит полю.
        private static Type ObjectTypeFor(Type valueType, ScenarioVariableKind kind)
        {
            return kind switch
            {
                ScenarioVariableKind.Object => valueType,
                ScenarioVariableKind.ObjectList => ScenarioVariableKinds.TryGetListItemType(valueType, out var itemType) ? itemType : null,
                _ => null
            };
        }

        public static void SetVariableId(SerializedObject serializedObject, string variableIdPath, string variableId,
            Action<SerializedProperty> onChosen = null)
        {
            if (serializedObject.targetObject == null)
                return;

            serializedObject.Update();
            var property = serializedObject.FindProperty(variableIdPath);
            property.stringValue = variableId;
            onChosen?.Invoke(property);
            serializedObject.ApplyModifiedProperties();
        }


        // ── Новая переменная ─────────────────────────────────────────────

        /// <summary>Переменная в графе объекта поля, с undo. В граф-файле объект сцены — сразу «Set on Object»: иначе файлу его не удержать.</summary>
        public static ScenarioVariableDefinition CreateVariable(SerializedObject serializedObject, ScenarioVariableKind kind, Type objectType,
            string variableName, Action<ScenarioVariableValue> fillValue)
        {
            var owner = serializedObject.targetObject;
            var graph = owner switch
            {
                ScenarioGraphAsset asset => asset.Graph,
                Scenario scenario => scenario.Graph,
                _ => null
            };

            if (graph == null)
                return null;

            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(owner, NewVariableUndoName);

            var variable = new ScenarioVariableDefinition(ScenarioGraphEditing.UniqueVariableName(graph, variableName), kind);

            if (objectType != null)
                variable.ObjectType = objectType;

            variable.IsExposed = owner is ScenarioGraphAsset && objectType != null && ScenarioBindingsEditing.IsSceneType(objectType);
            fillValue?.Invoke(variable.InitialValue);
            graph.AddVariable(variable);

            if (PrefabUtility.IsPartOfPrefabInstance(owner))
                PrefabUtility.RecordPrefabInstancePropertyModifications(owner);

            EditorUtility.SetDirty(owner);
            serializedObject.Update();

            return variable;
        }

        /// <summary>Своё значение поля того же вида, что переменная: у Flag Reference своё значение — ассет, его не перенести.</summary>
        public static bool CanCopyOwnValue(SerializedProperty ownValue, ScenarioVariableKind kind)
        {
            if (ownValue == null)
                return false;

            return kind switch
            {
                ScenarioVariableKind.Flag => ownValue.propertyType == SerializedPropertyType.Boolean,
                ScenarioVariableKind.Counter => ownValue.propertyType == SerializedPropertyType.Integer,
                ScenarioVariableKind.Number => ownValue.propertyType == SerializedPropertyType.Float,
                ScenarioVariableKind.Object => ownValue.propertyType == SerializedPropertyType.ObjectReference,
                ScenarioVariableKind.Vector3 => ownValue.propertyType == SerializedPropertyType.Vector3,
                ScenarioVariableKind.Text => ownValue.propertyType == SerializedPropertyType.String,
                ScenarioVariableKind.Color => ownValue.propertyType == SerializedPropertyType.Color,
                _ => ownValue.isArray
            };
        }

        /// <summary>Своё значение поля — в начальное значение переменной: «New Variable from Field» не теряет того, что уже выставлено.</summary>
        public static void CopyOwnValue(SerializedProperty ownValue, ScenarioVariableKind kind, ScenarioVariableValue target)
        {
            switch (kind)
            {
                case ScenarioVariableKind.Flag:
                    target.FlagValue = ownValue.boolValue;
                    break;

                case ScenarioVariableKind.Counter:
                    target.CounterValue = ownValue.intValue;
                    break;

                case ScenarioVariableKind.Number:
                    target.NumberValue = ownValue.floatValue;
                    break;

                case ScenarioVariableKind.Object:
                    target.ObjectValue = ownValue.objectReferenceValue;
                    break;

                case ScenarioVariableKind.Vector3:
                    target.VectorValue = ownValue.vector3Value;
                    break;

                case ScenarioVariableKind.Text:
                    target.TextValue = ownValue.stringValue;
                    break;

                case ScenarioVariableKind.Color:
                    target.ColorValue = ownValue.colorValue;
                    break;

                case ScenarioVariableKind.ObjectList when ownValue.isArray:
                    target.ObjectListValue = Enumerable.Range(0, ownValue.arraySize)
                        .Select(index => ownValue.GetArrayElementAtIndex(index).objectReferenceValue).ToList();
                    break;
            }
        }


        // ── Перетаскивание из панели переменных ──────────────────────────

        /// <summary>Переменная, которую сейчас тащат из панели переменных; null — тащат не её или ничего.</summary>
        public static string DraggedVariableId()
        {
            if (DragAndDrop.GetGenericData(DragSelectionKey) is not IEnumerable<object> dragged)
                return null;

            foreach (var item in dragged)
            {
                if (item is UnityEditor.Experimental.GraphView.BlackboardField { userData: string variableId })
                    return variableId;
            }

            return null;
        }

        /// <summary>
        /// Бросок переменной на поле: подходящее подсвечено и принимает её, неподходящее — тусклое.
        /// Звать в OnGUI поля после отрисовки; true — переменную только что выбрали.
        /// </summary>
        public static bool HandleVariableDrop(Rect rect, SerializedProperty variableId, Func<ScenarioVariableDefinition, bool> fits)
        {
            var draggedId = DraggedVariableId();
            var graph = draggedId != null ? GraphOf(variableId) : null;
            var variable = graph?.FindVariable(draggedId);

            if (variable == null)
                return false;

            var isFitting = fits(variable);
            var currentEvent = Event.current;

            if (currentEvent.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, isFitting ? new Color(0.3f, 0.8f, 0.4f, 0.25f) : new Color(0f, 0f, 0f, 0.35f));

            if (!isFitting || !rect.Contains(currentEvent.mousePosition))
                return false;

            if (currentEvent.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                variableId.stringValue = variable.Id;
                currentEvent.Use();
                return true;
            }

            return false;
        }


        // ── Общие помощники ──────────────────────────────────────────────

        public static string KindTextOf(ScenarioVariableDefinition variable)
        {
            return variable.Kind switch
            {
                ScenarioVariableKind.Object => variable.ObjectType == typeof(Object) ? "Object" : variable.ObjectType.Name,
                ScenarioVariableKind.ObjectList => variable.ObjectType == typeof(Object) ? "Object List" : $"{variable.ObjectType.Name} List",
                _ => variable.Kind.ToString()
            };
        }
    }
}
