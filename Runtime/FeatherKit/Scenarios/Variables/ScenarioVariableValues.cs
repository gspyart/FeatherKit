using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Значение переменной любого вида целиком: копия, равенство, «пусто», число — по виду переменной.
    /// Нужно блокам, которые работают с переменной, не зная её вида заранее: Set Variable, Compare Variable.
    /// </summary>
    public static class ScenarioVariableValues
    {
        /// <summary>Новая копия значения всех видов; список — свой.</summary>
        public static ScenarioVariableValue Clone(ScenarioVariableValue value)
        {
            var copy = new ScenarioVariableValue();
            CopyAll(value, copy);

            return copy;
        }

        private static void CopyAll(ScenarioVariableValue from, ScenarioVariableValue to)
        {
            to.FlagValue = from.FlagValue;
            to.CounterValue = from.CounterValue;
            to.NumberValue = from.NumberValue;
            to.ObjectValue = from.ObjectValue;
            to.VectorValue = from.VectorValue;
            to.TextValue = from.TextValue;
            to.ColorValue = from.ColorValue;
            to.ObjectListValue = new List<Object>(from.ObjectListValue);
        }


        /// <summary>Скопировать поле этого вида. Вернёт, изменилось ли значение.</summary>
        public static bool CopyKind(ScenarioVariableKind kind, ScenarioVariableValue from, ScenarioVariableValue to)
        {
            if (AreEqual(kind, from, to))
                return false;

            switch (kind)
            {
                case ScenarioVariableKind.Flag: to.FlagValue = from.FlagValue; break;
                case ScenarioVariableKind.Counter: to.CounterValue = from.CounterValue; break;
                case ScenarioVariableKind.Number: to.NumberValue = from.NumberValue; break;
                case ScenarioVariableKind.Vector3: to.VectorValue = from.VectorValue; break;
                case ScenarioVariableKind.Text: to.TextValue = from.TextValue; break;
                case ScenarioVariableKind.Color: to.ColorValue = from.ColorValue; break;
                case ScenarioVariableKind.ObjectList: ReplaceList(to.ObjectListValue, from.ObjectListValue); break;
                default: to.ObjectValue = from.ObjectValue; break;
            }

            return true;
        }

        // Живой список не подменяем: на него могут держать ссылку блоки списков.
        private static void ReplaceList(List<Object> target, List<Object> source)
        {
            if (ReferenceEquals(target, source))
                return;

            target.Clear();
            target.AddRange(source);
        }


        public static bool AreEqual(ScenarioVariableKind kind, ScenarioVariableValue left, ScenarioVariableValue right)
        {
            return kind switch
            {
                ScenarioVariableKind.Flag => left.FlagValue == right.FlagValue,
                ScenarioVariableKind.Counter => left.CounterValue == right.CounterValue,
                ScenarioVariableKind.Number => left.NumberValue.Equals(right.NumberValue),
                ScenarioVariableKind.Vector3 => left.VectorValue == right.VectorValue,
                ScenarioVariableKind.Text => string.Equals(left.TextValue ?? "", right.TextValue ?? "", StringComparison.Ordinal),
                ScenarioVariableKind.Color => left.ColorValue == right.ColorValue,
                ScenarioVariableKind.ObjectList => AreListsEqual(left.ObjectListValue, right.ObjectListValue),
                _ => left.ObjectValue == right.ObjectValue
            };
        }

        private static bool AreListsEqual(List<Object> left, List<Object> right)
        {
            if (left.Count != right.Count)
                return false;

            for (var index = 0; index < left.Count; index++)
            {
                if (left[index] != right[index])
                    return false;
            }

            return true;
        }


        /// <summary>Пусто: объекта нет, список пуст, текст пуст. У чисел, флагов, векторов и цветов пустоты нет.</summary>
        public static bool IsEmpty(ScenarioVariableKind kind, ScenarioVariableValue value)
        {
            return kind switch
            {
                ScenarioVariableKind.Object => value.ObjectValue == null,
                ScenarioVariableKind.ObjectList => value.ObjectListValue.Count == 0,
                ScenarioVariableKind.Text => string.IsNullOrEmpty(value.TextValue),
                _ => false
            };
        }


        /// <summary>Число из Counter или Number. False — у переменной этого вида числа нет.</summary>
        public static bool TryGetNumber(ScenarioVariableKind kind, ScenarioVariableValue value, out float number)
        {
            number = kind switch
            {
                ScenarioVariableKind.Counter => value.CounterValue,
                ScenarioVariableKind.Number => value.NumberValue,
                _ => 0f
            };

            return kind == ScenarioVariableKind.Counter || kind == ScenarioVariableKind.Number;
        }
    }
}
