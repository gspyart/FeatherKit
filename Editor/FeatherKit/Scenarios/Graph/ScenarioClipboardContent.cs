using System.Collections.Generic;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Что лежит в буфере: кусок графа целиком, со своими копиями нод и переменных.
    /// ScriptableObject — чтобы класть объекты сцены на их места через SerializedObject (ScenarioSceneReferences).
    /// Живёт мгновение — создал, перевёл в текст или из текста, уничтожил.
    /// </summary>
    internal class ScenarioClipboardContent : ScriptableObject
    {
        [SerializeReference] private List<ScenarioNode> nodes = new List<ScenarioNode>();
        [SerializeField] private List<ScenarioWire> wires = new List<ScenarioWire>();
        [SerializeField] private List<ScenarioNote> notes = new List<ScenarioNote>();
        [SerializeField] private List<ScenarioGroup> groups = new List<ScenarioGroup>();
        [SerializeField] private List<ScenarioVariableDefinition> variables = new List<ScenarioVariableDefinition>();
        [SerializeField] private List<ScenarioSceneReference> sceneReferences = new List<ScenarioSceneReference>();


        public List<ScenarioNode> Nodes => nodes;

        public List<ScenarioWire> Wires => wires;

        public List<ScenarioNote> Notes => notes;

        public List<ScenarioGroup> Groups => groups;

        public List<ScenarioVariableDefinition> Variables => variables;

        /// <summary>Объекты сцены в полях нод и переменных — их текст не хранит, они лежат здесь отдельно.</summary>
        public List<ScenarioSceneReference> SceneReferences => sceneReferences;

        /// <summary>Левый верхний угол куска: от него считается место вставки под курсором.</summary>
        public Vector2 TopLeft
        {
            get
            {
                var corners = nodes.Where(node => node != null).Select(node => node.Position)
                    .Concat(notes.Where(note => note != null).Select(note => note.Area.position)).ToList();

                return corners.Count == 0
                    ? Vector2.zero
                    : new Vector2(corners.Min(corner => corner.x), corners.Min(corner => corner.y));
            }
        }


        public static ScenarioClipboardContent Create()
        {
            var content = CreateInstance<ScenarioClipboardContent>();
            content.hideFlags = HideFlags.HideAndDontSave;

            return content;
        }

        public static ScenarioClipboardContent FromJson(string json)
        {
            var content = Create();
            EditorJsonUtility.FromJsonOverwrite(json, content);

            return content;
        }

        public string ToJson()
        {
            return EditorJsonUtility.ToJson(this);
        }
    }
}
