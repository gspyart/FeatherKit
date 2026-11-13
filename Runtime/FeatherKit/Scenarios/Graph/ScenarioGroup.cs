using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Рамка на холсте вокруг нескольких нод с заголовком — чтобы читать граф кусками.
    /// Состав рамки — id нод, а не место на холсте; на этапе 4 рамка станет состоянием.
    /// </summary>
    [Serializable]
    public class ScenarioGroup
    {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField] private Color color = new Color(0.3f, 0.3f, 0.3f, 0.3f);
        [SerializeField] private List<int> nodeIds = new List<int>();


        public ScenarioGroup(string title)
        {
            id = Guid.NewGuid().ToString("N");
            this.title = title;
        }


        public string Id => id;

        public string Title
        {
            get => title;
            set => title = value;
        }

        public Color Color
        {
            get => color;
            set => color = value;
        }

        public IReadOnlyList<int> NodeIds => nodeIds;


        public void AddNode(ScenarioNode node)
        {
            if (!nodeIds.Contains(node.Id))
                nodeIds.Add(node.Id);
        }

        public bool RemoveNode(int nodeId)
        {
            return nodeIds.Remove(nodeId);
        }
    }
}
