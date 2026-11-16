using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// На какой объект действует блок: этот объект, кто вызвал сценарий, игрок, конкретный объект или переменная графа.
    /// Игрока FeatherKit сам не знает — как его найти, задаёт игра через PlayerFinder.
    /// </summary>
    [Serializable]
    public class ScenarioTarget
    {
        [SerializeField] private ScenarioTargetKind kind;
        [SerializeField] private GameObject specificObject;

        [Tooltip("The graph's Object variable for the “Variable” kind")]
        [SerializeField] private string variableId;


        /// <summary>Игрок той сцены, где стоит сценарий. Задаёт игра; не задан — цель «Player» пуста.</summary>
        public static Func<Component, GameObject> PlayerFinder { get; set; }

        public string VariableId => variableId;

        public string Summary => kind switch
        {
            ScenarioTargetKind.Self => "this object",
            ScenarioTargetKind.Instigator => "caller",
            ScenarioTargetKind.Player => "player",
            ScenarioTargetKind.Variable => "graph variable",
            _ => specificObject != null ? specificObject.name : "no object selected"
        };


        /// <summary>Объект цели или null: вызвал никто, игрока нет, объект не выбран.</summary>
        public GameObject Resolve(ScenarioRun run)
        {
            return kind switch
            {
                ScenarioTargetKind.Self => run.Owner,
                ScenarioTargetKind.Instigator => run.Instigator,
                ScenarioTargetKind.Player => PlayerFinder?.Invoke(run.Scenario),
                ScenarioTargetKind.Variable => run.Scenario.GraphVariables.Get<GameObject>(variableId),
                _ => specificObject
            };
        }


        /// <summary>Цель по умолчанию — «этот объект» без выбранного объекта и переменной: переносить нечего.</summary>
        internal bool IsDefault => kind == ScenarioTargetKind.Self && !ScenarioFieldMigration.IsAssigned(specificObject) && string.IsNullOrEmpty(variableId);

        /// <summary>Та же цель точкой — для переноса старых полей блоков на ScenarioPoint.</summary>
        internal ScenarioPoint ToPoint()
        {
            return kind switch
            {
                ScenarioTargetKind.Self => new ScenarioPoint(ScenarioPointKind.ThisObject),
                ScenarioTargetKind.Instigator => new ScenarioPoint(ScenarioPointKind.Caller),
                ScenarioTargetKind.Player => new ScenarioPoint(ScenarioPointKind.Player),
                ScenarioTargetKind.Variable => ScenarioPoint.FromVariable(variableId),
                _ => new ScenarioPoint(ScenarioPointKind.Object)
                {
                    TargetTransform = ScenarioFieldMigration.IsAssigned(specificObject) ? specificObject.transform : null
                }
            };
        }


        public static ScenarioTarget FromVariable(string graphVariableId)
        {
            return new ScenarioTarget { kind = ScenarioTargetKind.Variable, variableId = graphVariableId };
        }

        public GameObject ResolveRequired(ScenarioRun run)
        {
            var target = Resolve(run);

            if (target == null)
                throw new InvalidOperationException($"Target “{Summary}” not found");

            return target;
        }
    }


    /// <summary>На кого действует блок. Новые виды — только в конец: в данных хранится число.</summary>
    public enum ScenarioTargetKind
    {
        [InspectorName("This Object")] Self,
        [InspectorName("Caller")] Instigator,
        [InspectorName("Player")] Player,
        [InspectorName("Object")] Specific,
        [InspectorName("Variable")] Variable
    }
}
