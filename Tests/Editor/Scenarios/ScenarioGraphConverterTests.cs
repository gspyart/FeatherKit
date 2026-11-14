using System.Linq;
using FeatherKit.EditorTools;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>Конвертер старого сценария: списки становятся нодами When и Do с проводом, старые поля пусты, undo всё возвращает.</summary>
    public class ScenarioGraphConverterTests
    {
        private GameObject owner;
        private Scenario scenario;


        [SetUp]
        public void CreateOldScenario()
        {
            // Скрытый и несохраняемый: тест не должен помечать изменённой открытую сцену.
            owner = EditorUtility.CreateGameObjectWithHideFlags("Old Scenario", HideFlags.HideAndDontSave);
            scenario = owner.AddComponent<Scenario>();

            var serializedScenario = new SerializedObject(scenario);
            AddBlock(serializedScenario.FindProperty("events"), new ObjectStartedEvent());
            AddBlock(serializedScenario.FindProperty("conditions"), new ChanceCondition());
            AddBlock(serializedScenario.FindProperty("actions"), new WaitAction());
            AddBlock(serializedScenario.FindProperty("actions"), new RepeatAction());
            serializedScenario.FindProperty("conditionsMode").enumValueIndex = (int)ScenarioConditionsMode.Any;
            serializedScenario.FindProperty("runsOnce").boolValue = true;
            serializedScenario.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void DestroyOldScenario()
        {
            Undo.ClearUndo(scenario);
            Object.DestroyImmediate(owner);
        }


        [Test]
        public void Convert_MakesWhenAndDoWithWire_AndEmptiesOldLists()
        {
            Assert.That(ScenarioGraphConverter.CanConvert(scenario), Is.True);

            var conversion = ScenarioGraphConverter.Convert(scenario);
            var graph = scenario.Graph;
            var when = (ScenarioWhenNode)graph.FindNode(conversion.WhenNodeId);
            var steps = (ScenarioStepsNode)graph.FindNode(conversion.StepsNodeId);

            Assert.That(when.Events.Select(scenarioEvent => scenarioEvent.GetType()),
                Is.EqualTo(new[] { typeof(ObjectStartedEvent), typeof(CalledEvent) }), "Called keeps old calls from buttons and Timeline working");
            Assert.That(when.Conditions.Count, Is.EqualTo(1));
            Assert.That(when.Conditions.Mode, Is.EqualTo(ScenarioConditionsMode.Any));
            Assert.That(when.RunsOnlyOnce, Is.True);
            Assert.That(steps.Steps.Count, Is.EqualTo(2));
            Assert.That(graph.Wires.Single().Connects(when.Id, ScenarioWhenNode.Then.Id, steps.Id, ScenarioStepsNode.In.Id), Is.True);
            Assert.That(conversion.OldFlowBlocks, Is.EqualTo(new[] { "Repeat" }));

            var serializedScenario = new SerializedObject(scenario);
            Assert.That(serializedScenario.FindProperty("events").arraySize, Is.Zero);
            Assert.That(serializedScenario.FindProperty("conditions").arraySize, Is.Zero);
            Assert.That(serializedScenario.FindProperty("actions").arraySize, Is.Zero);
            Assert.That(serializedScenario.FindProperty("runsOnce").boolValue, Is.False);
            Assert.That(ScenarioGraphConverter.CanConvert(scenario), Is.False);
        }

        [Test]
        public void Convert_OneUndo_BringsOldListsBack()
        {
            Undo.IncrementCurrentGroup();
            ScenarioGraphConverter.Convert(scenario);
            Undo.PerformUndo();

            var serializedScenario = new SerializedObject(scenario);
            Assert.That(scenario.Graph.Nodes, Is.Empty);
            Assert.That(serializedScenario.FindProperty("events").arraySize, Is.EqualTo(1));
            Assert.That(serializedScenario.FindProperty("actions").arraySize, Is.EqualTo(2));
            Assert.That(serializedScenario.FindProperty("runsOnce").boolValue, Is.True);
        }


        private static void AddBlock(SerializedProperty blocks, object block)
        {
            blocks.arraySize++;
            blocks.GetArrayElementAtIndex(blocks.arraySize - 1).managedReferenceValue = block;
        }
    }
}
