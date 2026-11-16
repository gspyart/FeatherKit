using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Как сравнить переменную любого вида. Больше-меньше — только у чисел, «пусто» — у объектов, списков, текста.</summary>
    public enum ScenarioVariableComparison
    {
        [InspectorName("=")] Equal,
        [InspectorName("≠")] NotEqual,
        [InspectorName("<")] Less,
        [InspectorName(">")] Greater,
        [InspectorName("≤")] LessOrEqual,
        [InspectorName("≥")] GreaterOrEqual,
        [InspectorName("Is Empty")] IsEmpty,
        [InspectorName("Is Not Empty")] IsNotEmpty
    }


    public static class ScenarioVariableComparisonExtensions
    {
        public static bool Holds(this ScenarioVariableComparison comparison, float value, float other)
        {
            return comparison switch
            {
                ScenarioVariableComparison.Equal => Mathf.Approximately(value, other),
                ScenarioVariableComparison.NotEqual => !Mathf.Approximately(value, other),
                ScenarioVariableComparison.Less => value < other,
                ScenarioVariableComparison.Greater => value > other,
                ScenarioVariableComparison.LessOrEqual => value <= other,
                ScenarioVariableComparison.GreaterOrEqual => value >= other,
                _ => false
            };
        }

        public static string Symbol(this ScenarioVariableComparison comparison)
        {
            return comparison switch
            {
                ScenarioVariableComparison.Equal => "=",
                ScenarioVariableComparison.NotEqual => "≠",
                ScenarioVariableComparison.Less => "<",
                ScenarioVariableComparison.Greater => ">",
                ScenarioVariableComparison.LessOrEqual => "≤",
                ScenarioVariableComparison.GreaterOrEqual => "≥",
                ScenarioVariableComparison.IsEmpty => "is empty",
                _ => "is not empty"
            };
        }

        /// <summary>Сравнение — «пусто» или «не пусто»: второе значение не нужно.</summary>
        public static bool IsEmptinessCheck(this ScenarioVariableComparison comparison)
        {
            return comparison == ScenarioVariableComparison.IsEmpty || comparison == ScenarioVariableComparison.IsNotEmpty;
        }
    }
}
