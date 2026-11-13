using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Заметка на холсте графа: текст для себя, в игре не участвует.</summary>
    [Serializable]
    public class ScenarioNote
    {
        [SerializeField] private string id;
        [TextArea]
        [SerializeField] private string text;
        [SerializeField] private Rect area = new Rect(0f, 0f, 200f, 100f);


        public ScenarioNote(string text, Rect area)
        {
            id = Guid.NewGuid().ToString("N");
            this.text = text;
            this.area = area;
        }


        public string Id => id;

        public string Text
        {
            get => text;
            set => text = value;
        }

        /// <summary>Место и размер заметки на холсте.</summary>
        public Rect Area
        {
            get => area;
            set => area = value;
        }
    }
}
