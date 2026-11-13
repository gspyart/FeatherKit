using System.Linq;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Пути SerializedProperty, на которые опираются редактор и конвертер.
    /// Переименовали поле — падает этот тест, а не окно графа и не старые сцены.
    /// </summary>
    public class ScenarioSerializedPathsTests
    {
        // ── Компонент Scenario ───────────────────────────────────────────

        [TestCase("events")]
        [TestCase("conditionsMode")]
        [TestCase("conditions")]
        [TestCase("actions")]
        [TestCase("runsOnce")]
        [TestCase("graphSource")]
        [TestCase("graph")]
        [TestCase("graphAsset")]
        [TestCase("variableBindings")]
        [TestCase("persistentId")]
        [TestCase("graph.nodes")]
        [TestCase("graph.wires")]
        [TestCase("graph.variables")]
        [TestCase("graph.groups")]
        [TestCase("graph.notes")]
        [TestCase("graph.nextNodeId")]
        public void Scenario_HasProperty(string path)
        {
            var owner = EditorUtility.CreateGameObjectWithHideFlags("Scenario Paths", HideFlags.HideAndDontSave);

            try
            {
                var serializedScenario = new SerializedObject(owner.AddComponent<Scenario>());

                Assert.That(serializedScenario.FindProperty(path), Is.Not.Null, path);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }


        // ── Граф-файл и ноды ─────────────────────────────────────────────

        [Test]
        public void GraphAsset_NodesWiresVariables_HaveProperties()
        {
            var asset = ScriptableObject.CreateInstance<ScenarioGraphAsset>();
            asset.Graph = ScenarioTestGraphs.SilhouetteWaitFor();

            foreach (var variable in ScenarioTestGraphs.WithVariables().Variables)
                asset.Graph.AddVariable(variable);

            var guard = asset.Graph.AddNode(new ScenarioGuardNode());

            try
            {
                var serializedAsset = new SerializedObject(asset);
                var graph = serializedAsset.FindProperty("graph");
                Assert.That(graph, Is.Not.Null, "graph");

                RequireRelative(NodeProperty(asset, graph, ScenarioTestGraphs.WhenTitle),
                    "id", "position", "customTitle", "events", "conditions", "conditions.mode", "conditions.conditions",
                    "rerunRule", "runsOnlyOnce");

                RequireRelative(NodeProperty(asset, graph, ScenarioTestGraphs.SilhouetteOnTitle), "steps", "steps.actions");

                var waitFor = NodeProperty(asset, graph, ScenarioTestGraphs.WaitLookTitle);
                RequireRelative(waitFor, "cases", "timeout", "checkInterval");
                RequireRelative(waitFor.FindPropertyRelative("cases").GetArrayElementAtIndex(0),
                    "id", "label", "awaitedEvent", "condition");

                var guardIndex = asset.Graph.Nodes.ToList().IndexOf(guard);
                RequireRelative(graph.FindPropertyRelative("nodes").GetArrayElementAtIndex(guardIndex), "conditions", "checkInterval");

                RequireRelative(graph.FindPropertyRelative("wires").GetArrayElementAtIndex(0),
                    "fromNodeId", "fromPortId", "toNodeId", "toPortId");

                var variables = graph.FindPropertyRelative("variables");
                RequireRelative(variables.GetArrayElementAtIndex(0),
                    "id", "variableName", "kind", "objectTypeName", "isExposed", "isSaved", "initialValue",
                    "initialValue.flagValue", "initialValue.counterValue", "initialValue.numberValue", "initialValue.objectValue");

                var lampIndex = asset.Graph.Variables.ToList().FindIndex(variable => variable.Name == "Lamp");
                Assert.That(variables.GetArrayElementAtIndex(lampIndex).FindPropertyRelative("objectTypeName").stringValue,
                    Is.EqualTo("UnityEngine.Light, UnityEngine.CoreModule"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private static SerializedProperty NodeProperty(ScenarioGraphAsset asset, SerializedProperty graph, string title)
        {
            var index = asset.Graph.Nodes.ToList().FindIndex(node => node.Title == title);

            return graph.FindPropertyRelative("nodes").GetArrayElementAtIndex(index);
        }

        private static void RequireRelative(SerializedProperty parent, params string[] relativePaths)
        {
            foreach (var relativePath in relativePaths)
                Assert.That(parent.FindPropertyRelative(relativePath), Is.Not.Null, $"{parent.propertyPath} → {relativePath}");
        }
    }
}
