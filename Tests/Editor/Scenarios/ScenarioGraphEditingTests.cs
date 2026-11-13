using System.Linq;
using System.Reflection;
using FeatherKit.Scenarios;
using NUnit.Framework;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>Правки графа через его методы: удаление чистит провода и рамки, исходы Wait For не теряют провода и id.</summary>
    public class ScenarioGraphEditingTests
    {
        // ── Ноды и провода ───────────────────────────────────────────────

        [Test]
        public void RemoveNode_RemovesItsWiresAndGroupPlace()
        {
            var graph = ScenarioTestGraphs.WithVariables();
            var lampOn = ScenarioTestGraphs.FindNode<ScenarioStepsNode>(graph, ScenarioTestGraphs.LampOnTitle);

            Assert.That(graph.RemoveNode(lampOn.Id), Is.True);

            Assert.That(graph.FindNode(lampOn.Id), Is.Null);
            Assert.That(graph.Wires.Any(wire => wire.FromNodeId == lampOn.Id || wire.ToNodeId == lampOn.Id), Is.False);
            Assert.That(graph.Groups[0].NodeIds, Has.No.Member(lampOn.Id));
            Assert.That(graph.RemoveNode(lampOn.Id), Is.False);
        }

        [Test]
        public void RemovedNodeId_IsNotGivenAgain()
        {
            var graph = new ScenarioGraph();
            var first = graph.AddNode(new ScenarioStepsNode());
            graph.RemoveNode(first.Id);

            Assert.That(graph.AddNode(new ScenarioStepsNode()).Id, Is.Not.EqualTo(first.Id));
        }

        [Test]
        public void Disconnect_RemovesOnlyThatWire()
        {
            var graph = ScenarioTestGraphs.FanOut();
            var wireCount = graph.Wires.Count;

            Assert.That(graph.Disconnect(graph.Wires[0]), Is.True);
            Assert.That(graph.Wires.Count, Is.EqualTo(wireCount - 1));
        }

        [Test]
        public void DisconnectPort_RemovesAllWiresOfThatOutput()
        {
            var graph = ScenarioTestGraphs.FanOut();
            var when = ScenarioTestGraphs.FindNode<ScenarioWhenNode>(graph, ScenarioTestGraphs.WhenTitle);

            Assert.That(graph.DisconnectPort(when.Id, ScenarioWhenNode.Then.Id), Is.EqualTo(2));
            Assert.That(graph.Wires, Is.Empty);
        }


        // ── Переменные, рамки, заметки ───────────────────────────────────

        [Test]
        public void RemoveVariableGroupNote_ById()
        {
            var graph = ScenarioTestGraphs.WithVariables();
            var variable = graph.Variables[0];
            var group = graph.Groups[0];
            var note = graph.Notes[0];
            var nodeCount = graph.Nodes.Count;

            Assert.That(graph.RemoveVariable(variable.Id), Is.True);
            Assert.That(graph.RemoveGroup(group.Id), Is.True);
            Assert.That(graph.RemoveNote(note.Id), Is.True);

            Assert.That(graph.FindVariable(variable.Id), Is.Null);
            Assert.That(graph.Groups, Is.Empty);
            Assert.That(graph.Notes, Is.Empty);
            Assert.That(graph.Nodes.Count, Is.EqualTo(nodeCount), "Removing a group must keep its nodes");
            Assert.That(graph.RemoveVariable(variable.Id), Is.False);
        }


        // ── Исходы Wait For ──────────────────────────────────────────────

        [Test]
        public void RemoveWaitCase_RemovesCaseAndItsWiresOnly()
        {
            var graph = ScenarioTestGraphs.SilhouetteWaitFor();
            var waitLook = ScenarioTestGraphs.FindNode<ScenarioWaitForNode>(graph, ScenarioTestGraphs.WaitLookTitle);
            var lookedAt = waitLook.Cases[0];
            var wiresBefore = graph.Wires.Count;

            Assert.That(graph.RemoveWaitCase(waitLook, lookedAt.Id), Is.True);

            Assert.That(waitLook.FindCase(lookedAt.Id), Is.Null);
            Assert.That(graph.Wires.Any(wire => wire.FromPortId == lookedAt.Output.Id), Is.False);
            Assert.That(graph.Wires.Count, Is.EqualTo(wiresBefore - 1));
            Assert.That(graph.RemoveWaitCase(waitLook, lookedAt.Id), Is.False);
        }

        [Test]
        public void InsertAndMoveCase_KeepOrder()
        {
            var waitFor = new ScenarioWaitForNode();
            var first = waitFor.AddCase(new ScenarioWaitCase(new ScenarioTestSwitchCondition(true), "First"));
            var second = waitFor.AddCase(new ScenarioWaitCase(new ScenarioTestSwitchCondition(true), "Second"));
            var inserted = waitFor.InsertCase(0, new ScenarioWaitCase(new ScenarioTestSwitchCondition(true), "Inserted"));

            Assert.That(waitFor.Cases, Is.EqualTo(new[] { inserted, first, second }));

            waitFor.MoveCase(0, 2);

            Assert.That(waitFor.Cases, Is.EqualTo(new[] { first, second, inserted }));
        }

        [Test]
        public void InsertCase_WithTakenId_GetsNewId()
        {
            var waitFor = new ScenarioWaitForNode();
            var original = waitFor.AddCase(new ScenarioWaitCase(new ScenarioTestSwitchCondition(true)));

            var copy = new ScenarioWaitCase(new ScenarioTestSwitchCondition(true));
            ForceCaseId(copy, original.Id);
            waitFor.InsertCase(1, copy);

            Assert.That(copy.Id, Is.Not.EqualTo(original.Id));
        }

        [Test]
        public void EnsureUniqueCaseIds_FixesRepeats()
        {
            var waitFor = new ScenarioWaitForNode();
            waitFor.AddCase(new ScenarioWaitCase(new ScenarioTestSwitchCondition(true)));
            waitFor.AddCase(new ScenarioWaitCase(new ScenarioTestSwitchCondition(true)));

            Assert.That(waitFor.EnsureUniqueCaseIds(), Is.False);

            // Как копия элемента в инспекторе: id второго исхода совпал с первым.
            var firstId = waitFor.Cases[0].Id;
            ForceCaseId(waitFor.Cases[1], firstId);

            Assert.That(waitFor.EnsureUniqueCaseIds(), Is.True);
            Assert.That(waitFor.Cases[1].Id, Is.Not.EqualTo(firstId));
            Assert.That(waitFor.Cases[0].Id, Is.EqualTo(firstId), "The first of the repeats keeps its id");
        }


        // Так id повторяется при копировании элемента списка в инспекторе — снаружи API его не задать.
        private static void ForceCaseId(ScenarioWaitCase waitCase, string id)
        {
            typeof(ScenarioWaitCase).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(waitCase, id);
        }
    }
}
