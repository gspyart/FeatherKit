using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Счётчик сценариев как ассет: «дёрнуто рычагов», «сколько раз игрок умер в коридоре».
    /// Сравнивается по ссылке, как флаг; значение хранит ScenarioCounters.
    /// </summary>
    [CreateAssetMenu(menuName = "FeatherKit/Scenarios/Counter", fileName = "Counter_")]
    public class ScenarioCounter : ScriptableObject
    {
        [Tooltip("The counter's value at the start of the game")]
        [SerializeField] private int initialValue;

        [Tooltip("Game — the value lives for the whole game. Level — it goes back to the initial value when a level loads")]
        [SerializeField] private ScenarioVariableLifetime lifetime;

        [Tooltip("Kept in checkpoints and saves")]
        [SerializeField] private bool isSaved;

        [Tooltip("Permanent key for saves; does not change when the asset is renamed")]
        [SerializeField] private string id;


        public int InitialValue => initialValue;

        public ScenarioVariableLifetime Lifetime => lifetime;

        public bool IsSaved => isSaved;

        /// <summary>Постоянный ключ для сохранений. Пусто — ещё не выдан: выдача — этап 3, сохранения.</summary>
        public string Id => id;

        public int Value
        {
            get => ScenarioCounters.Get(this);
            set => ScenarioCounters.Set(this, value);
        }
    }
}
