using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Поле блока «какая переменная графа» — любого вида: Set Variable, Compare Variable, Variable Changed.
    /// Какие виды предлагать в списке, задаёт атрибут ScenarioAllowedVariableKinds на поле; без него — все.
    /// </summary>
    [Serializable]
    public class ScenarioVariableChoice
    {
        [Tooltip("The graph variable this block works with")]
        [SerializeField] private string variableId;


        public string VariableId
        {
            get => variableId;
            set => variableId = value;
        }

        public bool IsChosen => !string.IsNullOrEmpty(variableId);


        public static ScenarioVariableChoice FromVariable(string graphVariableId)
        {
            return new ScenarioVariableChoice { variableId = graphVariableId };
        }
    }


    /// <summary>Какие виды переменных предлагать в поле ScenarioVariableChoice.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ScenarioAllowedVariableKindsAttribute : Attribute
    {
        public ScenarioAllowedVariableKindsAttribute(params ScenarioVariableKind[] kinds)
        {
            Kinds = kinds;
        }


        public IReadOnlyList<ScenarioVariableKind> Kinds { get; }
    }
}
