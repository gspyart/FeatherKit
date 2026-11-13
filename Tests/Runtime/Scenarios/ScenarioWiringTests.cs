using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>Индекс проводов без игры: куда ведёт выход, и что пропускается с предупреждением.</summary>
    public class ScenarioWiringTests
    {
        [Test]
        public void FanOut_OneOutputLeadsToBothNodes_InWireOrder()
        {
            var graph = ScenarioTestGraphs.FanOut();
            var when = ScenarioTestGraphs.FindNode<ScenarioWhenNode>(graph, ScenarioTestGraphs.WhenTitle);

            var targets = new ScenarioWiring(graph).TargetsOf(when.Id, ScenarioWhenNode.Then.Id);

            Assert.That(targets.Count, Is.EqualTo(2));
            Assert.That(targets[0].Node.Title, Is.EqualTo(ScenarioTestGraphs.LeftTitle));
            Assert.That(targets[1].Node.Title, Is.EqualTo(ScenarioTestGraphs.RightTitle));
            Assert.That(targets[0].Input, Is.EqualTo(ScenarioStepsNode.In));
        }

        [Test]
        public void OutputWithoutWires_HasNoTargets()
        {
            var graph = ScenarioTestGraphs.FanOut();
            var when = ScenarioTestGraphs.FindNode<ScenarioWhenNode>(graph, ScenarioTestGraphs.WhenTitle);

            Assert.That(new ScenarioWiring(graph).TargetsOf(when.Id, ScenarioWhenNode.NotMet.Id), Is.Empty);
        }

        [Test]
        public void WaitForCaseOutput_IsIndexedByCaseId()
        {
            var graph = ScenarioTestGraphs.SilhouetteWaitFor();
            var waitLook = ScenarioTestGraphs.FindNode<ScenarioWaitForNode>(graph, ScenarioTestGraphs.WaitLookTitle);

            var targets = new ScenarioWiring(graph).TargetsOf(waitLook.Id, waitLook.Cases[1].Output.Id);

            Assert.That(targets.Count, Is.EqualTo(1));
            Assert.That(targets[0].Node.Title, Is.EqualTo(ScenarioTestGraphs.SilhouetteOnTitle));
        }

        [Test]
        public void WireToUnknownPort_IsSkippedWithWarning()
        {
            var graph = ScenarioTestGraphs.FanOut();
            var when = ScenarioTestGraphs.FindNode<ScenarioWhenNode>(graph, ScenarioTestGraphs.WhenTitle);
            var left = ScenarioTestGraphs.FindNode<ScenarioStepsNode>(graph, ScenarioTestGraphs.LeftTitle);

            AddRawWire(graph, new ScenarioWire(when.Id, "case.gone", left.Id, ScenarioStepsNode.In.Id));
            AddRawWire(graph, new ScenarioWire(when.Id, ScenarioWhenNode.Then.Id, left.Id, "nowhere"));
            AddRawWire(graph, new ScenarioWire(when.Id, ScenarioWhenNode.Then.Id, 999, ScenarioStepsNode.In.Id));

            LogAssert.Expect(LogType.Warning, new Regex("wire skipped.*no output “case.gone”"));
            LogAssert.Expect(LogType.Warning, new Regex("wire skipped.*no input “nowhere”"));
            LogAssert.Expect(LogType.Warning, new Regex("wire skipped.*node 999 is missing"));

            var wiring = new ScenarioWiring(graph);

            Assert.That(wiring.SkippedWires.Count, Is.EqualTo(3));
            Assert.That(wiring.TargetsOf(when.Id, ScenarioWhenNode.Then.Id).Count, Is.EqualTo(2));
        }

        [Test]
        public void EmptyOrMissingGraph_HasNoTargets()
        {
            Assert.That(new ScenarioWiring(null).TargetsOf(1, "then"), Is.Empty);
            Assert.That(new ScenarioWiring(new ScenarioGraph()).TargetsOf(1, "then"), Is.Empty);
        }


        // Провод в обход Connect — такой остаётся в данных, когда тип ноды переименовали или исход удалили руками.
        private static void AddRawWire(ScenarioGraph graph, ScenarioWire wire)
        {
            var wires = (List<ScenarioWire>)typeof(ScenarioGraph)
                .GetField("wires", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(graph);

            wires.Add(wire);
        }
    }
}
