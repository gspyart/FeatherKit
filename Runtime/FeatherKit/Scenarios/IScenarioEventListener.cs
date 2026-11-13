using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Кто слушает блоки-события: нода When, нода Wait For, старый список «When» сценария.
    /// Событие узнаёт через него свой сценарий и сообщает ему, что сработало.
    /// </summary>
    public interface IScenarioEventListener
    {
        /// <summary>Сценарий, на объекте которого слушают: его Start, триггеры и столкновения.</summary>
        Scenario Scenario { get; }

        void HandleEventFired(ScenarioEvent firedEvent, GameObject instigator);
    }
}
