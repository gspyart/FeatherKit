using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Какая переменная графа подходит полю какого типа — одно правило для редактора и рантайма.
    /// - bool — Flag, int — Counter, float — Number, объект Unity — Object; прочие типы без переменных
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

            return kind != ScenarioVariableKind.Object || CanConvertObject(variable.ObjectType, valueType);
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
