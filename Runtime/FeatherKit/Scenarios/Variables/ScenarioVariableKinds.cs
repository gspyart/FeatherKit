using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Какая переменная графа подходит полю какого типа — одно правило для редактора и рантайма.
    /// - bool — Flag, int — Counter, float — Number, Vector3, string — Text, Color, объект Unity — Object,
    ///   список объектов — Object List; прочие типы без переменных
    /// - объект переводится между GameObject и его компонентами: в переменной объект, поле ждёт Light — берём Light с него
    /// </summary>
    public static class ScenarioVariableKinds
    {
        /// <summary>Вид переменной для поля этого типа. False — такому полю переменная не положена.</summary>
        public static bool TryGetKind(Type valueType, out ScenarioVariableKind kind)
        {
            kind = default;

            if (valueType == typeof(bool))
                kind = ScenarioVariableKind.Flag;
            else if (valueType == typeof(int))
                kind = ScenarioVariableKind.Counter;
            else if (valueType == typeof(float))
                kind = ScenarioVariableKind.Number;
            else if (valueType == typeof(Vector3))
                kind = ScenarioVariableKind.Vector3;
            else if (valueType == typeof(string))
                kind = ScenarioVariableKind.Text;
            else if (valueType == typeof(Color))
                kind = ScenarioVariableKind.Color;
            else if (TryGetListItemType(valueType, out _))
                kind = ScenarioVariableKind.ObjectList;
            else if (valueType != null && typeof(Object).IsAssignableFrom(valueType))
                kind = ScenarioVariableKind.Object;
            else
                return false;

            return true;
        }


        /// <summary>Переменную можно выбрать в поле этого типа: вид совпал, а объект переводится в нужный тип.</summary>
        public static bool Fits(ScenarioVariableDefinition variable, Type valueType)
        {
            if (variable == null || !TryGetKind(valueType, out var kind) || kind != variable.Kind)
                return false;

            return kind switch
            {
                ScenarioVariableKind.Object => CanConvertObject(variable.ObjectType, valueType),
                ScenarioVariableKind.ObjectList => TryGetListItemType(valueType, out var itemType)
                                                   && CanConvertObject(variable.ObjectType, itemType),
                _ => true
            };
        }


        /// <summary>Тип элемента у поля-списка объектов: List&lt;T&gt; или IReadOnlyList&lt;T&gt;, где T — объект Unity.</summary>
        public static bool TryGetListItemType(Type valueType, out Type itemType)
        {
            itemType = null;

            if (valueType == null || !valueType.IsGenericType)
                return false;

            var definition = valueType.GetGenericTypeDefinition();

            if (definition != typeof(List<>) && definition != typeof(IReadOnlyList<>))
                return false;

            itemType = valueType.GetGenericArguments()[0];

            return typeof(Object).IsAssignableFrom(itemType);
        }


        /// <summary>Объект переменной типа storedType можно отдать полю типа valueType — сразу или через GetComponent / .gameObject.</summary>
        public static bool CanConvertObject(Type storedType, Type valueType)
        {
            if (valueType.IsAssignableFrom(storedType) || storedType.IsAssignableFrom(valueType))
                return true;

            return IsGameObjectOrComponent(storedType) && IsGameObjectOrComponent(valueType);
        }

        private static bool IsGameObjectOrComponent(Type type)
        {
            return type == typeof(GameObject) || typeof(Component).IsAssignableFrom(type);
        }


        /// <summary>Объект переменной в виде, который ждёт поле. Null — не тот тип или на объекте нет такого компонента.</summary>
        public static Object ConvertObject(Object storedObject, Type valueType)
        {
            if (storedObject == null || valueType == null)
                return null;

            if (valueType.IsInstanceOfType(storedObject))
                return storedObject;

            var gameObject = storedObject switch
            {
                GameObject storedGameObject => storedGameObject,
                Component storedComponent => storedComponent.gameObject,
                _ => null
            };

            if (gameObject == null)
                return null;

            if (valueType == typeof(GameObject))
                return gameObject;

            return typeof(Component).IsAssignableFrom(valueType) ? gameObject.GetComponent(valueType) : null;
        }
    }
}
