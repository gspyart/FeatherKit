using FeatherKit.Scenarios;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Отпечаток того, что холст рисует сам, а не через привязанные поля: ноды, порты, провода, переменные, заметки.
    /// Отпечаток другой — граф поменяли в обход холста (откат префаба, второе окно, инспектор): пора пересобрать.
    /// </summary>
    internal static class ScenarioGraphFingerprint
    {
        private const long Prime = 1099511628211L;
        private const long Seed = unchecked((long)14695981039346656037UL);


        public static long Of(ScenarioGraph graph)
        {
            if (graph == null)
                return 0L;

            var hash = Seed;

            foreach (var node in graph.Nodes)
                hash = AddNode(hash, node);

            foreach (var wire in graph.Wires)
            {
                hash = Add(hash, wire.FromNodeId);
                hash = Add(hash, wire.FromPortId);
                hash = Add(hash, wire.ToNodeId);
                hash = Add(hash, wire.ToPortId);
            }

            foreach (var variable in graph.Variables)
                hash = AddVariable(hash, variable);

            foreach (var note in graph.Notes)
            {
                hash = Add(hash, note?.Id);
                hash = Add(hash, note?.Text);
                hash = Add(hash, note?.Color.GetHashCode() ?? 0);
                hash = Add(hash, note?.Area.GetHashCode() ?? 0);
            }

            return hash;
        }

        // Подписи портов меняются вместе с исходами Wait For — их тоже в отпечаток.
        private static long AddNode(long hash, ScenarioNode node)
        {
            if (node == null)
                return Add(hash, -1);

            hash = Add(hash, node.GetType().FullName);
            hash = Add(hash, node.Id);
            hash = Add(hash, node.Position.GetHashCode());

            var ports = node.CollectPorts();

            foreach (var input in ports.Inputs)
                hash = Add(hash, input.Id);

            foreach (var output in ports.Outputs)
            {
                hash = Add(hash, output.Id);
                hash = Add(hash, output.Label);
            }

            return hash;
        }

        private static long AddVariable(long hash, ScenarioVariableDefinition variable)
        {
            if (variable == null)
                return Add(hash, -1);

            hash = Add(hash, variable.Id);
            hash = Add(hash, variable.Name);
            hash = Add(hash, (int)variable.Kind);
            hash = Add(hash, variable.ObjectType.FullName);
            hash = Add(hash, variable.IsExposed ? 1 : 0);

            return hash;
        }


        // ── Смешивание ───────────────────────────────────────────────────

        private static long Add(long hash, int value)
        {
            return unchecked((hash ^ value) * Prime);
        }

        // string.GetHashCode стабилен в пределах сессии редактора — отпечаток дальше сессии и не живёт.
        private static long Add(long hash, string value)
        {
            return Add(hash, value != null ? value.GetHashCode() : 0);
        }
    }
}
