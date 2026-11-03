using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Вложенный набор условий внутри блока: «all of» или «at least one of».
    /// Из таких наборов, вложенных друг в друга, собирается любая логика вида (a или b) и (b или c).
    /// </summary>
    [Serializable]
    public class ScenarioConditionList
    {
        [SerializeField] private ScenarioConditionsMode mode;
        [SerializeReference] private List<ScenarioCondition> conditions = new List<ScenarioCondition>();


        public int Count => conditions.Count;

        public ScenarioConditionsMode Mode => mode;


        public bool IsMet(ScenarioRun run)
        {
            return AreMet(conditions, mode, run);
        }


        /// <summary>Пустой набор выполнен всегда — и в режиме «All Conditions», и в режиме «At Least One».</summary>
        public static bool AreMet(IReadOnlyList<ScenarioCondition> conditions, ScenarioConditionsMode mode, ScenarioRun run)
        {
            var hasConditions = false;

            foreach (var condition in conditions)
            {
                if (condition == null)
                    continue;

                hasConditions = true;
                var isMet = condition.IsMet(run);

                if (mode == ScenarioConditionsMode.All && !isMet)
                    return false;

                if (mode == ScenarioConditionsMode.Any && isMet)
                    return true;
            }

            return mode == ScenarioConditionsMode.All || !hasConditions;
        }
    }
}
