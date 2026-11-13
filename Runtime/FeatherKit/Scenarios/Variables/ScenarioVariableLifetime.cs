using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Сколько живёт значение общего флага или счётчика: всю игру или до загрузки следующего уровня.</summary>
    public enum ScenarioVariableLifetime
    {
        [InspectorName("Game")] Game,
        [InspectorName("Level")] Level
    }
}
