using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>Инспектор граф-файла: что в нём и кнопка «Open Graph» — сам граф правится в окне графа.</summary>
    [CustomEditor(typeof(ScenarioGraphAsset))]
    public class ScenarioGraphAssetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var graph = ((ScenarioGraphAsset)target).Graph;

            EditorGUILayout.HelpBox("A scenario graph for many objects and scenes. Scene objects come in through "
                                    + "“Set on Object” variables: each Scenario using this file gives its own values.", MessageType.None);

            EditorGUILayout.LabelField("Nodes", graph.Nodes.Count.ToString());
            EditorGUILayout.LabelField("Wires", graph.Wires.Count.ToString());
            EditorGUILayout.LabelField("Variables", graph.Variables.Count.ToString());

            if (GUILayout.Button("Open Graph"))
                ScenarioGraphWindow.Open(target);
        }
    }
}
