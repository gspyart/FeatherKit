using System.Collections;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>Ноды потока этапа 2: Random, Toggle, Do N, Cooldown, Switch on Counter; их общее состояние и сброс на Stop.</summary>
    public class ScenarioFlowStage2Tests : ScenarioPlayModeTestBase
    {
        [Test]
        public void Random_ZeroWeight_NeverChosen()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var random = graph.AddNode(new ScenarioRandomNode());
            random.Weights[0] = 0f;
            ConnectToIn(graph, when, ScenarioWhenNode.Then, random);
            ConnectToIn(graph, random, ScenarioRandomNode.OutOutput(1), AddSteps(graph, "One"));
            ConnectToIn(graph, random, ScenarioRandomNode.OutOutput(2), AddSteps(graph, "Two"));
            var scenario = CreateScenario(graph);

            for (var attempt = 0; attempt < 20; attempt++)
                RaiseWhen(scenario);

            Assert.That(Marks.FindAll(mark => mark == "Two").Count, Is.EqualTo(20));
        }

        [Test]
        public void Toggle_Alternates_StopKeepsMemory_ResetNodeMemoryStartsOver()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var toggle = graph.AddNode(new ScenarioToggleNode());
            ConnectToIn(graph, when, ScenarioWhenNode.Then, toggle);
            ConnectToIn(graph, toggle, ScenarioToggleNode.A, AddSteps(graph, "A"));
            ConnectToIn(graph, toggle, ScenarioToggleNode.B, AddSteps(graph, "B"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            RaiseWhen(scenario);
            RaiseWhen(scenario);
            scenario.Stop();
            RaiseWhen(scenario);
            scenario.ResetNodeMemory();
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "A", "B", "A", "B", "A" }));
        }

        [Test]
        public void DoN_LetsThroughN_ThenExhausted_ResetStartsOver()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var resetWhen = AddWhen(graph, "Reset When");
            var doN = graph.AddNode(new ScenarioDoNNode { TimesAllowed = 2 });
            ConnectToIn(graph, when, ScenarioWhenNode.Then, doN);
            graph.Connect(resetWhen, ScenarioWhenNode.Then, doN, ScenarioDoNNode.Reset);
            ConnectToIn(graph, doN, ScenarioDoNNode.Out, AddSteps(graph, "Out"));
            ConnectToIn(graph, doN, ScenarioDoNNode.Exhausted, AddSteps(graph, "Exhausted"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            RaiseWhen(scenario);
            RaiseWhen(scenario);
            RaiseWhen(scenario, "Reset When");
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Out", "Out", "Exhausted", "Out" }));
        }

        [UnityTest]
        public IEnumerator Cooldown_BlocksUntilSecondsPass()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var cooldown = graph.AddNode(new ScenarioCooldownNode { Seconds = 0.2f });
            ConnectToIn(graph, when, ScenarioWhenNode.Then, cooldown);
            ConnectToIn(graph, cooldown, ScenarioCooldownNode.Out, AddSteps(graph, "Out"));
            ConnectToIn(graph, cooldown, ScenarioCooldownNode.Blocked, AddSteps(graph, "Blocked"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            RaiseWhen(scenario);
            yield return new WaitForSeconds(0.3f);
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Out", "Blocked", "Out" }));
        }

        [Test]
        public void Random_RollAtTotal_PicksLastPositiveWeight()
        {
            Assert.That(ScenarioRandomNode.PickOutputNumber(new[] { 1f, 0f }, 1f), Is.EqualTo(1), "{1, 0} with roll = total");
            Assert.That(ScenarioRandomNode.PickOutputNumber(new[] { 0f, 2f, 0f }, 0f), Is.EqualTo(2));
            Assert.That(ScenarioRandomNode.PickOutputNumber(new[] { 1f, 1f }, 1f), Is.EqualTo(2));
            Assert.That(ScenarioRandomNode.PickOutputNumber(new[] { 0f, 0f }, 0f), Is.Zero);
        }

        [Test]
        public void StopScenarioNode_ResetNodeMemory_ForgetsOnce()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var once = graph.AddNode(new ScenarioOnceNode());
            ConnectToIn(graph, when, ScenarioWhenNode.Then, once);
            ConnectToIn(graph, once, ScenarioOnceNode.Out, AddSteps(graph, "Scream"));
            var resetWhen = AddWhen(graph, "Reset When");
            ConnectToIn(graph, resetWhen, ScenarioWhenNode.Then, graph.AddNode(new ScenarioStopScenarioNode { ResetsNodeMemory = true }));
            var plainStopWhen = AddWhen(graph, "Stop When");
            ConnectToIn(graph, plainStopWhen, ScenarioWhenNode.Then, graph.AddNode(new ScenarioStopScenarioNode()));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            RaiseWhen(scenario, "Stop When");
            RaiseWhen(scenario);
            RaiseWhen(scenario, "Reset When");
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Scream", "Scream" }), "Plain Stop keeps Once passed; Reset Node Memory forgets it");
        }

        [Test]
        public void SwitchOnCounter_GoesToValueOrDefault()
        {
            var graph = new ScenarioGraph();
            var levers = graph.AddVariable(new ScenarioVariableDefinition("Levers", ScenarioVariableKind.Counter));
            var when = AddWhen(graph);
            when.RerunRule = ScenarioRerunRule.RunAlongside;
            var switchNode = graph.AddNode(new ScenarioSwitchOnCounterNode());
            SetField(switchNode, "counter", ScenarioCounterReference.FromVariable(levers.Id));
            var one = switchNode.AddCase(1);
            var two = switchNode.AddCase(2);
            var pull = AddSteps(graph, "Pull", new ScenarioTestCountAction("levers", ScenarioCounterReference.FromVariable(levers.Id)));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, pull);
            ConnectToIn(graph, pull, ScenarioStepsNode.Done, switchNode);
            ConnectToIn(graph, switchNode, one.Output, AddSteps(graph, "One"));
            ConnectToIn(graph, switchNode, two.Output, AddSteps(graph, "Two"));
            ConnectToIn(graph, switchNode, ScenarioSwitchOnCounterNode.Default, AddSteps(graph, "Default"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            RaiseWhen(scenario);
            RaiseWhen(scenario);

            Assert.That(Marks.FindAll(mark => mark is "One" or "Two" or "Default"), Is.EqualTo(new[] { "One", "Two", "Default" }));
        }
    }
}
