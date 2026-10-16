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
        [Tooltip("С каким значением флаг начинает игру")]
        [SerializeField] private bool initialValue;


        public bool InitialValue => initialValue;

        public bool Value
        {
            get => ScenarioFlags.Get(this);
            set => ScenarioFlags.Set(this, value);
        }
    }
}
