using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Как сравнить число счётчика с заданным: равно, больше, не меньше…</summary>
    public enum ScenarioComparison
    {
        [InspectorName("=")] Equal,
        [InspectorName("≠")] NotEqual,
        [InspectorName(">")] Greater,
        [InspectorName("≥")] GreaterOrEqual,
        [InspectorName("<")] Less,
        [InspectorName("≤")] LessOrEqual
    }


    public static class ScenarioComparisonExtensions
    {
        public static bool Holds(this ScenarioComparison comparison, int value, int other)
        {
            return comparison switch
            {
                ScenarioComparison.Equal => value == other,
                ScenarioComparison.NotEqual => value != other,
                ScenarioComparison.Greater => value > other,
                ScenarioComparison.GreaterOrEqual => value >= other,
                ScenarioComparison.Less => value < other,
                _ => value <= other
            };
        }

        public static string Symbol(this ScenarioComparison comparison)
        {
            return comparison switch
            {
                ScenarioComparison.Equal => "=",
                ScenarioComparison.NotEqual => "≠",
                ScenarioComparison.Greater => ">",
                ScenarioComparison.GreaterOrEqual => "≥",
                ScenarioComparison.Less => "<",
                _ => "≤"
            };
        }
    }
}
