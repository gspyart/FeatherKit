using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Где лежит граф сценария: прямо в компоненте или в граф-файле.</summary>
    public enum ScenarioGraphSource
    {
        [InspectorName("In This Object")] Embedded,
        [InspectorName("Graph File")] Asset
    }
}
