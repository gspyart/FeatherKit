using System;
using System.Collections.Generic;
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
                _ => ConvertObject<T>(variableId, value.ObjectValue)
            };

            return result is T typedResult ? typedResult : default;
        }


        // Привязали объект без нужного компонента — это ошибка настройки, а не «пусто».
        private Object ConvertObject<T>(string variableId, Object storedObject)
        {
            var converted = ScenarioVariableKinds.ConvertObject(storedObject, typeof(T));

            if (converted == null && storedObject != null)
                throw new InvalidOperationException(
                    $"Graph variable “{variablesById[variableId].Definition.Name}” holds “{storedObject.name}” without {typeof(T).Name}");

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
                ObjectValue = value.ObjectValue
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
