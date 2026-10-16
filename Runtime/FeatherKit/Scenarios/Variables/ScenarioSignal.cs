using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Сигнал сценариев как ассет: «свет на этаже погас», «монстр ушёл».
    /// Один сценарий посылает — все, кто ждёт этот сигнал, срабатывают. Рассылает ScenarioSignals.
    /// </summary>
    [CreateAssetMenu(menuName = "FeatherKit/Scenarios/Signal", fileName = "Signal_")]
    public class ScenarioSignal : ScriptableObject
    {
        [Tooltip("Что значит сигнал и кто его посылает — для себя, в игре не используется")]
        [TextArea]
        [SerializeField] private string note;
    }
}
