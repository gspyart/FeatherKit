using System.Collections;
using System.Text.RegularExpressions;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Переменные графа и граф-файл: у каждого объекта своя копия графа, свои переменные и Only Once,
    /// привязки открытых переменных, время жизни общих флагов Level.
    /// </summary>
    public class ScenarioGraphVariablesTests : ScenarioPlayModeTestBase
    {
        private const string LevelTestScenePath = "Packages/com.gspy.featherkit/Tests/Runtime/Scenarios/ScenarioLevelTestScene.unity";


        // ── Переменные графа ─────────────────────────────────────────────

        [Test]
        public void FlagVariable_IsReadAndWrittenThroughReference()
        {
            var graph = new ScenarioGraph();
            var doorOpen = graph.AddVariable(new ScenarioVariableDefinition("Door Open", ScenarioVariableKind.Flag));
            var door = ScenarioFlagReference.FromVariable(doorOpen.Id);
            var when = AddWhen(graph);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Steps",
                new ScenarioTestRecordFlagAction("door", door), new ScenarioTestSetFlagAction(door, true),
                new ScenarioTestRecordFlagAction("door", door)));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Steps", "door=False", "door=True" }));
            Assert.That(doorOpen.InitialValue.FlagValue, Is.False, "Writing does not touch the initial value");
        }

        [Test]
        public void MissingVariable_FailsRun()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Steps",
                new ScenarioTestRecordFlagAction("gone", ScenarioFlagReference.FromVariable("gone"))));
            var scenario = CreateScenario(graph);

            LogAssert.Expect(LogType.Error, new Regex("Graph variable “gone” not found"));
            LogAssert.Expect(LogType.Exception, new Regex("Graph variable “gone” not found"));

            RaiseWhen(scenario);

            Assert.That(ReportedRuns[0].Outcome, Is.EqualTo(ScenarioRunOutcome.Failed));
        }


        // ── Граф-файл: копия на объект ───────────────────────────────────

        [Test]
        public void GraphAsset_TwoObjects_KeepOwnOnlyOnceAndVariables()
        {
            var graph = new ScenarioGraph();
            var doorOpen = graph.AddVariable(new ScenarioVariableDefinition("Door Open", ScenarioVariableKind.Flag));
            var levers = graph.AddVariable(new ScenarioVariableDefinition("Levers", ScenarioVariableKind.Counter));
            levers.InitialValue.CounterValue = 2;
            var door = ScenarioFlagReference.FromVariable(doorOpen.Id);
            var when = AddWhen(graph);
            when.RunsOnlyOnce = true;
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Steps",
                new ScenarioTestRecordFlagAction("door", door), new ScenarioTestSetFlagAction(door, true),
                new ScenarioTestCountAction("levers", ScenarioCounterReference.FromVariable(levers.Id))));
            var graphAsset = CreateGraphAsset(graph);

            var first = CreateScenario(graphAsset, "First");
            var second = CreateScenario(graphAsset, "Second");

            RaiseWhen(first);
            RaiseWhen(first);
            RaiseWhen(second);

            Assert.That(Marks, Is.EqualTo(new[] { "Steps", "door=False", "levers=3", "Steps", "door=False", "levers=3" }));
            Assert.That(first.Graph, Is.Not.SameAs(graphAsset.Graph));
            Assert.That(first.Graph, Is.Not.SameAs(second.Graph));
            Assert.That(graphAsset.Graph.Nodes[0], Is.InstanceOf<ScenarioWhenNode>());
            Assert.That(((ScenarioWhenNode)graphAsset.Graph.Nodes[0]).Events[0].IsAttached, Is.False,
                "The asset itself is never attached — only its copies");
        }

        [UnityTest]
        public IEnumerator GraphAssetCopy_IsDestroyedWithScenario()
        {
            var graphAsset = CreateGraphAsset(ScenarioTestGraphs.WhenThenSteps());
            var assetsBefore = Resources.FindObjectsOfTypeAll<ScenarioGraphAsset>().Length;

            var scenario = CreateScenario(graphAsset);

            Assert.That(Resources.FindObjectsOfTypeAll<ScenarioGraphAsset>().Length, Is.EqualTo(assetsBefore + 1));

            Object.DestroyImmediate(scenario.gameObject);
            yield return null;

            Assert.That(Resources.FindObjectsOfTypeAll<ScenarioGraphAsset>().Length, Is.EqualTo(assetsBefore));
        }


        // ── Привязки ─────────────────────────────────────────────────────

        [Test]
        public void ExposedObjectVariable_TakesEachObjectsBinding_ConvertedToFieldType()
        {
            var graph = new ScenarioGraph();
            var lamp = graph.AddVariable(new ScenarioVariableDefinition("Lamp", ScenarioVariableKind.Object));
            lamp.ObjectType = typeof(GameObject);
            lamp.IsExposed = true;
            var when = AddWhen(graph);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Steps",
                new ScenarioTestRecordLightAction(ScenarioValue<Light>.FromVariable(lamp.Id))));
            var graphAsset = CreateGraphAsset(graph);

            var lampA = CreateGameObject("Lamp A");
            lampA.AddComponent<Light>();
            var lampB = CreateGameObject("Lamp B");
            var lightB = lampB.AddComponent<Light>();

            var first = CreateScenario(graphAsset, "First", Binding(lamp, lampA));
            var second = CreateScenario(graphAsset, "Second", Binding(lamp, lightB));
            var unbound = CreateScenario(graphAsset, "Unbound");

            RaiseWhen(first);
            RaiseWhen(second);
            RaiseWhen(unbound);

            Assert.That(Marks, Is.EqualTo(new[] { "Steps", "Lamp A", "Steps", "Lamp B", "Steps", "no light" }));
        }

        [Test]
        public void BindingOfHiddenVariable_IsIgnored()
        {
            var graph = new ScenarioGraph();
            var lamp = graph.AddVariable(new ScenarioVariableDefinition("Lamp", ScenarioVariableKind.Object));
            var when = AddWhen(graph);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Steps",
                new ScenarioTestRecordLightAction(ScenarioValue<Light>.FromVariable(lamp.Id))));
            var lampObject = CreateGameObject("Lamp");
            lampObject.AddComponent<Light>();

            var scenario = CreateScenario(CreateGraphAsset(graph), "Scenario", Binding(lamp, lampObject));
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Steps", "no light" }));
        }

        [Test]
        public void EmbeddedGraph_IgnoresBindingsLeftFromGraphFile()
        {
            var graph = new ScenarioGraph();
            var lamp = graph.AddVariable(new ScenarioVariableDefinition("Lamp", ScenarioVariableKind.Object));
            lamp.IsExposed = true;
            var when = AddWhen(graph);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Steps",
                new ScenarioTestRecordLightAction(ScenarioValue<Light>.FromVariable(lamp.Id))));
            var leftover = CreateGameObject("Leftover Lamp");
            leftover.AddComponent<Light>();

            var scenario = CreateScenario(graph, "Embedded", Binding(lamp, leftover));
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Steps", "no light" }));
        }

        private static ScenarioVariableBinding Binding(ScenarioVariableDefinition variable, Object value)
        {
            var binding = new ScenarioVariableBinding(variable.Id);
            binding.Value.ObjectValue = value;

            return binding;
        }


        // ── Блоки флагов и счётчиков на переменных графа ─────────────────

        [Test]
        public void FlagBlocks_ChangeCheckAndListenToGraphVariable()
        {
            var graph = new ScenarioGraph();
            var doorOpen = graph.AddVariable(new ScenarioVariableDefinition("Door Open", ScenarioVariableKind.Flag));
            var door = ScenarioFlagReference.FromVariable(doorOpen.Id);

            var setFlag = new SetFlagAction();
            SetField(setFlag, "flag", door);
            SetField(setFlag, "change", ScenarioSwitch.TurnOn);
            var flagIsOn = new FlagCondition();
            SetField(flagIsOn, "flag", door);
            var flagChanged = new FlagChangedEvent();
            SetField(flagChanged, "flag", door);

            var when = AddWhen(graph);
            var check = graph.AddNode(new ScenarioIfNode());
            check.Conditions.Add(flagIsOn);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, check);
            ConnectToIn(graph, check, ScenarioIfNode.True, AddSteps(graph, "Open"));
            ConnectToIn(graph, check, ScenarioIfNode.False, AddSteps(graph, "Closed", setFlag));
            var opened = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Opened" });
            opened.AddEvent(flagChanged);
            ConnectToIn(graph, opened, ScenarioWhenNode.Then, AddSteps(graph, "Heard Open"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Closed", "Heard Open", "Open" }));
        }

        [Test]
        public void CounterBlocks_ChangeCheckAndListenToGraphVariable()
        {
            var graph = new ScenarioGraph();
            var levers = graph.AddVariable(new ScenarioVariableDefinition("Levers", ScenarioVariableKind.Counter));
            var leverCount = ScenarioCounterReference.FromVariable(levers.Id);

            var addLever = new ChangeCounterAction();
            SetField(addLever, "counter", leverCount);
            var allPulled = new CounterCondition();
            SetField(allPulled, "counter", leverCount);
            SetField(allPulled, "value", 2);
            var reachedTwo = new CounterChangedEvent();
            SetField(reachedTwo, "counter", leverCount);
            SetField(reachedTwo, "value", 2);

            var when = AddWhen(graph);
            when.RerunRule = ScenarioRerunRule.RunAlongside;
            var check = graph.AddNode(new ScenarioIfNode());
            check.Conditions.Add(allPulled);
            var pull = AddSteps(graph, "Pull", addLever);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, pull);
            ConnectToIn(graph, pull, ScenarioStepsNode.Done, check);
            ConnectToIn(graph, check, ScenarioIfNode.True, AddSteps(graph, "All Pulled"));
            var reached = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Reached" });
            reached.AddEvent(reachedTwo);
            ConnectToIn(graph, reached, ScenarioWhenNode.Then, AddSteps(graph, "Heard Two"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Pull", "Pull", "Heard Two", "All Pulled" }));
        }


        // ── Время жизни общих флагов ─────────────────────────────────────

        [UnityTest]
        public IEnumerator LevelLifetime_ResetsOnLevelLoad_GameLifetimeStays()
        {
            var levelFlag = CreateScriptableObject<ScenarioFlag>();
            SetField(levelFlag, "lifetime", ScenarioVariableLifetime.Level);
            var gameFlag = CreateScriptableObject<ScenarioFlag>();
            var levelCounter = CreateScriptableObject<ScenarioCounter>();
            SetField(levelCounter, "lifetime", ScenarioVariableLifetime.Level);

            levelFlag.Value = true;
            gameFlag.Value = true;
            levelCounter.Value = 5;

            yield return LoadLevelScene(LoadSceneMode.Additive);

            Assert.That(levelFlag.Value, Is.True, "A scene loaded on top is not a new level");

            yield return LoadLevelScene(LoadSceneMode.Single);

            Assert.That(levelFlag.Value, Is.False);
            Assert.That(levelCounter.Value, Is.Zero);
            Assert.That(gameFlag.Value, Is.True);
        }

        private static IEnumerator LoadLevelScene(LoadSceneMode mode)
        {
#if UNITY_EDITOR
            var loading = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(LevelTestScenePath,
                new LoadSceneParameters(mode));

            while (!loading.isDone)
                yield return null;
#else
            Assert.Ignore("Loading a scene by path works only in the editor");
            yield break;
#endif
        }
    }
}
