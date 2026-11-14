using System.Collections;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Рабочие части контракта этапа 2 в игре: списки и слоты переменных, точки, For Each, Gate и Once, Distance,
    /// срабатывание своим «случаем».
    /// </summary>
    public class ScenarioStage2ContractTests : ScenarioPlayModeTestBase
    {
        [TearDown]
        public void ForgetPlayer()
        {
            ScenarioTarget.PlayerFinder = null;
            ScenarioPoint.PlayerEyesFinder = null;
        }


        // ── Списки и For Each ────────────────────────────────────────────

        [Test]
        public void ForEach_OverBoundObjectList_SetsCurrentItemEachTime()
        {
            var graph = ScenarioTestGraphs.ForEachOverList();
            var currentLamp = graph.FindVariableByName(ScenarioTestGraphs.CurrentLampVariableName);
            ScenarioTestGraphs.FindNode<ScenarioStepsNode>(graph, ScenarioTestGraphs.EachTitle)
                .AddStep(new ScenarioTestRecordLightAction(ScenarioValue<Light>.FromVariable(currentLamp.Id)));
            var lamps = graph.FindVariableByName(ScenarioTestGraphs.LampsVariableName);

            var binding = new ScenarioVariableBinding(lamps.Id);
            binding.Value.ObjectListValue.Add(CreateLamp("Lamp A").gameObject);
            binding.Value.ObjectListValue.Add(CreateLamp("Lamp B"));
            binding.Value.ObjectListValue.Add(CreateLamp("Lamp C"));

            var scenario = CreateScenario(CreateGraphAsset(graph), "Corridor", binding);
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[]
            {
                ScenarioTestGraphs.EachTitle, "Lamp A", ScenarioTestGraphs.EachTitle, "Lamp B",
                ScenarioTestGraphs.EachTitle, "Lamp C", ScenarioTestGraphs.AllDoneTitle
            }));
        }

        private Light CreateLamp(string lampName)
        {
            var lamp = CreateGameObject(lampName).AddComponent<Light>();

            return lamp;
        }


        // ── Gate и Once ──────────────────────────────────────────────────

        [Test]
        public void Gate_ClosedAtStart_OpensOnInput_OnceLetsThroughOnlyFirst()
        {
            var scenario = CreateScenario(ScenarioTestGraphs.GateAndOnce());

            RaiseWhen(scenario);

            Assert.That(Marks, Is.Empty, "The gate starts closed");

            RaiseWhen(scenario, ScenarioTestGraphs.OpenWhenTitle);
            RaiseWhen(scenario);
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { ScenarioTestGraphs.PassedTitle }));

            scenario.Stop();
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { ScenarioTestGraphs.PassedTitle }), "Stop puts the gate back to closed");
        }


        // ── Точки и дистанция ────────────────────────────────────────────

        [Test]
        public void Distance_UsesPlayerPoint_HorizontalOnly()
        {
            var player = CreateGameObject("Player");
            ScenarioTarget.PlayerFinder = _ => player;
            var scenario = CreateScenario(ScenarioTestGraphs.Distance());

            player.transform.position = new Vector3(1f, 10f, 1f);
            RaiseWhen(scenario);
            player.transform.position = new Vector3(5f, 0f, 0f);
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { ScenarioTestGraphs.NearTitle, ScenarioTestGraphs.FarTitle }));
        }

        [Test]
        public void Point_ToVectorSlot_AndBackThroughValue()
        {
            var graph = new ScenarioGraph();
            var spot = graph.AddVariable(new ScenarioVariableDefinition("Spot", ScenarioVariableKind.Vector3));
            var marker = CreateGameObject("Marker");
            marker.transform.SetPositionAndRotation(new Vector3(2f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));

            var objectPoint = new ScenarioPoint(ScenarioPointKind.Object) { TargetTransform = marker.transform, Offset = Vector3.forward };
            var worldPoint = new ScenarioPoint(ScenarioPointKind.WorldPoint) { WorldPosition = new Vector3(0f, 1f, 0f) };
            var spotValue = ScenarioValue<Vector3>.FromVariable(spot.Id);
            var spotSlot = ScenarioVariableSlot<Vector3>.FromVariable(spot.Id);

            var when = AddWhen(graph);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Steps",
                new ScenarioTestStorePointAction(objectPoint, spotSlot), new ScenarioTestRecordVectorAction(spotValue),
                new ScenarioTestStorePointAction(worldPoint, spotSlot), new ScenarioTestRecordVectorAction(spotValue),
                new ScenarioTestStorePointAction(ScenarioPoint.FromVariable(spot.Id), spotSlot),
                new ScenarioTestRecordVectorAction(spotValue)));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Steps", "(3.0, 0.0, 0.0)", "(0.0, 1.0, 0.0)", "(0.0, 1.0, 0.0)" }),
                "Offset goes along the object's axes; a Vector3 variable is a point too");
        }


        // ── Свой «случай» ────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator FireAsOwnOccurrence_NewWaitDoesNotEatTheKnockThatStartedIt()
        {
            var graph = new ScenarioGraph();
            var when = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Knock When", RerunRule = ScenarioRerunRule.RunAlongside });
            when.AddEvent(new ScenarioTestKnockEvent("knock"));
            var knock = AddSteps(graph, "Knock");
            var waitFor = graph.AddNode(new ScenarioWaitForNode());
            var knockAgain = waitFor.AddCase(new ScenarioWaitCase(new ScenarioTestKnockEvent("knock again")));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, knock);
            ConnectToIn(graph, knock, ScenarioStepsNode.Done, waitFor);
            ConnectToIn(graph, waitFor, knockAgain.Output, AddSteps(graph, "Burst"));
            var scenario = CreateScenario(graph);

            ScenarioTestKnockEvent.Knock();
            yield return WaitFrames(2);
            ScenarioTestKnockEvent.Knock();
            yield return WaitFrames(3);

            Assert.That(Marks, Is.EqualTo(new[] { "Knock", "Knock", "Burst" }));
            Assert.That(scenario.IsRunning, Is.True, "The second run waits for the third knock");
        }
    }


    /// <summary>Поиск переменной графа по имени — для тестов.</summary>
    public static class ScenarioTestGraphVariables
    {
        public static ScenarioVariableDefinition FindVariableByName(this ScenarioGraph graph, string variableName)
        {
            foreach (var variable in graph.Variables)
            {
                if (variable.Name == variableName)
                    return variable;
            }

            throw new System.ArgumentException($"No variable “{variableName}” in the graph");
        }
    }
}
