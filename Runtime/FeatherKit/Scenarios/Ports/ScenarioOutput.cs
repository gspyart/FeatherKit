using System;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Выход ноды графа: отсюда уходят провода потока, с одного выхода — сколько угодно.
    /// Два выхода равны, если у них один id: по id провод находит порт после загрузки графа.
    /// </summary>
    public sealed class ScenarioOutput : IEquatable<ScenarioOutput>
    {
        public ScenarioOutput(string id, string label)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Port id is empty", nameof(id));

            Id = id;
            Label = label;
        }


        /// <summary>Постоянный ключ порта — его хранит провод. Меняется только вместе с проводами.</summary>
        public string Id { get; }

        /// <summary>Подпись порта на ноде.</summary>
        public string Label { get; }


        // ── Равенство по id ──────────────────────────────────────────────

        // IEquatable<ScenarioOutput>
        public bool Equals(ScenarioOutput other)
        {
            return other is not null && string.Equals(Id, other.Id, StringComparison.Ordinal);
        }

        public override bool Equals(object other)
        {
            return Equals(other as ScenarioOutput);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Id);
        }

        public static bool operator ==(ScenarioOutput left, ScenarioOutput right)
        {
            return left is null ? right is null : left.Equals(right);
        }

        public static bool operator !=(ScenarioOutput left, ScenarioOutput right)
        {
            return !(left == right);
        }


        public override string ToString()
        {
            return Id;
        }
    }
}
