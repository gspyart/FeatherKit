namespace FeatherKit.Scenarios
{
    /// <summary>Выходы, которых столько, сколько задано в ноде: «Branch 1», «Branch 2»… Id — «префикс.номер».</summary>
    internal static class ScenarioNumberedOutputs
    {
        public static ScenarioOutput Create(string idPrefix, string labelPrefix, int number)
        {
            return new ScenarioOutput($"{idPrefix}.{number}", $"{labelPrefix} {number}");
        }

        /// <summary>Входы типа, затем выходы с номерами, затем постоянные выходы типа — так они стоят на ноде.</summary>
        public static void Describe(ScenarioNode node, ScenarioPortList ports, string idPrefix, string labelPrefix, int count)
        {
            foreach (var input in ScenarioNodePorts.InputsOf(node.GetType()))
                ports.AddInput(input);

            for (var number = 1; number <= count; number++)
                ports.AddOutput(Create(idPrefix, labelPrefix, number));

            foreach (var output in ScenarioNodePorts.OutputsOf(node.GetType()))
                ports.AddOutput(output);
        }
    }
}
