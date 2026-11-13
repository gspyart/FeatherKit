using System;
using System.Collections.Generic;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Входы и выходы одной ноды по порядку — так они стоят на ноде.
    /// Её заполняет нода в DescribePorts; порт с уже занятым id пропускается.
    /// </summary>
    public class ScenarioPortList
    {
        private readonly List<ScenarioInput> inputs = new List<ScenarioInput>();
        private readonly List<ScenarioOutput> outputs = new List<ScenarioOutput>();


        public IReadOnlyList<ScenarioInput> Inputs => inputs;

        public IReadOnlyList<ScenarioOutput> Outputs => outputs;


        // ── Заполнение ───────────────────────────────────────────────────

        // Повтор id не роняет ноду: скопированный в инспекторе исход Wait For несёт тот же id, первый побеждает.
        public void AddInput(ScenarioInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (FindInput(input.Id) == null)
                inputs.Add(input);
        }

        public void AddOutput(ScenarioOutput output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            if (FindOutput(output.Id) == null)
                outputs.Add(output);
        }

        public void Clear()
        {
            inputs.Clear();
            outputs.Clear();
        }


        // ── Поиск ────────────────────────────────────────────────────────

        /// <summary>Вход с этим id или null.</summary>
        public ScenarioInput FindInput(string id)
        {
            foreach (var input in inputs)
            {
                if (string.Equals(input.Id, id, StringComparison.Ordinal))
                    return input;
            }

            return null;
        }

        /// <summary>Выход с этим id или null.</summary>
        public ScenarioOutput FindOutput(string id)
        {
            foreach (var output in outputs)
            {
                if (string.Equals(output.Id, id, StringComparison.Ordinal))
                    return output;
            }

            return null;
        }
    }
}
