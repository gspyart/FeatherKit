using System.Linq;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Контракт графа сценария: порты находятся, граф переживает сохранение в файл,
    /// копия граф-файла не делит данные с оригиналом, события сообщают слушателю.
    /// </summary>
    public class ScenarioContractTests
    {
        private const string TempFolder = "Assets/ScenarioContractTestTemp";
        private const string TempAssetPath = TempFolder + "/Graph.asset";


        [TearDown]
        public void DeleteTempFolder()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.DeleteAsset(TempFolder);
        }


        // ── Порты ────────────────────────────────────────────────────────

        [Test]
        public void WhenNode_HasNoInputs_AndThenNotMetOutputs()
        {
            var ports = new ScenarioWhenNode().CollectPorts();

            Assert.That(ports.Inputs, Is.Empty);
            Assert.That(OutputIds(ports), Is.EqualTo(new[] { "then", "notMet" }));
        }

        [Test]
        public void StepsNode_HasInAndDone()
        {
            var ports = new ScenarioStepsNode().CollectPorts();

            Assert.That(InputIds(ports), Is.EqualTo(new[] { "in" }));
            Assert.That(OutputIds(ports), Is.EqualTo(new[] { "done" }));
        }

        [Test]
        public void GuardNode_HasInheritedIn_AndOwnOutputsInDeclarationOrder()
        {
            var ports = new ScenarioGuardNode().CollectPorts();

            Assert.That(InputIds(ports), Is.EqualTo(new[] { "in" }));
            Assert.That(OutputIds(ports), Is.EqualTo(new[] { "body", "broken", "done" }));
        }

        [Test]
        public void CustomNode_FindsPrivateStaticOutputs()
        {
            var ports = new ScenarioTestCoinFlipNode(0.5f).CollectPorts();

            Assert.That(InputIds(ports), Is.EqualTo(new[] { "in" }));
            Assert.That(OutputIds(ports), Is.EqualTo(new[] { ScenarioTestCoinFlipNode.HeadsId, ScenarioTestCoinFlipNode.TailsId }));
        }

        [Test]
        public void WaitForNode_HasCaseOutputsBeforeTimedOut()
        {
            var graph = ScenarioTestGraphs.SilhouetteWaitFor();
            var waitLook = ScenarioTestGraphs.FindNode<ScenarioWaitForNode>(graph, ScenarioTestGraphs.WaitLookTitle);
            var ports = waitLook.CollectPorts();

            var expectedIds = waitLook.Cases.Select(waitCase => ScenarioWaitCase.OutputIdPrefix + waitCase.Id)
                .Append("timedOut");

            Assert.That(OutputIds(ports), Is.EqualTo(expectedIds));
            Assert.That(ports.Outputs[0].Label, Is.EqualTo(ScenarioTestGraphs.LookedAtCaseLabel));
            Assert.That(ports.Outputs[1].Label, Is.EqualTo(ScenarioTestGraphs.LeftZoneCaseLabel));
        }

        [Test]
        public void StaticPorts_AreCachedPerType()
        {
            Assert.That(ScenarioNodePorts.OutputsOf(typeof(ScenarioGuardNode)),
                Is.SameAs(ScenarioNodePorts.OutputsOf(typeof(ScenarioGuardNode))));
        }

        [Test]
        public void Ports_AreEqualById()
        {
            var sameId = new ScenarioOutput("then", "Other Label");

            Assert.That(sameId == ScenarioWhenNode.Then, Is.True);
            Assert.That(sameId.GetHashCode(), Is.EqualTo(ScenarioWhenNode.Then.GetHashCode()));
            Assert.That(new ScenarioInput("in", "In") != ScenarioFlowNode.In, Is.False);
        }

        [Test]
        public void PortList_SkipsRepeatedId()
        {
            var ports = new ScenarioPortList();
            ports.AddOutput(new ScenarioOutput("a", "First"));
            ports.AddOutput(new ScenarioOutput("a", "Second"));

            Assert.That(ports.Outputs.Count, Is.EqualTo(1));
            Assert.That(ports.Outputs[0].Label, Is.EqualTo("First"));
        }

        [Test]
        public void Connect_ToMissingPort_Throws()
        {
            var graph = new ScenarioGraph();
            var when = graph.AddNode(new ScenarioWhenNode());
            var steps = graph.AddNode(new ScenarioStepsNode());

            Assert.Throws<System.ArgumentException>(() =>
                graph.Connect(when, ScenarioStepsNode.Done, steps, ScenarioStepsNode.In));
        }


        // ── Сохранение в файл ────────────────────────────────────────────

        [Test]
        public void GraphAsset_SurvivesSaveAndLoad()
        {
            var original = ScenarioTestGraphs.SilhouetteWaitFor();
            var variablesSource = ScenarioTestGraphs.WithVariables();

            foreach (var variable in variablesSource.Variables)
                original.AddVariable(variable);

            original.AddGroup(variablesSource.Groups[0]);
            original.AddNote(variablesSource.Notes[0]);

            var expected = Describe(original);
            var originalWaitLook = ScenarioTestGraphs.FindNode<ScenarioWaitForNode>(original, ScenarioTestGraphs.WaitLookTitle);
            var expectedPortIds = OutputIds(originalWaitLook.CollectPorts());

            var asset = ScriptableObject.CreateInstance<ScenarioGraphAsset>();
            asset.Graph = original;

            AssetDatabase.CreateFolder("Assets", "ScenarioContractTestTemp");
            AssetDatabase.CreateAsset(asset, TempAssetPath);
            AssetDatabase.SaveAssets();
            Resources.UnloadAsset(asset);

            var loaded = AssetDatabase.LoadAssetAtPath<ScenarioGraphAsset>(TempAssetPath);

            Assert.That(loaded.Graph, Is.Not.SameAs(original), "The asset was not reloaded from disk");
            Assert.That(Describe(loaded.Graph), Is.EqualTo(expected));

            var loadedWaitLook = ScenarioTestGraphs.FindNode<ScenarioWaitForNode>(loaded.Graph, ScenarioTestGraphs.WaitLookTitle);
            Assert.That(OutputIds(loadedWaitLook.CollectPorts()), Is.EqualTo(expectedPortIds));

            var lamp = loaded.Graph.Variables.Single(variable => variable.Name == "Lamp");
            Assert.That(lamp.ObjectType, Is.EqualTo(typeof(Light)));
            Assert.That(lamp.IsExposed, Is.True);

            var newNode = loaded.Graph.AddNode(new ScenarioStepsNode());
            Assert.That(newNode.Id, Is.GreaterThan(loaded.Graph.Nodes.Where(node => node != newNode).Max(node => node.Id)));
        }


        // ── Копия граф-файла ─────────────────────────────────────────────

        [Test]
        public void InstantiatedAsset_IsIndependentCopy()
        {
            var asset = ScriptableObject.CreateInstance<ScenarioGraphAsset>();
            asset.Graph = ScenarioTestGraphs.SilhouetteWaitFor();

            var copy = Object.Instantiate(asset);

            try
            {
                Assert.That(Describe(copy.Graph), Is.EqualTo(Describe(asset.Graph)));
                Assert.That(copy.Graph, Is.Not.SameAs(asset.Graph));

                var originalWait = ScenarioTestGraphs.FindNode<ScenarioWaitForNode>(asset.Graph, ScenarioTestGraphs.WaitLookTitle);
                var copiedWait = ScenarioTestGraphs.FindNode<ScenarioWaitForNode>(copy.Graph, ScenarioTestGraphs.WaitLookTitle);

                Assert.That(copiedWait, Is.Not.SameAs(originalWait));
                Assert.That(copiedWait.Cases[0].Condition, Is.Not.SameAs(originalWait.Cases[0].Condition));

                copiedWait.Timeout = 5f;
                copiedWait.Position = new Vector2(-100f, -100f);
                ((ScenarioTestSwitchCondition)copiedWait.Cases[0].Condition).IsOn = true;

                Assert.That(originalWait.Timeout, Is.EqualTo(1f));
                Assert.That(originalWait.Position, Is.Not.EqualTo(new Vector2(-100f, -100f)));
                Assert.That(((ScenarioTestSwitchCondition)originalWait.Cases[0].Condition).IsOn, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(copy);
                Object.DestroyImmediate(asset);
            }
        }


        // ── Слушатель событий ────────────────────────────────────────────

        [Test]
        public void Event_ReportsToListener_UntilReleased()
        {
            var listener = new RecordingEventListener();
            var scenarioEvent = new ScenarioTestManualEvent("door");
            var instigator = EditorUtility.CreateGameObjectWithHideFlags("Instigator", HideFlags.HideAndDontSave);

            try
            {
                scenarioEvent.Attach(listener);
                scenarioEvent.Raise(instigator);

                Assert.That(scenarioEvent.IsAttached, Is.True);
                Assert.That(listener.LastFiredEvent, Is.SameAs(scenarioEvent));
                Assert.That(listener.LastInstigator, Is.SameAs(instigator));

                scenarioEvent.Release();
                listener.LastFiredEvent = null;
                scenarioEvent.Raise(instigator);

                Assert.That(scenarioEvent.IsAttached, Is.False);
                Assert.That(listener.LastFiredEvent, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(instigator);
            }
        }


        [Test]
        public void Event_SecondAttachWithoutRelease_Throws()
        {
            var scenarioEvent = new ScenarioTestManualEvent("door");
            scenarioEvent.Attach(new RecordingEventListener());

            Assert.Throws<System.InvalidOperationException>(() => scenarioEvent.Attach(new RecordingEventListener()));

            scenarioEvent.Release();
            scenarioEvent.Release();
            Assert.DoesNotThrow(() => scenarioEvent.Attach(new RecordingEventListener()));
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private static string[] InputIds(ScenarioPortList ports)
        {
            return ports.Inputs.Select(input => input.Id).ToArray();
        }

        private static string[] OutputIds(ScenarioPortList ports)
        {
            return ports.Outputs.Select(output => output.Id).ToArray();
        }

        // Всё сохраняемое в графе одной строкой: два графа с одинаковым описанием совпадают по данным.
        private static string Describe(ScenarioGraph graph)
        {
            var nodes = graph.Nodes.Select(node => $"{node.Id}:{node.GetType().Name}:{node.Title}:{node.Position}:{DescribeNodeContent(node)}");
            var wires = graph.Wires.Select(wire => $"{wire.FromNodeId}.{wire.FromPortId}->{wire.ToNodeId}.{wire.ToPortId}");
            var variables = graph.Variables.Select(variable =>
                $"{variable.Id}:{variable.Name}:{variable.Kind}:{variable.IsExposed}:{variable.IsSaved}:{variable.ObjectType.Name}:" +
                $"{variable.InitialValue.FlagValue}:{variable.InitialValue.CounterValue}:{variable.InitialValue.NumberValue}");
            var groups = graph.Groups.Select(group => $"{group.Id}:{group.Title}:{string.Join(",", group.NodeIds)}");
            var notes = graph.Notes.Select(note => $"{note.Id}:{note.Text}:{note.Area}");

            return string.Join("\n", nodes.Concat(wires).Concat(variables).Concat(groups).Concat(notes));
        }

        private static string DescribeNodeContent(ScenarioNode node)
        {
            return node switch
            {
                ScenarioWhenNode whenNode => $"events {whenNode.Events.Count}, conditions {whenNode.Conditions.Count}, {whenNode.RerunRule}, {whenNode.RunsOnlyOnce}",
                ScenarioStepsNode steps => $"steps {steps.Steps.Count}",
                ScenarioWaitForNode waitFor => $"timeout {waitFor.Timeout}, " + string.Join(",", waitFor.Cases.Select(waitCase =>
                    $"{waitCase.Id}/{waitCase.Label}/{waitCase.AwaitedEvent?.GetType().Name}/{waitCase.Condition?.GetType().Name}")),
                ScenarioGuardNode guard => $"conditions {guard.Conditions.Count}",
                ScenarioTestCoinFlipNode coin => $"heads {coin.HeadsChance}",
                _ => ""
            };
        }


        /// <summary>Слушатель для теста: запоминает последнее сработавшее событие.</summary>
        private class RecordingEventListener : IScenarioEventListener
        {
            public ScenarioEvent LastFiredEvent { get; set; }

            public GameObject LastInstigator { get; private set; }

            // IScenarioEventListener
            public Scenario Scenario => null;

            // IScenarioEventListener
            public void HandleEventFired(ScenarioEvent firedEvent, GameObject instigator)
            {
                LastFiredEvent = firedEvent;
                LastInstigator = instigator;
            }
        }
    }
}
