using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Провод потока: выход одной ноды → вход другой.
    /// Хранит id нод и портов, а не ссылки: так граф переживает копирование и загрузку.
    /// </summary>
    [Serializable]
    public class ScenarioWire
    {
        [SerializeField] private int fromNodeId;
        [SerializeField] private string fromPortId;
        [SerializeField] private int toNodeId;
        [SerializeField] private string toPortId;


        public ScenarioWire(int fromNodeId, string fromPortId, int toNodeId, string toPortId)
        {
            this.fromNodeId = fromNodeId;
            this.fromPortId = fromPortId;
            this.toNodeId = toNodeId;
            this.toPortId = toPortId;
        }


        public int FromNodeId => fromNodeId;

        /// <summary>Id выхода — ScenarioOutput.Id.</summary>
        public string FromPortId => fromPortId;

        public int ToNodeId => toNodeId;

        /// <summary>Id входа — ScenarioInput.Id.</summary>
        public string ToPortId => toPortId;


        public bool Connects(int fromNode, string fromPort, int toNode, string toPort)
        {
            return fromNodeId == fromNode && toNodeId == toNode
                   && string.Equals(fromPortId, fromPort, StringComparison.Ordinal)
                   && string.Equals(toPortId, toPort, StringComparison.Ordinal);
        }
    }
}
