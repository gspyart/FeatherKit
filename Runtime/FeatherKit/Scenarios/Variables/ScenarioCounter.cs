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
        [Tooltip("С каким значением счётчик начинает игру")]
        [SerializeField] private int initialValue;


        public int InitialValue => initialValue;

        public int Value
        {
            get => ScenarioCounters.Get(this);
            set => ScenarioCounters.Set(this, value);
        }
    }
}
