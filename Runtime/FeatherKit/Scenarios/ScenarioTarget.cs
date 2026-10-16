using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// На какой объект действует блок: этот объект, кто вызвал сценарий, игрок или конкретный объект.
    /// Игрока FeatherKit сам не знает — как его найти, задаёт игра через PlayerFinder.
    /// </summary>
    [Serializable]
    public class ScenarioTarget
    {
        [SerializeField] private ScenarioTargetKind kind;
        [SerializeField] private GameObject specificObject;


        /// <summary>Игрок той сцены, где стоит сценарий. Задаёт игра; не задан — цель «Игрок» пуста.</summary>
        public static Func<Component, GameObject> PlayerFinder { get; set; }

        public string Summary => kind switch
        {
            ScenarioTargetKind.Self => "этот объект",
            ScenarioTargetKind.Instigator => "кто вызвал",
            ScenarioTargetKind.Player => "игрок",
            _ => specificObject != null ? specificObject.name : "объект не выбран"
        };


        /// <summary>Объект цели или null: вызвал никто, игрока нет, объект не выбран.</summary>
        public GameObject Resolve(ScenarioRun run)
        {
            return kind switch
            {
                ScenarioTargetKind.Self => run.Owner,
                ScenarioTargetKind.Instigator => run.Instigator,
                ScenarioTargetKind.Player => PlayerFinder?.Invoke(run.Scenario),
                _ => specificObject
            };
        }

        public GameObject ResolveRequired(ScenarioRun run)
        {
            var target = Resolve(run);

            if (target == null)
                throw new InvalidOperationException($"Цель «{Summary}» не найдена");

            return target;
        }
    }


    public enum ScenarioTargetKind
    {
        [InspectorName("Этот объект")] Self,
        [InspectorName("Кто вызвал")] Instigator,
        [InspectorName("Игрок")] Player,
        [InspectorName("Объект")] Specific
    }
}
