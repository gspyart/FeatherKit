using System;
using System.Collections.Generic;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Поле ScenarioVariableValue или ScenarioVariableComparison берёт вид у соседнего выбора переменной:
    /// редактор рисует значение её вида и предлагает подходящие ей сравнения.
    /// Соседние «взять из другой переменной» прячут своё значение, когда выбраны.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ScenarioValueKindFromAttribute : Attribute
    {
        public ScenarioValueKindFromAttribute(string variableFieldName, params string[] replacingVariableFieldNames)
        {
            VariableFieldName = variableFieldName;
            ReplacingVariableFieldNames = replacingVariableFieldNames;
        }


        /// <summary>Соседнее поле ScenarioVariableChoice, чей вид у значения.</summary>
        public string VariableFieldName { get; }

        /// <summary>Соседние ScenarioVariableChoice: выбрана любая — своё значение не нужно, поле прячется.</summary>
        public IReadOnlyList<string> ReplacingVariableFieldNames { get; }

        /// <summary>Соседнее поле ScenarioVariableComparison: «пусто» или «не пусто» — значение не нужно. Пусто — не смотреть.</summary>
        public string ComparisonFieldName { get; set; }
    }
}
