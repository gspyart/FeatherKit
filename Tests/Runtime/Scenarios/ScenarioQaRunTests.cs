using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using FeatherKit.Contexts;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Проверки тестировщика: запуски и сценарий целиком — триггер в Wait For, Run Scenario, защита от петли
    /// между запусками, утечки, выключение и уничтожение, копия объекта. Красные — QA-Found.
    /// </summary>
    public class ScenarioQaRunTests : ScenarioPlayModeTestBase
    {
        private const BindingFlags PrivateField = BindingFlags.Instance | BindingFlags.NonPublic;


        // ── Wait For и выход из зоны ─────────────────────────────────────

        // Пример из плана: игрок вошёл в зону → пугалка → Wait For «Left Zone» (выход из триггера).
        // Игрок уже внутри, когда граф дошёл до Wait For, — его выход должен быть услышан.
        [UnityTest, Category("QA_Found")]
        public IEnumerator WaitFor_TriggerExit_PlayerAlreadyInsideWhenWaitStarts_IsHeard()
        {
            var graph = new ScenarioGraph();
            var when = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Entered" });
            when.AddEvent(PlayerTrigger(TriggerCrossing.Entered));
            var scare = AddSteps(graph, "Scare");
            var waitFor = graph.AddNode(new ScenarioWaitForNode());
            var leftZone = waitFor.AddCase(new ScenarioWaitCase(PlayerTrigger(TriggerCrossing.Exited), "Left Zone"));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, scare);
            ConnectToIn(graph, scare, ScenarioStepsNode.Done, waitFor);
            ConnectToIn(graph, waitFor, leftZone.Output, AddSteps(graph, "Left"));
            var scenario = CreateScenario(graph);
            var player = CreateGameObject("Player");
            player.tag = "Player";
            var playerCollider = player.AddComponent<BoxCollider>();

            scenario.SendMessage("OnTriggerEnter", playerCollider);

            Assert.That(Marks, Is.EqualTo(new[] { "Scare" }));

            yield return null;
            scenario.SendMessage("OnTriggerExit", playerCollider);
            yield return WaitFrames(2);

            Assert.That(Marks, Is.EqualTo(new[] { "Scare", "Left" }),
                "The exit block only knows visitors that entered while it listened — the player is never seen leaving");
        }

        private static TaggedTriggerEvent PlayerTrigger(TriggerCrossing crossing)
        {
            var triggerEvent = new TaggedTriggerEvent();
            SetField(triggerEvent, "crossing", crossing);
            SetField(triggerEvent, "visitorTag", "Player");

            return triggerEvent;
        }


        // ── Run Scenario ─────────────────────────────────────────────────

        // У вызываемого сценария есть фоновая жизнь (своя When, долгий запуск). Run Scenario с «Wait Until Done»
        // должен дождаться вызванного запуска, а не всего сценария.
        [UnityTest, Category("QA_Found")]
        public IEnumerator RunScenarioNode_WaitsForCalledRun_NotForUnrelatedRunsOfTarget()
        {
            var calleeGraph = new ScenarioGraph();
            var called = calleeGraph.AddNode(new ScenarioWhenNode { CustomTitle = "Called" });
            called.AddEvent(new CalledEvent());
            ConnectToIn(calleeGraph, called, ScenarioWhenNode.Then, AddSteps(calleeGraph, "Callee", new ScenarioTestWaitFramesAction(2)));
            var ambient = AddWhen(calleeGraph, "Ambient");
            ConnectToIn(calleeGraph, ambient, ScenarioWhenNode.Then, AddSteps(calleeGraph, "Ambient Loop", new ScenarioTestWaitFramesAction(10000)));
            var callee = CreateScenario(calleeGraph, "Callee");
            RaiseWhen(callee, "Ambient");

            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var runScenario = graph.AddNode(new ScenarioRunScenarioNode());
            runScenario.TargetScenario.Value = callee;
            ConnectToIn(graph, when, ScenarioWhenNode.Then, runScenario);
            ConnectToIn(graph, runScenario, ScenarioRunScenarioNode.Done, AddSteps(graph, "Caller Done"));
            var caller = CreateScenario(graph, "Caller");

            RaiseWhen(caller);
            yield return WaitFrames(8);

            Assert.That(Marks, Is.EqualTo(new[] { "Ambient Loop", "Callee", "Caller Done" }),
                "Run Scenario waits while ANY run of the target goes, so it never gets to Done");
        }


        // ── Защита от петли между запусками ──────────────────────────────

        // Сценарий зовёт сам себя (Run Alongside) без ожидания. Каждый новый запуск — новый счётчик переходов,
        // и защита «больше 1000 за кадр» не срабатывает; без ограничителя в тесте это переполнение стека.
        [Test, Category("QA_Found")]
        public void SelfCallLoopWithoutWaiting_IsCaughtAsLoop()
        {
            const int Depth = 120;
            const int ChainLength = 10;

            var graph = new ScenarioGraph();
            var called = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Called", RerunRule = ScenarioRerunRule.RunAlongside });
            called.AddEvent(new CalledEvent());
            ScenarioNode previous = called;
            var previousOutput = ScenarioWhenNode.Then;

            for (var index = 0; index < ChainLength; index++)
            {
                var step = graph.AddNode(new ScenarioStepsNode());
                ConnectToIn(graph, previous, previousOutput, step);
                previous = step;
                previousOutput = ScenarioStepsNode.Done;
            }

            var branch = graph.AddNode(new ScenarioIfNode());
            branch.Conditions.Add(new ScenarioQaCountdownCondition(Depth));
            var callSelf = graph.AddNode(new ScenarioRunScenarioNode { CustomTitle = "Call Self", WaitsForEnd = false });
            ConnectToIn(graph, previous, previousOutput, branch);
            ConnectToIn(graph, branch, ScenarioIfNode.True, callSelf);
            ConnectToIn(graph, callSelf, ScenarioRunScenarioNode.Done, AddSteps(graph, "Returned"));
            var scenario = CreateScenario(graph);
            NodeOf<ScenarioRunScenarioNode>(scenario, "Call Self").TargetScenario.Value = scenario;

            LogAssert.Expect(LogType.Error, new Regex("starts scenarios without waiting"));

            scenario.TryRun();

            var returned = Marks.FindAll(mark => mark == "Returned").Count;
            var transitions = (Depth + 1) * (ChainLength + 1) + Depth * 2;

            Assert.That(ReportedRuns.Exists(record => record.Outcome == ScenarioRunOutcome.Failed), Is.True,
                $"{transitions} instant transitions in one frame, {Depth} nested runs, no loop error");
            Assert.That(returned, Is.LessThan(Depth));
        }


        // ── Not Met и повторное срабатывание ─────────────────────────────

        // Решение автора (а): запуск, ушедший в Not Met, — идущий запуск. Пока идёт подсказка, Skip If Running
        // пропускает новое срабатывание, даже если условия уже сошлись.
        [UnityTest, Category("QA_Question")]
        public IEnumerator NotMetRunWithWires_BlocksThen_UnderSkipIfRunning()
        {
            var condition = new ScenarioTestSwitchCondition(false);
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            when.Conditions.Add(condition);
            ConnectToIn(graph, when, ScenarioWhenNode.NotMet, AddSteps(graph, "Hint", new ScenarioTestWaitFramesAction(10)));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Scare"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            yield return WaitFrames(2);
            condition.IsOn = true;
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Hint" }), "The hint run is still going — the new event is skipped");

            yield return WaitFrames(12);
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Hint", "Scare" }));
        }

        // Not Met без проводов кончается в том же кадре и ничего не держит.
        [Test]
        public void NotMetWithoutWires_DoesNotBlockThen()
        {
            var condition = new ScenarioTestSwitchCondition(false);
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            when.Conditions.Add(condition);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Scare"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);

            Assert.That(scenario.IsRunning, Is.False);

            condition.IsOn = true;
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Scare" }));
        }


        // ── Утечки ───────────────────────────────────────────────────────

        // Ветка мгновенной ноды кончается внутри StartCoroutine — раньше, чем раннер запишет её в свой словарь.
        [UnityTest]
        public IEnumerator InstantRuns_DoNotLeaveEntriesInCoroutineRunner()
        {
            const int Runs = 200;

            var scenario = CreateScenario(ScenarioTestGraphs.FanOut());
            yield return null;
            var before = RunningCoroutineCount();

            for (var index = 0; index < Runs; index++)
                RaiseWhen(scenario);

            yield return WaitFrames(3);

            Assert.That(Marks.Count, Is.EqualTo(Runs * 2));
            Assert.That(RunningCoroutineCount() - before, Is.LessThan(5),
                "Finished coroutines left in CoroutineRunner.running — it grows with every instant node");
        }

        private static int RunningCoroutineCount()
        {
            var runner = AppContext.Current.Coroutines;
            var running = (IDictionary)runner.GetType().GetField("running", PrivateField).GetValue(runner);

            return running.Count;
        }


        // ── Запуск до Awake ──────────────────────────────────────────────

        // Объект сценария ещё ни разу не включали — графа у него нет, и TryRun не должен рапортовать «пошёл».
        [Test, Category("QA_Found")]
        public void TryRun_OnNeverActivatedGraphScenario_DoesNotPretendToRun()
        {
            var graph = new ScenarioGraph();
            var called = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Called" });
            called.AddEvent(new CalledEvent());
            ConnectToIn(graph, called, ScenarioWhenNode.Then, AddSteps(graph, "Called Steps"));
            var gameObject = CreateGameObject("Never Active");
            gameObject.SetActive(false);
            var scenario = gameObject.AddComponent<Scenario>();
            SetField(scenario, "graphSource", ScenarioGraphSource.Embedded);
            SetField(scenario, "graph", graph);

            var hasStarted = scenario.TryRun();

            Assert.That(Marks, Is.Empty);
            Assert.That(hasStarted, Is.False, "TryRun says the scenario started, though no node ran");
            Assert.That(ReportedRuns, Is.Empty, "An empty old-list run was reported as Finished");
        }


        // ── Выключение и уничтожение посреди ожидания ────────────────────

        [UnityTest]
        public IEnumerator DisableThenDestroy_DuringWaitFor_ReleasesEverything()
        {
            var effect = new ScenarioTestHeldEffect();
            var graph = ScenarioTestGraphs.SilhouetteWaitFor();
            ScenarioTestGraphs.FindNode<ScenarioStepsNode>(graph, ScenarioTestGraphs.SilhouetteOnTitle)
                .AddStep(new ScenarioTestHoldEffectAction(effect));
            var scenario = CreateScenario(graph);
            var whenEvent = NodeOf<ScenarioWhenNode>(scenario, ScenarioTestGraphs.WhenTitle).Events[0];
            var leftZone = NodeOf<ScenarioWaitForNode>(scenario, ScenarioTestGraphs.WaitLookTitle).Cases[1].AwaitedEvent;

            RaiseWhen(scenario);
            yield return null;
            scenario.gameObject.SetActive(false);

            Assert.That(whenEvent.IsAttached, Is.False, "When stops listening while the scenario is off");

            Object.Destroy(scenario.gameObject);
            yield return null;

            Assert.That(leftZone.IsAttached, Is.False);
            Assert.That(effect.ReleaseCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisableEnable_DuringRun_DoesNotDoubleSubscribe()
        {
            var scenario = CreateScenario(ScenarioTestGraphs.SilhouetteWaitFor());

            RaiseWhen(scenario);
            yield return null;
            scenario.enabled = false;
            scenario.enabled = true;
            scenario.enabled = false;
            scenario.enabled = true;

            Assert.DoesNotThrow(() => RaiseWhen(scenario), "Skip If Running: the event is just ignored");
            Assert.That(Marks, Is.EqualTo(new[] { ScenarioTestGraphs.SilhouetteOnTitle }));
        }


        // ── Копия объекта в игре ─────────────────────────────────────────

        // Instantiate сценария с граф-файлом: у копии своя копия графа и свой Only Once; оригинал живёт дальше.
        [UnityTest]
        public IEnumerator InstantiatedScenarioWithGraphAsset_HasOwnCopyAndOnlyOnce()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            when.RunsOnlyOnce = true;
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Steps"));
            var original = CreateScenario(CreateGraphAsset(graph), "Original");

            RaiseWhen(original);
            var clone = Object.Instantiate(original.gameObject).GetComponent<Scenario>();

            try
            {
                Assert.That(clone.Graph, Is.Not.SameAs(original.Graph));

                RaiseWhen(clone);
                RaiseWhen(original);

                Assert.That(Marks, Is.EqualTo(new[] { "Steps", "Steps" }));

                Object.Destroy(clone.gameObject);
                yield return null;

                Assert.That(original.Graph, Is.Not.Null);
                RaiseWhen(original);
                Assert.That(Marks.Count, Is.EqualTo(2));
            }
            finally
            {
                if (clone != null)
                    Object.DestroyImmediate(clone.gameObject);
            }
        }
    }
}
