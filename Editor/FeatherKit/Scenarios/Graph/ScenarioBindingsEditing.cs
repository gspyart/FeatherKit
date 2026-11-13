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
    /// Привязки граф-файла на одном объекте — чтение и запись без UI, общие для инспектора Scenario и окна графа.
    /// - значение открытой переменной здесь: своё (привязка) или начальное из файла
    /// - завести, убрать, почистить привязки к пропавшим переменным; найти открытые Object без значения
    /// Правит SerializedProperty компонента: undo и «изменена сцена» — его, а не граф-файла.
    /// </summary>
    internal class ScenarioBindingsEditing
    {
        public const string BindingsPropertyName = "variableBindings";

        private const string VariableIdPropertyName = "variableId";
        private const string ValuePropertyName = "value";


        public ScenarioBindingsEditing(SerializedObject serializedScenario)
        {
            SerializedScenario = serializedScenario;
        }


        public SerializedObject SerializedScenario { get; }

        public Scenario Scenario => (Scenario)SerializedScenario.targetObject;

        private SerializedProperty Bindings => SerializedScenario.FindProperty(BindingsPropertyName);


        /// <summary>Привязки работают только у граф-файла: во встроенном графе объекты сцены кладутся прямо в переменную.</summary>
        public static bool AreUsedBy(Scenario scenario)
        {
            return scenario != null && scenario.GraphSource == ScenarioGraphSource.Asset;
        }

        /// <summary>Тип, который живёт только в сцене: такой объект граф-файл получит лишь через привязку.</summary>
        public static bool IsSceneType(Type type)
        {
            return type == typeof(GameObject) || typeof(Component).IsAssignableFrom(type);
        }


        // ── Значения ─────────────────────────────────────────────────────

        /// <summary>Значение привязки переменной на этом объекте (ScenarioVariableValue). Null — своего значения нет.</summary>
        public SerializedProperty ValueOf(string variableId)
        {
            var index = IndexOf(variableId);

            return index >= 0 ? Bindings.GetArrayElementAtIndex(index).FindPropertyRelative(ValuePropertyName) : null;
        }

        /// <summary>Новая привязка. Новый элемент массива копирует соседа — значения сбрасываются.</summary>
        public SerializedProperty AddBinding(string variableId)
        {
            var existing = ValueOf(variableId);

            if (existing != null)
                return existing;

            var bindings = Bindings;
            bindings.arraySize++;

            var binding = bindings.GetArrayElementAtIndex(bindings.arraySize - 1);
            binding.FindPropertyRelative(VariableIdPropertyName).stringValue = variableId;

            var value = binding.FindPropertyRelative(ValuePropertyName);
            value.FindPropertyRelative("flagValue").boolValue = false;
            value.FindPropertyRelative("counterValue").intValue = 0;
            value.FindPropertyRelative("numberValue").floatValue = 0f;
            value.FindPropertyRelative("objectValue").objectReferenceValue = null;

            return value;
        }

        public void RemoveBinding(string variableId)
        {
            var index = IndexOf(variableId);

            if (index >= 0)
                Bindings.DeleteArrayElementAtIndex(index);
        }

        private int IndexOf(string variableId)
        {
            var bindings = Bindings;

            for (var index = 0; index < bindings.arraySize; index++)
            {
                if (bindings.GetArrayElementAtIndex(index).FindPropertyRelative(VariableIdPropertyName).stringValue == variableId)
                    return index;
            }

            return -1;
        }


        // ── Проверки ─────────────────────────────────────────────────────

        /// <summary>Открытые переменные Object, у которых на этом объекте нет ни своего значения, ни начального.</summary>
        public List<ScenarioVariableDefinition> ObjectVariablesWithoutValue(ScenarioGraph graph)
        {
            if (graph == null)
                return new List<ScenarioVariableDefinition>();

            return graph.Variables
                .Where(variable => variable is { IsExposed: true, Kind: ScenarioVariableKind.Object })
                .Where(variable => ObjectValueOf(variable) == null)
                .ToList();
        }

        private Object ObjectValueOf(ScenarioVariableDefinition variable)
        {
            var bound = ValueOf(variable.Id);

            return bound != null ? bound.FindPropertyRelative("objectValue").objectReferenceValue : variable.InitialValue.ObjectValue;
        }


        public int CountUnused(ScenarioGraph graph)
        {
            return UnusedIndices(graph).Count;
        }

        /// <summary>Убрать привязки к переменным, которых в графе нет или которые больше не открыты.</summary>
        public void RemoveUnused(ScenarioGraph graph)
        {
            var unused = UnusedIndices(graph);

            for (var index = unused.Count - 1; index >= 0; index--)
                Bindings.DeleteArrayElementAtIndex(unused[index]);
        }

        private List<int> UnusedIndices(ScenarioGraph graph)
        {
            var bindings = Bindings;
            var unused = new List<int>();

            for (var index = 0; index < bindings.arraySize; index++)
            {
                var variableId = bindings.GetArrayElementAtIndex(index).FindPropertyRelative(VariableIdPropertyName).stringValue;

                if (graph?.FindVariable(variableId) is not { IsExposed: true })
                    unused.Add(index);
            }

            return unused;
        }
    }
}
