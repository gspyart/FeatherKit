using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Граф сценария файлом — один на много объектов и сцен.
    /// Объекты сцены граф-файл не хранит: они приходят через открытые переменные, значения — в привязках у Scenario.
    /// </summary>
    [CreateAssetMenu(menuName = "FeatherKit/Scenarios/Scenario Graph", fileName = "ScenarioGraph_")]
    public class ScenarioGraphAsset : ScriptableObject
    {
        [SerializeField] private ScenarioGraph graph = new ScenarioGraph();


        /// <summary>Граф файла. Присвоение — для конвертера и сборки кодом; null даёт пустой граф.</summary>
        public ScenarioGraph Graph
        {
            get => graph;
            set => graph = value ?? new ScenarioGraph();
        }
    }
}
