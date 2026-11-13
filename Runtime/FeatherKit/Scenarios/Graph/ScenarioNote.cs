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

        [Tooltip("The note's background on the canvas. New notes are a muted blue")]
        [SerializeField] private Color color = new Color(0.26f, 0.31f, 0.38f, 1f);


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

        public Color Color
        {
            get => color;
            set => color = value;
        }
    }
}
