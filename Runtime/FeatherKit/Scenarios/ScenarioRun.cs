using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Один запуск сценария — то, что получает каждый его блок.
    /// - чей это сценарий и на каком объекте он стоит
    /// - кто его вызвал: вошедший в триггер, другой сценарий; может не быть никого
    /// - общее правило блоков: пустое поле компонента — компонент на объекте сценария
    /// </summary>
    public class ScenarioRun
    {
        public ScenarioRun(Scenario scenario, GameObject instigator)
        {
            Scenario = scenario;
            Instigator = instigator;
        }


        public Scenario Scenario { get; }

        /// <summary>Кто вызвал сценарий. Null — никто: старт сцены, кнопка, Timeline.</summary>
        public GameObject Instigator { get; }

        public GameObject Owner => Scenario.gameObject;

        /// <summary>Сценарий остановили: ветки «одновременно» и повторы заканчивают, не начиная новых шагов.</summary>
        public bool IsStopped { get; internal set; }


        public T ComponentOrOwners<T>(T fieldValue) where T : Component
        {
            return fieldValue != null ? fieldValue : Owner.GetComponent<T>();
        }
    }
}
