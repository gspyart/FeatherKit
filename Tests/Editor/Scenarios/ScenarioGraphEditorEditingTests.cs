using System.Linq;
using FeatherKit.EditorTools;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Правки графа из редактора: ноды, провода, исходы Wait For, переменные — и undo каждой правки.
    /// Граф лежит в граф-файле в памяти: так undo работает, а проект не трогается.
    /// </summary>
    public class ScenarioGraphEditorEditingTests
    {
        private ScenarioGraphAsset graphFile;
        private ScenarioGraphEditing editing;


        private ScenarioGraph Graph => graphFile.Graph;


        [SetUp]
        public void CreateGraphFile()
        {
            graphFile = ScriptableObject.CreateInstance<ScenarioGraphAsset>();
            graphFile.Graph = ScenarioTestGraphs.SilhouetteWaitFor();
            editing = new ScenarioGraphEditing(ScenarioGraphHost.For(graphFile));
        }

        [TearDown]
        public void DestroyGraphFile()
        {
            Undo.ClearUndo(graphFile);
            Object.DestroyImmediate(graphFile);
        }


        // ── Ноды ─────────────────────────────────────────────────────────

        [Test]
        public void AddNode_ByStepBlock_GivesDoWithThatStep()
        {
            var node = editing.AddNode(typeof(WaitAction), new Vector2(10.4f, 20.6f));

            Assert.That(node, Is.TypeOf<ScenarioStepsNode>());
            Assert.That(((ScenarioStepsNode)node).Steps.Count, Is.EqualTo(1));
            Assert.That(Graph.FindNode(node.Id), Is.SameAs(node));
            Assert.That(node.Position, Is.EqualTo(new Vector2(10f, 21f)), "Positions are rounded to whole numbers");
        }

        [Test]
        public void AddNode_ByEventBlock_GivesWhenWithThatEvent()
        {
            var node = (ScenarioWhenNode)editing.AddNode(typeof(ObjectStartedEvent), Vector2.zero);

            Assert.That(node.Events.Single(), Is.TypeOf<ObjectStartedEvent>());
        }

        [Test]
        public void AddNode_ByFlowNodeType_GivesThatNode()
        {
            Assert.That(editing.AddNode(typeof(ScenarioGuardNode), Vector2.zero), Is.TypeOf<ScenarioGuardNode>());
        }

        [Test]
        public void AddNode_Undo_RemovesIt_Redo_BringsItBack()
        {
            var nodeCount = Graph.Nodes.Count;

            Undo.IncrementCurrentGroup();
            var nodeId = editing.AddNode(typeof(ScenarioStepsNode), Vector2.zero).Id;

            Undo.PerformUndo();
            Assert.That(Graph.Nodes.Count, Is.EqualTo(nodeCount));
            Assert.That(Graph.FindNode(nodeId), Is.Null);

            Undo.PerformRedo();
            Assert.That(Graph.FindNode(nodeId), Is.Not.Null);
        }

        [Test]
        public void MoveNode_RoundsPosition_UndoMovesBack()
        {
            var node = Graph.Nodes[0];
            var oldPosition = node.Position;

            Undo.IncrementCurrentGroup();
            editing.MoveNode(node.Id, new Vector2(123.7f, -4.2f));
            Assert.That(Graph.FindNode(node.Id).Position, Is.EqualTo(new Vector2(124f, -4f)));

            Undo.PerformUndo();
            Assert.That(Graph.FindNode(node.Id).Position, Is.EqualTo(oldPosition));
        }

        [Test]
        public void RemoveNodes_RemovesTheirWires_UndoRestoresBoth()
        {
            var waitLook = ScenarioTestGraphs.FindNode<ScenarioWaitForNode>(Graph, ScenarioTestGraphs.WaitLookTitle);
            var waitLookId = waitLook.Id;
            var wireCount = Graph.Wires.Count;

            Undo.IncrementCurrentGroup();
            editing.RemoveNodes(new[] { waitLookId });

            Assert.That(Graph.FindNode(waitLookId), Is.Null);
            Assert.That(Graph.Wires.Any(wire => wire.FromNodeId == waitLookId || wire.ToNodeId == waitLookId), Is.False);

            Undo.PerformUndo();
            Assert.That(Graph.FindNode(waitLookId), Is.Not.Null);
            Assert.That(Graph.Wires.Count, Is.EqualTo(wireCount));
        }


        // ── Провода ──────────────────────────────────────────────────────

        [Test]
        public void Connect_AddsWire_UndoRemovesIt()
        {
            var timedOut = ScenarioTestGraphs.FindNode<ScenarioStepsNode>(Graph, ScenarioTestGraphs.TimedOutTitle);
            var silhouetteOff = ScenarioTestGraphs.FindNode<ScenarioStepsNode>(Graph, ScenarioTestGraphs.SilhouetteOffTitle);
            var wireCount = Graph.Wires.Count;

            Undo.IncrementCurrentGroup();
            var wire = editing.Connect(timedOut.Id, ScenarioStepsNode.Done.Id, silhouetteOff.Id, ScenarioStepsNode.In.Id);

            Assert.That(wire, Is.Not.Null);
            Assert.That(Graph.Wires.Count, Is.EqualTo(wireCount + 1));

            Undo.PerformUndo();
            Assert.That(Graph.Wires.Count, Is.EqualTo(wireCount));
        }

        [Test]
        public void Connect_ToUnknownPort_ReturnsNull_AndAddsNothing()
        {
            var timedOut = ScenarioTestGraphs.FindNode<ScenarioStepsNode>(Graph, ScenarioTestGraphs.TimedOutTitle);
            var wireCount = Graph.Wires.Count;

            Assert.That(editing.Connect(timedOut.Id, "no such output", timedOut.Id, ScenarioStepsNode.In.Id), Is.Null);
            Assert.That(Graph.Wires.Count, Is.EqualTo(wireCount));
        }

        [Test]
        public void Disconnect_RemovesOnlyGivenWire()
        {
            var wire = Graph.Wires[0];
            var wireCount = Graph.Wires.Count;

            editing.Disconnect(new[] { wire });

            Assert.That(Graph.Wires.Count, Is.EqualTo(wireCount - 1));
            Assert.That(Graph.Wires, Has.No.Member(wire));
        }


        // ── Исходы Wait For ──────────────────────────────────────────────

        [Test]
        public void AddWaitCase_AddsOutputBeforeTimedOut()
        {
            var waitLook = ScenarioTestGraphs.FindNode<ScenarioWaitForNode>(Graph, ScenarioTestGraphs.WaitLookTitle);

            var waitCase = editing.AddWaitCase(waitLook, new ChanceCondition());
            var outputs = waitLook.CollectPorts().Outputs;

            Assert.That(waitCase.IsEvent, Is.False);
            Assert.That(outputs[outputs.Count - 2].Id, Is.EqualTo(waitCase.Output.Id));
            Assert.That(outputs[outputs.Count - 1], Is.EqualTo(ScenarioWaitForNode.TimedOut));
        }

        [Test]
        public void MoveWaitCase_ChangesOrder_KeepsWires()
        {
            var waitLook = ScenarioTestGraphs.FindNode<ScenarioWaitForNode>(Graph, ScenarioTestGraphs.WaitLookTitle);
            var first = waitLook.Cases[0];
            var wireCount = Graph.Wires.Count;

            editing.MoveWaitCase(waitLook, 0, 1);

            Assert.That(waitLook.Cases[1], Is.SameAs(first));
            Assert.That(Graph.Wires.Count, Is.EqualTo(wireCount));
        }

        [Test]
        public void RemoveWaitCase_RemovesItsWires_UndoRestoresCaseAndWire()
        {
            var waitLook = ScenarioTestGraphs.FindNode<ScenarioWaitForNode>(Graph, ScenarioTestGraphs.WaitLookTitle);
            var waitLookId = waitLook.Id;
            var lookedAt = waitLook.Cases[0];
            var caseId = lookedAt.Id;
            var outputId = lookedAt.Output.Id;
            var wireCount = Graph.Wires.Count;

            Undo.IncrementCurrentGroup();
            editing.RemoveWaitCase(waitLook, caseId);

            Assert.That(waitLook.FindCase(caseId), Is.Null);
            Assert.That(Graph.Wires.Any(wire => wire.FromPortId == outputId), Is.False);

            Undo.PerformUndo();
            var restoredNode = (ScenarioWaitForNode)Graph.FindNode(waitLookId);
            Assert.That(restoredNode.FindCase(caseId), Is.Not.Null);
            Assert.That(Graph.Wires.Count, Is.EqualTo(wireCount));
        }


        // ── Переменные ───────────────────────────────────────────────────

        [Test]
        public void AddVariable_GivesUniqueNames()
        {
            var first = editing.AddVariable(ScenarioVariableKind.Flag);
            var second = editing.AddVariable(ScenarioVariableKind.Counter);

            Assert.That(second.Name, Is.Not.EqualTo(first.Name));
            Assert.That(second.Kind, Is.EqualTo(ScenarioVariableKind.Counter));
            Assert.That(Graph.Variables, Has.Member(first).And.Member(second));
        }

        [Test]
        public void RenameVariable_ToTakenName_GetsNumber()
        {
            var door = editing.AddVariable(ScenarioVariableKind.Flag);
            editing.RenameVariable(door.Id, "Door");
            var other = editing.AddVariable(ScenarioVariableKind.Flag);

            editing.RenameVariable(other.Id, "Door");

            Assert.That(door.Name, Is.EqualTo("Door"));
            Assert.That(other.Name, Is.EqualTo("Door 2"));
        }

        [Test]
        public void SetVariableObjectType_ChangesFittingFields()
        {
            var lamp = editing.AddVariable(ScenarioVariableKind.Object);

            editing.SetVariableObjectType(lamp.Id, typeof(Light));

            Assert.That(lamp.ObjectType, Is.EqualTo(typeof(Light)));
            Assert.That(ScenarioVariableKinds.Fits(lamp, typeof(Light)), Is.True);
            Assert.That(ScenarioVariableKinds.Fits(lamp, typeof(AudioClip)), Is.False);
        }

        [Test]
        public void RemoveVariable_UndoRestoresIt()
        {
            var variable = editing.AddVariable(ScenarioVariableKind.Number);
            var variableId = variable.Id;

            Undo.IncrementCurrentGroup();
            editing.RemoveVariable(variableId);
            Assert.That(Graph.FindVariable(variableId), Is.Null);

            Undo.PerformUndo();
            Assert.That(Graph.FindVariable(variableId), Is.Not.Null);
        }


        // ── Заметки ──────────────────────────────────────────────────────

        [Test]
        public void Note_AddEditRemove_UndoBringsItBack()
        {
            var note = editing.AddNote(new Vector2(10.4f, 20.6f));
            var noteId = note.Id;

            editing.SetNoteText(noteId, "Lamp comes from the bindings");
            editing.SetNoteColor(noteId, Color.red);
            editing.SetNoteArea(noteId, new Rect(5.2f, 6.7f, 300.4f, 150.6f));

            Assert.That(note.Text, Is.EqualTo("Lamp comes from the bindings"));
            Assert.That(note.Color, Is.EqualTo(Color.red));
            Assert.That(note.Area, Is.EqualTo(new Rect(5f, 7f, 300f, 151f)), "Place and size are rounded like node positions");

            Undo.IncrementCurrentGroup();
            editing.RemoveNotes(new[] { noteId });
            Assert.That(Graph.Notes.Any(existing => existing.Id == noteId), Is.False);

            Undo.PerformUndo();
            var restored = Graph.Notes.Single(existing => existing.Id == noteId);
            Assert.That(restored.Color, Is.EqualTo(Color.red));
        }


        // ── Где лежит граф ───────────────────────────────────────────────

        [Test]
        public void Host_NodeProperty_PointsAtThatNode()
        {
            var host = ScenarioGraphHost.For(graphFile);
            var waitLook = ScenarioTestGraphs.FindNode<ScenarioWaitForNode>(Graph, ScenarioTestGraphs.WaitLookTitle);

            Assert.That(host.NodeProperty(waitLook.Id).managedReferenceValue, Is.SameAs(waitLook));
        }

        [Test]
        public void Host_ForScenario_FollowsGraphSource()
        {
            // Скрытый и несохраняемый: тест не должен помечать изменённой открытую сцену.
            var gameObject = EditorUtility.CreateGameObjectWithHideFlags("Scenario Host Test", HideFlags.HideAndDontSave);

            try
            {
                var scenario = gameObject.AddComponent<Scenario>();
                Assert.That(ScenarioGraphHost.For(gameObject).Owner, Is.SameAs(scenario), "Embedded graph lives in the component");

                var serializedScenario = new SerializedObject(scenario);
                serializedScenario.FindProperty("graphSource").enumValueIndex = (int)ScenarioGraphSource.Asset;
                serializedScenario.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(ScenarioGraphHost.For(scenario), Is.Null, "No graph file picked yet");

                serializedScenario.FindProperty("graphAsset").objectReferenceValue = graphFile;
                serializedScenario.ApplyModifiedPropertiesWithoutUndo();

                var host = ScenarioGraphHost.For(scenario);
                Assert.That(host.Owner, Is.SameAs(graphFile));
                Assert.That(host.Scenario, Is.SameAs(scenario));
                Assert.That(host.CanReferenceSceneObjects, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
