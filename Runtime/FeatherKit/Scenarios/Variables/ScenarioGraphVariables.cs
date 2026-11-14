using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Живые значения переменных графа у одного экземпляра — у каждого Scenario свои. Часть Scenario.
    /// - на старте: начальное значение переменной, у открытой — значение из привязки на объекте
    /// - читает и пишет по id; сообщает, что значение изменилось
    /// - чужой вид или пропавшая переменная — ошибка, а не тихий ноль
    /// </summary>
    public class ScenarioGraphVariables
    {
        private readonly Dictionary<string, LiveGraphVariable> variablesById =
            new Dictionary<string, LiveGraphVariable>(StringComparer.Ordinal);


        public ScenarioGraphVariables(ScenarioGraph graph, IReadOnlyList<ScenarioVariableBinding> bindings)
        {
            if (graph == null)
                return;

            foreach (var definition in graph.Variables)
            {
                if (definition == null || string.IsNullOrEmpty(definition.Id))
                    continue;

                var startValue = definition.IsExposed ? BoundValueOf(definition, bindings) ?? definition.InitialValue
                    : definition.InitialValue;

                variablesById[definition.Id] = new LiveGraphVariable(definition, CopyOf(startValue));
            }
        }


        /// <summary>Значение переменной изменилось — кто бы его ни поменял.</summary>
        public event Action<ScenarioVariableDefinition> Changed;


        public bool Contains(string variableId)
        {
            return variableId != null && variablesById.ContainsKey(variableId);
        }

        /// <summary>Описание переменной этого экземпляра: вид, тип объекта, имя. False — такой переменной нет.</summary>
        public bool TryGetDefinition(string variableId, out ScenarioVariableDefinition definition)
        {
            definition = null;

            if (variableId == null || !variablesById.TryGetValue(variableId, out var variable))
                return false;

            definition = variable.Definition;

            return true;
        }


        // ── Значение по типу поля ────────────────────────────────────────

        /// <summary>Значение для поля типа T: объект переводится между GameObject и его компонентами.</summary>
        public T Get<T>(string variableId)
        {
            if (!ScenarioVariableKinds.TryGetKind(typeof(T), out var kind))
                throw new InvalidOperationException($"A field of type {typeof(T).Name} cannot read a graph variable");

            var value = Require(variableId, kind).Value;

            object result = kind switch
            {
                ScenarioVariableKind.Flag => value.FlagValue,
                ScenarioVariableKind.Counter => value.CounterValue,
                ScenarioVariableKind.Number => value.NumberValue,
                ScenarioVariableKind.Vector3 => value.VectorValue,
                ScenarioVariableKind.Text => value.TextValue,
                ScenarioVariableKind.Color => value.ColorValue,
                ScenarioVariableKind.ObjectList => ConvertObjectList<T>(variableId, value.ObjectListValue),
                _ => ConvertObject(variableId, value.ObjectValue, typeof(T))
            };

            return result is T typedResult ? typedResult : default;
        }

        // Список — новый, в типе поля: запись в него не меняет переменную, для этого есть GetObjectListLive.
        private object ConvertObjectList<T>(string variableId, List<Object> storedObjects)
        {
            ScenarioVariableKinds.TryGetListItemType(typeof(T), out var itemType);

            var converted = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType), storedObjects.Count);

            foreach (var storedObject in storedObjects)
                converted.Add(ConvertObject(variableId, storedObject, itemType));

            return converted;
        }


        /// <summary>Записать значение поля типа T: список объектов заменяет содержимое живого списка.</summary>
        public void Set<T>(string variableId, T value)
        {
            if (!ScenarioVariableKinds.TryGetKind(typeof(T), out var kind))
                throw new InvalidOperationException($"A field of type {typeof(T).Name} cannot write a graph variable");

            object boxedValue = value;

            switch (kind)
            {
                case ScenarioVariableKind.Flag: SetFlag(variableId, (bool)boxedValue); break;
                case ScenarioVariableKind.Counter: SetCounter(variableId, (int)boxedValue); break;
                case ScenarioVariableKind.Number: SetNumber(variableId, (float)boxedValue); break;
                case ScenarioVariableKind.Vector3: SetVector(variableId, (Vector3)boxedValue); break;
                case ScenarioVariableKind.Text: SetText(variableId, (string)boxedValue); break;
                case ScenarioVariableKind.Color: SetColor(variableId, (Color)boxedValue); break;
                case ScenarioVariableKind.ObjectList: ReplaceObjectList(variableId, (IEnumerable)boxedValue); break;
                default: SetObject(variableId, (Object)boxedValue); break;
            }
        }

        private void ReplaceObjectList(string variableId, IEnumerable objects)
        {
            var liveList = GetObjectListLive(variableId);
            liveList.Clear();

            if (objects != null)
            {
                foreach (var item in objects)
                    liveList.Add(item as Object);
            }

            MarkChanged(variableId);
        }


        // Привязали объект без нужного компонента — это ошибка настройки, а не «пусто».
        private Object ConvertObject(string variableId, Object storedObject, Type valueType)
        {
            var converted = ScenarioVariableKinds.ConvertObject(storedObject, valueType);

            if (converted == null && storedObject != null)
                throw new InvalidOperationException(
                    $"Graph variable “{variablesById[variableId].Definition.Name}” holds “{storedObject.name}” without {valueType.Name}");

            return converted;
        }


        // ── По видам ─────────────────────────────────────────────────────

        public bool GetFlag(string variableId)
        {
            return Require(variableId, ScenarioVariableKind.Flag).Value.FlagValue;
        }

        public void SetFlag(string variableId, bool value)
        {
            var variable = Require(variableId, ScenarioVariableKind.Flag);

            if (variable.Value.FlagValue == value)
                return;

            variable.Value.FlagValue = value;
            RaiseChanged(variable.Definition);
        }


        public int GetCounter(string variableId)
        {
            return Require(variableId, ScenarioVariableKind.Counter).Value.CounterValue;
        }

        public void SetCounter(string variableId, int value)
        {
            var variable = Require(variableId, ScenarioVariableKind.Counter);

            if (variable.Value.CounterValue == value)
                return;

            variable.Value.CounterValue = value;
            RaiseChanged(variable.Definition);
        }


        public float GetNumber(string variableId)
        {
            return Require(variableId, ScenarioVariableKind.Number).Value.NumberValue;
        }

        public void SetNumber(string variableId, float value)
        {
            var variable = Require(variableId, ScenarioVariableKind.Number);

            if (variable.Value.NumberValue.Equals(value))
                return;

            variable.Value.NumberValue = value;
            RaiseChanged(variable.Definition);
        }


        public Object GetObject(string variableId)
        {
            return Require(variableId, ScenarioVariableKind.Object).Value.ObjectValue;
        }

        public void SetObject(string variableId, Object value)
        {
            var variable = Require(variableId, ScenarioVariableKind.Object);

            if (variable.Value.ObjectValue == value)
                return;

            variable.Value.ObjectValue = value;
            RaiseChanged(variable.Definition);
        }


        public Vector3 GetVector(string variableId)
        {
            return Require(variableId, ScenarioVariableKind.Vector3).Value.VectorValue;
        }

        public void SetVector(string variableId, Vector3 value)
        {
            var variable = Require(variableId, ScenarioVariableKind.Vector3);

            if (variable.Value.VectorValue == value)
                return;

            variable.Value.VectorValue = value;
            RaiseChanged(variable.Definition);
        }


        public string GetText(string variableId)
        {
            return Require(variableId, ScenarioVariableKind.Text).Value.TextValue;
        }

        public void SetText(string variableId, string value)
        {
            var variable = Require(variableId, ScenarioVariableKind.Text);

            if (string.Equals(variable.Value.TextValue, value, StringComparison.Ordinal))
                return;

            variable.Value.TextValue = value;
            RaiseChanged(variable.Definition);
        }


        public Color GetColor(string variableId)
        {
            return Require(variableId, ScenarioVariableKind.Color).Value.ColorValue;
        }

        public void SetColor(string variableId, Color value)
        {
            var variable = Require(variableId, ScenarioVariableKind.Color);

            if (variable.Value.ColorValue == value)
                return;

            variable.Value.ColorValue = value;
            RaiseChanged(variable.Definition);
        }


        /// <summary>Сам живой список переменной: блоки списков меняют его на месте и зовут MarkChanged.</summary>
        public List<Object> GetObjectListLive(string variableId)
        {
            return Require(variableId, ScenarioVariableKind.ObjectList).Value.ObjectListValue;
        }

        /// <summary>Значение поменяли на месте, мимо Set: сообщить слушателям «Variable Changed».</summary>
        public void MarkChanged(string variableId)
        {
            if (variableId == null || !variablesById.TryGetValue(variableId, out var variable))
                throw new InvalidOperationException($"Graph variable “{variableId}” not found — it was removed from the graph");

            RaiseChanged(variable.Definition);
        }


        // Рассылка — отдельный «случай»: ожидание, начатое из-за этой перемены, её же не засчитывает.
        private void RaiseChanged(ScenarioVariableDefinition definition)
        {
            if (Changed == null)
                return;

            ScenarioEventOccurrences.Begin();

            try
            {
                Changed.Invoke(definition);
            }
            finally
            {
                ScenarioEventOccurrences.End();
            }
        }


        // ── Начальные значения ───────────────────────────────────────────

        private static ScenarioVariableValue BoundValueOf(ScenarioVariableDefinition definition,
            IReadOnlyList<ScenarioVariableBinding> bindings)
        {
            if (bindings == null)
                return null;

            foreach (var binding in bindings)
            {
                if (binding != null && string.Equals(binding.VariableId, definition.Id, StringComparison.Ordinal))
                    return binding.Value;
            }

            return null;
        }

        // Своя копия: запись в переменную не должна менять начальное значение в графе или привязку на объекте.
        private static ScenarioVariableValue CopyOf(ScenarioVariableValue value)
        {
            return new ScenarioVariableValue
            {
                FlagValue = value.FlagValue,
                CounterValue = value.CounterValue,
                NumberValue = value.NumberValue,
                ObjectValue = value.ObjectValue,
                VectorValue = value.VectorValue,
                TextValue = value.TextValue,
                ColorValue = value.ColorValue,
                ObjectListValue = new List<Object>(value.ObjectListValue)
            };
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private LiveGraphVariable Require(string variableId, ScenarioVariableKind kind)
        {
            if (variableId == null || !variablesById.TryGetValue(variableId, out var variable))
                throw new InvalidOperationException($"Graph variable “{variableId}” not found — it was removed from the graph");

            if (variable.Definition.Kind != kind)
                throw new InvalidOperationException(
                    $"Graph variable “{variable.Definition.Name}” is a {variable.Definition.Kind}, not a {kind}");

            return variable;
        }


        /// <summary>Переменная графа и её значение у этого экземпляра.</summary>
        private readonly struct LiveGraphVariable
        {
            public LiveGraphVariable(ScenarioVariableDefinition definition, ScenarioVariableValue value)
            {
                Definition = definition;
                Value = value;
            }


            public ScenarioVariableDefinition Definition { get; }

            public ScenarioVariableValue Value { get; }
        }
    }
}
