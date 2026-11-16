using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FeatherKit.Scenarios;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Какие переменные граф заполняет сам — в них пишет поле «куда записать» (ScenarioVariableSlot) блока или ноды.
    /// Такой переменной не нужно значение снаружи: проверки графа это учитывают.
    /// </summary>
    internal static class ScenarioVariableWriters
    {
        private const int MaxDepth = 8;


        /// <summary>Переменные, которые граф заполняет сам: в них пишет поле «куда записать» какого-нибудь блока или ноды.</summary>
        public static HashSet<string> WrittenVariableIds(ScenarioGraph graph)
        {
            var writtenIds = new HashSet<string>();

            foreach (var node in graph.Nodes)
            {
                if (node != null)
                    CollectWrittenIds(node, writtenIds, 0);
            }

            return writtenIds;
        }

        private static void CollectWrittenIds(object owner, HashSet<string> writtenIds, int depth)
        {
            if (depth > MaxDepth)
                return;

            foreach (var field in ScenarioGraphValidation.SerializedFieldsOf(owner.GetType()))
            {
                switch (field.GetValue(owner))
                {
                    case null:
                        break;

                    case var slot when ScenarioGraphValidation.IsGeneric(slot.GetType(), typeof(ScenarioVariableSlot<>)):
                        var variableId = (string)slot.GetType().GetProperty(nameof(ScenarioVariableSlot<int>.VariableId))?.GetValue(slot);

                        if (!string.IsNullOrEmpty(variableId))
                            writtenIds.Add(variableId);

                        break;

                    case IList list:
                        foreach (var item in list)
                        {
                            if (item != null && (field.GetCustomAttribute<SerializeReference>() != null || ScenarioGraphValidation.IsNestedData(item.GetType())))
                                CollectWrittenIds(item, writtenIds, depth + 1);
                        }

                        break;

                    case var nested when ScenarioGraphValidation.IsNestedData(nested.GetType()):
                        CollectWrittenIds(nested, writtenIds, depth + 1);
                        break;
                }
            }
        }
    }
}
