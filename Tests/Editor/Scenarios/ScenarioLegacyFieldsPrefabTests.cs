using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Старые поля блоков в настоящем файле префаба: YAML со старым «source» читается без исключений,
    /// а блок работает из новой точки после переноса.
    /// </summary>
    public class ScenarioLegacyFieldsPrefabTests
    {
        private const string TempFolder = "Assets/ScenarioLegacyFieldsTemp";
        private const string PrefabPath = TempFolder + "/Old Play Sound.prefab";


        [TearDown]
        public void DeleteTempFolder()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.DeleteAsset(TempFolder);
        }


        [Test]
        public void OldPlaySoundWithObjectSource_LoadsFromYaml_AndPlaysFromThatObject()
        {
            SaveOldPrefab();

            var yaml = File.ReadAllText(PrefabPath);
            Assert.That(yaml, Does.Contain("source:"), "The prefab file holds the old field");
            Assert.That(yaml, Does.Match(@"specificObject: \{fileID: [1-9]"), "…with the speaker object in it");

            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            LogAssert.NoUnexpectedReceived();

            var playSound = (PlaySoundAction)StepsOf(prefab.GetComponent<Scenario>())[0];
            var speaker = prefab.transform.Find("Speaker");

            Assert.That(playSound.SoundPoint.Kind, Is.EqualTo(ScenarioPointKind.Object));
            Assert.That(playSound.SoundPoint.TargetTransform, Is.SameAs(speaker));
            Assert.That(playSound.TryUpgradeLegacyFields(), Is.False, "Upgrading twice changes nothing");
        }

        // Префаб с Play Sound, у которого заполнены только старые скрытые поля — так он лежал до этапа 2.
        private static void SaveOldPrefab()
        {
            AssetDatabase.CreateFolder("Assets", "ScenarioLegacyFieldsTemp");

            var root = new GameObject("Old Play Sound");
            var speaker = new GameObject("Speaker");
            speaker.transform.SetParent(root.transform, false);

            try
            {
                var steps = new ScenarioStepsNode { CustomTitle = "Steps" };
                steps.AddStep(new PlaySoundAction());
                var graph = new ScenarioGraph();
                graph.AddNode(steps);

                var scenario = root.AddComponent<Scenario>();
                typeof(Scenario).GetField("graph", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scenario, graph);

                var serializedScenario = new SerializedObject(scenario);
                var action = serializedScenario.FindProperty("graph.nodes.Array.data[0].steps.actions.Array.data[0]");
                action.FindPropertyRelative("source.kind").enumValueIndex = (int)ScenarioTargetKind.Specific;
                action.FindPropertyRelative("source.specificObject").objectReferenceValue = speaker;
                serializedScenario.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static List<ScenarioAction> StepsOf(Scenario scenario)
        {
            var steps = (ScenarioStepsNode)scenario.Graph.Nodes.First(node => node is ScenarioStepsNode);

            return (List<ScenarioAction>)typeof(ScenarioActionList)
                .GetField("actions", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(steps.Steps);
        }
    }
}
