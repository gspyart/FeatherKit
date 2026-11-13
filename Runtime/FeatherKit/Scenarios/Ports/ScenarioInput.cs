using System;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Вход ноды графа: сюда приходит провод потока.
    /// Два входа равны, если у них один id: по id провод находит порт после загрузки графа.
    /// </summary>
    public sealed class ScenarioInput : IEquatable<ScenarioInput>
    {
        public ScenarioInput(string id, string label)
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

        // IEquatable<ScenarioInput>
        public bool Equals(ScenarioInput other)
        {
            return other is not null && string.Equals(Id, other.Id, StringComparison.Ordinal);
        }

        public override bool Equals(object other)
        {
            return Equals(other as ScenarioInput);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Id);
        }

        public static bool operator ==(ScenarioInput left, ScenarioInput right)
        {
            return left is null ? right is null : left.Equals(right);
        }

        public static bool operator !=(ScenarioInput left, ScenarioInput right)
        {
            return !(left == right);
        }


        public override string ToString()
        {
            return Id;
        }
    }
}
