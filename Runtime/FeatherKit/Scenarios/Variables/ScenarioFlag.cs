using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Флаг сценариев как ассет: «дверь открыта», «испуг в коридоре был».
    /// Сравнивается по ссылке — опечатки в имени не бывает. Значение хранит ScenarioFlags.
    /// </summary>
    [CreateAssetMenu(menuName = "FeatherKit/Scenarios/Flag", fileName = "Flag_")]
    public class ScenarioFlag : ScriptableObject
    {
        [Tooltip("The flag's value at the start of the game")]
        [SerializeField] private bool initialValue;

        [Tooltip("Game — the value lives for the whole game. Level — it goes back to the initial value when a level loads")]
        [SerializeField] private ScenarioVariableLifetime lifetime;

        [Tooltip("Kept in checkpoints and saves")]
        [SerializeField] private bool isSaved;

        [Tooltip("Permanent key for saves; does not change when the asset is renamed")]
        [SerializeField] private string id;


        public bool InitialValue => initialValue;

        public ScenarioVariableLifetime Lifetime => lifetime;

        public bool IsSaved => isSaved;

        /// <summary>Постоянный ключ для сохранений. Пусто — ещё не выдан: выдача — этап 3, сохранения.</summary>
        public string Id => id;

        public bool Value
        {
            get => ScenarioFlags.Get(this);
            set => ScenarioFlags.Set(this, value);
        }
    }
}
