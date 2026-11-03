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
        [Tooltip("What the signal means and who sends it — a note for yourself, not used in the game")]
        [TextArea]
        [SerializeField] private string note;
    }
}
