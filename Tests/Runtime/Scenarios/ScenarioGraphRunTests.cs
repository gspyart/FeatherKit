using System.Collections;
using System.Text.RegularExpressions;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Запуск графа: маршрут по проводам, мгновенные ноды, повторное срабатывание When,
    /// остановка с долгими эффектами, защита от вечного цикла, журнал и старый режим списков.
    /// </summary>
    public class ScenarioGraphRunTests : ScenarioPlayModeTestBase
    {
        // ── Маршрут ──────────────────────────────────────────────────────

        [Test]
        public void ConditionsMet_GoesToThen_InTheSameFrame()
        {
            var scenario = CreateScenario(ScenarioTestGraphs.WhenThenSteps());

            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { ScenarioTestGraphs.LampOnTitle }));
            Assert.That(scenario.IsRunning, Is.False);
            Assert.That(ReportedRuns.Count, Is.EqualTo(1));
            Assert.That(ReportedRuns[0].Outcome, Is.EqualTo(ScenarioRunOutcome.Finished));
            Assert.That(ReportedRuns[0].StartNodeId, Is.EqualTo(NodeOf<ScenarioWhenNode>(scenario, ScenarioTestGraphs.WhenTitle).Id));
        }

        [Test]
        public void ConditionsNotMet_GoesToNotMet_AndReportsIt()
        {
            var scenario = CreateScenario(ScenarioTestGraphs.WhenThenSteps());
            var whenNode = NodeOf<ScenarioWhenNode>(scenario, ScenarioTestGraphs.WhenTitle);
            whenNode.Conditions.Add(new ScenarioTestSwitchCondition(false));

            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { ScenarioTestGraphs.NotMetTitle }));
            Assert.That(ReportedRuns.Count, Is.EqualTo(1));
            Assert.That(ReportedRuns[0].Outcome, Is.EqualTo(ScenarioRunOutcome.ConditionsNotMet));
        }

        [Test]
        public void OneOutput_TwoWires_BothRun()
        {
            var scenario = CreateScenario(ScenarioTestGraphs.FanOut());

            RaiseWhen(scenario);

            Assert.That(Marks, Is.EquivalentTo(new[] { ScenarioTestGraphs.LeftTitle, ScenarioTestGraphs.RightTitle }));
        }

        [Test]
        public void CustomNode_FiresItsPrivateOutput()
        {
            var scenario = CreateScenario(ScenarioTestGraphs.CoinFlip(headsChance: 1f));

            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { ScenarioTestGraphs.HeadsTitle }));
        }

        [Test]
        public void BranchNode_GoesByConditions()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var branch = graph.AddNode(new ScenarioIfNode());
            var condition = new ScenarioTestSwitchCondition(true);
            branch.Conditions.Add(condition);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, branch);
            ConnectToIn(graph, branch, ScenarioIfNode.True, AddSteps(graph, "True"));
            ConnectToIn(graph, branch, ScenarioIfNode.False, AddSteps(graph, "False"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            condition.IsOn = false;
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "True", "False" }));
        }

        [Test]
        public void WireFired_ReportsNodeAndOutput()
        {
            var scenario = CreateScenario(ScenarioTestGraphs.WhenThenSteps());
            var whenNode = NodeOf<ScenarioWhenNode>(scenario, ScenarioTestGraphs.WhenTitle);
            var lampOn = NodeOf<ScenarioStepsNode>(scenario, ScenarioTestGraphs.LampOnTitle);
            var firedWires = new System.Collections.Generic.List<string>();
            scenario.WireFired += (nodeId, portId) => firedWires.Add($"{nodeId}:{portId}");

            RaiseWhen(scenario);

            Assert.That(firedWires, Is.EqualTo(new[] { $"{whenNode.Id}:then", $"{lampOn.Id}:done" }));
        }


        // ── Долгие ноды и подсветка ──────────────────────────────────────

        [UnityTest]
        public IEnumerator LongNode_IsPerforming_UntilItsStepsEnd()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var slow = AddSteps(graph, "Slow", new ScenarioTestWaitFramesAction(3));
            var after = AddSteps(graph, "After");
            ConnectToIn(graph, when, ScenarioWhenNode.Then, slow);
            ConnectToIn(graph, slow, ScenarioStepsNode.Done, after);
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);

            Assert.That(scenario.IsRunning, Is.True);
            Assert.That(scenario.IsPerforming(slow.Id), Is.True);
            Assert.That(scenario.IsPerforming(when.Id), Is.False);

            yield return WaitFrames(5);

            Assert.That(Marks, Is.EqualTo(new[] { "Slow", "After" }));
            Assert.That(scenario.IsPerforming(slow.Id), Is.False);
            Assert.That(scenario.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator BackgroundStep_DoesNotHoldDone_ButKeepsRunGoing()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var steps = AddSteps(graph, "Steps", new ScenarioTestBackgroundRecordAction(3, "Background End"));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, steps);
            ConnectToIn(graph, steps, ScenarioStepsNode.Done, AddSteps(graph, "Done"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Steps", "Done" }));
            Assert.That(scenario.IsRunning, Is.True, "A background step keeps the run going");
            Assert.That(scenario.IsPerforming(steps.Id), Is.True, "A background step highlights its node");

            yield return WaitFrames(5);

            Assert.That(Marks, Is.EqualTo(new[] { "Steps", "Done", "Background End" }));
            Assert.That(scenario.IsRunning, Is.False);
        }


        // ── Повторное срабатывание ───────────────────────────────────────

        [UnityTest]
        public IEnumerator SkipIfRunning_IgnoresSecondEvent()
        {
            var scenario = CreateScenario(SlowGraph(ScenarioRerunRule.SkipIfRunning));

            RaiseWhen(scenario);
            RaiseWhen(scenario);
            yield return WaitFrames(5);

            Assert.That(Marks, Is.EqualTo(new[] { "Slow", "End" }));
        }

        [UnityTest]
        public IEnumerator SkipIfRunning_DoesNotCheckConditions_WhileRunGoes()
        {
            var graph = SlowGraph(ScenarioRerunRule.SkipIfRunning);
            var condition = new ScenarioTestCountingCondition("counted");
            ScenarioTestGraphs.FindNode<ScenarioWhenNode>(graph, ScenarioTestGraphs.WhenTitle).Conditions.Add(condition);
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            RaiseWhen(scenario);
            RaiseWhen(scenario);

            Assert.That(condition.CheckCount, Is.EqualTo(1));

            yield return WaitFrames(5);
            RaiseWhen(scenario);

            Assert.That(condition.CheckCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Restart_StopsPreviousRun_AndStartsNew()
        {
            var scenario = CreateScenario(SlowGraph(ScenarioRerunRule.Restart));

            RaiseWhen(scenario);
            yield return null;
            RaiseWhen(scenario);
            yield return WaitFrames(5);

            Assert.That(Marks, Is.EqualTo(new[] { "Slow", "Slow", "End" }));
            Assert.That(ReportedRuns.ConvertAll(record => record.Outcome),
                Is.EqualTo(new[] { ScenarioRunOutcome.Stopped, ScenarioRunOutcome.Finished }));
        }

        [UnityTest]
        public IEnumerator RunAlongside_BothRunsGoToTheEnd()
        {
            var scenario = CreateScenario(SlowGraph(ScenarioRerunRule.RunAlongside));

            RaiseWhen(scenario);
            yield return null;
            RaiseWhen(scenario);
            yield return WaitFrames(5);

            Assert.That(Marks, Is.EqualTo(new[] { "Slow", "Slow", "End", "End" }));
        }

        [UnityTest]
        public IEnumerator OnlyOnce_IgnoresEventsAfterFirstRun()
        {
            var graph = SlowGraph(ScenarioRerunRule.RunAlongside);
            ScenarioTestGraphs.FindNode<ScenarioWhenNode>(graph, ScenarioTestGraphs.WhenTitle).RunsOnlyOnce = true;
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            yield return WaitFrames(5);
            RaiseWhen(scenario);
            yield return WaitFrames(5);

            Assert.That(Marks, Is.EqualTo(new[] { "Slow", "End" }));
            Assert.That(scenario.HasRun, Is.True);
        }

        [Test]
        public void OnlyOnce_IsNotSpent_WhenConditionsAreNotMet()
        {
            var graph = ScenarioTestGraphs.WhenThenSteps();
            var whenNode = ScenarioTestGraphs.FindNode<ScenarioWhenNode>(graph, ScenarioTestGraphs.WhenTitle);
            var condition = new ScenarioTestSwitchCondition(false);
            whenNode.Conditions.Add(condition);
            whenNode.RunsOnlyOnce = true;
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            condition.IsOn = true;
            RaiseWhen(scenario);
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { ScenarioTestGraphs.NotMetTitle, ScenarioTestGraphs.LampOnTitle }));
        }

        [UnityTest]
        public IEnumerator Restart_WithConditionsNotMet_KeepsPreviousRun()
        {
            var graph = SlowGraph(ScenarioRerunRule.Restart);
            var condition = new ScenarioTestSwitchCondition(true);
            ScenarioTestGraphs.FindNode<ScenarioWhenNode>(graph, ScenarioTestGraphs.WhenTitle).Conditions.Add(condition);
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            yield return null;
            condition.IsOn = false;
            RaiseWhen(scenario);
            yield return WaitFrames(4);

            Assert.That(Marks, Is.EqualTo(new[] { "Slow", "End" }), "A failed attempt does not cut the run that goes");
            Assert.That(ReportedRuns.ConvertAll(record => record.Outcome),
                Is.EqualTo(new[] { ScenarioRunOutcome.ConditionsNotMet, ScenarioRunOutcome.Finished }));
        }

        // When → «Slow» (2 кадра) → «End».
        private static ScenarioGraph SlowGraph(ScenarioRerunRule rerunRule)
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            when.RerunRule = rerunRule;
            var slow = AddSteps(graph, "Slow", new ScenarioTestWaitFramesAction(2));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, slow);
            ConnectToIn(graph, slow, ScenarioStepsNode.Done, AddSteps(graph, "End"));

            return graph;
        }


        // ── Остановка и долгие эффекты ───────────────────────────────────

        [UnityTest]
        public IEnumerator Stop_CutsRun_AndReleasesHeldEffects()
        {
            var effect = new ScenarioTestHeldEffect();
            var scenario = CreateScenario(HoldingGraph(effect));

            RaiseWhen(scenario);
            yield return null;
            scenario.Stop();
            yield return WaitFrames(5);

            Assert.That(effect.ReleaseCount, Is.EqualTo(1));
            Assert.That(Marks, Is.EqualTo(new[] { "Hold" }));
            Assert.That(scenario.IsRunning, Is.False);
            Assert.That(ReportedRuns[0].Outcome, Is.EqualTo(ScenarioRunOutcome.Stopped));
        }

        [UnityTest]
        public IEnumerator DestroyingScenario_ReleasesHeldEffects()
        {
            var effect = new ScenarioTestHeldEffect();
            var scenario = CreateScenario(HoldingGraph(effect));

            RaiseWhen(scenario);
            yield return null;
            Object.DestroyImmediate(scenario.gameObject);

            Assert.That(effect.ReleaseCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FinishedRun_KeepsHeldEffect_UntilScenarioStops()
        {
            var effect = new ScenarioTestHeldEffect();
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Hold", new ScenarioTestHoldEffectAction(effect)));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            yield return null;

            Assert.That(scenario.IsRunning, Is.False);
            Assert.That(effect.IsReleased, Is.False);

            scenario.Stop();

            Assert.That(effect.ReleaseCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator StopThisRunNode_StopsOwnRunOnly_AndReleasesItsEffects()
        {
            var effect = new ScenarioTestHeldEffect();
            var graph = HoldingGraph(effect);
            var when = ScenarioTestGraphs.FindNode<ScenarioWhenNode>(graph, ScenarioTestGraphs.WhenTitle);
            var stopWaiter = graph.AddNode(new ScenarioWaitForNode());
            var stopEvent = new ScenarioTestManualEvent("stop");
            var stopCase = stopWaiter.AddCase(new ScenarioWaitCase(stopEvent));
            var stopRun = graph.AddNode(new ScenarioStopRunNode());
            ConnectToIn(graph, when, ScenarioWhenNode.Then, stopWaiter);
            ConnectToIn(graph, stopWaiter, stopCase.Output, stopRun);
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            yield return null;
            stopEvent.Raise();
            yield return WaitFrames(6);

            Assert.That(effect.ReleaseCount, Is.EqualTo(1));
            Assert.That(Marks, Is.EqualTo(new[] { "Hold" }), "The slow node after the hold was cut off");
            Assert.That(ReportedRuns[0].Outcome, Is.EqualTo(ScenarioRunOutcome.Stopped));
            Assert.That(stopEvent.IsAttached, Is.False);
        }

        [UnityTest]
        public IEnumerator Stop_DuringWaitFor_EventNoLongerArrives_AndNextRunWaitsAgain()
        {
            var scenario = CreateScenario(ScenarioTestGraphs.SilhouetteWaitFor());
            var waitLook = NodeOf<ScenarioWaitForNode>(scenario, ScenarioTestGraphs.WaitLookTitle);
            var leftZone = (ScenarioTestManualEvent)waitLook.Cases[1].AwaitedEvent;

            RaiseWhen(scenario);
            yield return null;
            scenario.Stop();
            leftZone.Raise();
            yield return WaitFrames(2);

            Assert.That(leftZone.IsAttached, Is.False);
            Assert.That(Marks, Is.EqualTo(new[] { ScenarioTestGraphs.SilhouetteOnTitle }));

            RaiseWhen(scenario);
            ((ScenarioTestSwitchCondition)waitLook.Cases[0].Condition).IsOn = true;
            yield return WaitFrames(2);

            Assert.That(Marks, Is.EqualTo(new[]
            {
                ScenarioTestGraphs.SilhouetteOnTitle, ScenarioTestGraphs.SilhouetteOnTitle, ScenarioTestGraphs.SilhouetteOffTitle
            }));
        }

        // When → «Hold»: берёт эффект и ждёт 4 кадра → «Late».
        private static ScenarioGraph HoldingGraph(ScenarioTestHeldEffect effect)
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var hold = AddSteps(graph, "Hold", new ScenarioTestHoldEffectAction(effect), new ScenarioTestWaitFramesAction(4));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, hold);
            ConnectToIn(graph, hold, ScenarioStepsNode.Done, AddSteps(graph, "Late"));

            return graph;
        }


        // ── Ошибки ───────────────────────────────────────────────────────

        [Test]
        public void LoopWithoutWaiting_FailsRun_WithError()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var first = AddSteps(graph, "First");
            var second = AddSteps(graph, "Second");
            ConnectToIn(graph, when, ScenarioWhenNode.Then, first);
            ConnectToIn(graph, first, ScenarioStepsNode.Done, second);
            ConnectToIn(graph, second, ScenarioStepsNode.Done, first);
            var scenario = CreateScenario(graph);

            LogAssert.Expect(LogType.Error, new Regex("failed at node .*Loop without waiting"));
            LogAssert.Expect(LogType.Exception, new Regex("Loop without waiting"));

            RaiseWhen(scenario);

            Assert.That(Marks.Count, Is.InRange(9990, 10001));
            Assert.That(scenario.IsRunning, Is.False);
            Assert.That(ReportedRuns[0].Outcome, Is.EqualTo(ScenarioRunOutcome.Failed));
        }

        [Test]
        public void FailedStep_FailsRun_AndReportsItsNode()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var broken = AddSteps(graph, "Broken", new ScenarioTestFailAction("boom"));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, broken);
            ConnectToIn(graph, broken, ScenarioStepsNode.Done, AddSteps(graph, "Never"));
            var scenario = CreateScenario(graph);

            LogAssert.Expect(LogType.Error, new Regex("failed at node “Broken” \\(ScenarioTestFailAction\\)"));
            LogAssert.Expect(LogType.Exception, new Regex(ScenarioTestFailAction.Message));

            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Broken" }));
            Assert.That(ReportedRuns[0].Outcome, Is.EqualTo(ScenarioRunOutcome.Failed));
            Assert.That(ReportedRuns[0].FailedNodeId, Is.EqualTo(broken.Id));
        }


        // ── Триггеры: уход без OnTriggerExit ─────────────────────────────

        [UnityTest]
        public IEnumerator TriggerGuest_ColliderDisabled_GetsExit_AndEntersAgainLater()
        {
            var scenario = CreateScenario(EnterExitGraph());
            var playerCollider = CreatePlayerCollider();

            scenario.SendMessage("OnTriggerEnter", playerCollider);
            playerCollider.enabled = false;
            yield return WaitFixedUpdates(2);

            Assert.That(Marks, Is.EqualTo(new[] { "Entered", "Exited" }));

            playerCollider.enabled = true;
            yield return WaitFixedUpdates(2);
            scenario.SendMessage("OnTriggerEnter", playerCollider);

            Assert.That(Marks, Is.EqualTo(new[] { "Entered", "Exited", "Entered" }));
        }

        [UnityTest]
        public IEnumerator TriggerGuest_TeleportedFarAway_GetsExit()
        {
            var scenario = CreateScenario(EnterExitGraph());
            var zone = scenario.gameObject.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            var playerCollider = CreatePlayerCollider();

            yield return WaitFixedUpdates(1);
            scenario.SendMessage("OnTriggerEnter", playerCollider);
            yield return WaitFixedUpdates(2);

            Assert.That(Marks, Is.EqualTo(new[] { "Entered" }), "Still inside the zone — no exit");

            playerCollider.enabled = false;
            playerCollider.transform.position = new Vector3(100f, 0f, 0f);
            playerCollider.enabled = true;
            yield return WaitFixedUpdates(3);

            Assert.That(Marks, Is.EqualTo(new[] { "Entered", "Exited" }));
        }

        // When «вошёл» → «Entered», When «вышел» → «Exited». Без коллайдера-триггера на сценарии проверка «далеко» молчит.
        private ScenarioGraph EnterExitGraph()
        {
            var graph = new ScenarioGraph();
            var entered = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Enter When" });
            entered.AddEvent(TriggerEventFor(TriggerCrossing.Entered));
            var exited = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Exit When", RerunRule = ScenarioRerunRule.RunAlongside });
            exited.AddEvent(TriggerEventFor(TriggerCrossing.Exited));
            ConnectToIn(graph, entered, ScenarioWhenNode.Then, AddSteps(graph, "Entered"));
            ConnectToIn(graph, exited, ScenarioWhenNode.Then, AddSteps(graph, "Exited"));

            return graph;
        }

        private static TaggedTriggerEvent TriggerEventFor(TriggerCrossing crossing)
        {
            var triggerEvent = new TaggedTriggerEvent();
            SetField(triggerEvent, "crossing", crossing);

            return triggerEvent;
        }

        private BoxCollider CreatePlayerCollider()
        {
            return CreateGameObject("Player").AddComponent<BoxCollider>();
        }

        private static IEnumerator WaitFixedUpdates(int count)
        {
            for (var index = 0; index < count; index++)
                yield return new WaitForFixedUpdate();
        }


        // ── Вызов и старый режим ─────────────────────────────────────────

        [Test]
        public void TryRun_FiresWhenNodesWithCalledEvent()
        {
            var graph = new ScenarioGraph();
            var called = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Called" });
            called.AddEvent(new CalledEvent());
            ConnectToIn(graph, called, ScenarioWhenNode.Then, AddSteps(graph, "Called Steps"));
            ConnectToIn(graph, AddWhen(graph), ScenarioWhenNode.Then, AddSteps(graph, "Other Steps"));
            var scenario = CreateScenario(graph);

            Assert.That(scenario.TryRun(), Is.True);
            Assert.That(Marks, Is.EqualTo(new[] { "Called Steps" }));
        }

        [Test]
        public void TryRun_WithoutCalledEvent_ReturnsFalse()
        {
            var scenario = CreateScenario(ScenarioTestGraphs.WhenThenSteps());

            Assert.That(scenario.TryRun(), Is.False);
            Assert.That(Marks, Is.Empty);
        }

        [UnityTest]
        public IEnumerator RunScenarioNode_WaitsForCalledScenario()
        {
            var calledGraph = new ScenarioGraph();
            var called = calledGraph.AddNode(new ScenarioWhenNode { CustomTitle = "Called" });
            called.AddEvent(new CalledEvent());
            ConnectToIn(calledGraph, called, ScenarioWhenNode.Then, AddSteps(calledGraph, "Callee", new ScenarioTestWaitFramesAction(3)));
            var callee = CreateScenario(calledGraph, "Callee");

            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var runScenario = graph.AddNode(new ScenarioRunScenarioNode());
            runScenario.TargetScenario.Value = callee;
            ConnectToIn(graph, when, ScenarioWhenNode.Then, runScenario);
            ConnectToIn(graph, runScenario, ScenarioRunScenarioNode.Done, AddSteps(graph, "Caller Done"));
            var caller = CreateScenario(graph, "Caller");
            var instigator = CreateGameObject("Player");

            RaiseWhen(caller, instigator: instigator);

            Assert.That(Marks, Is.EqualTo(new[] { "Callee" }));

            yield return WaitFrames(5);

            Assert.That(Marks, Is.EqualTo(new[] { "Callee", "Caller Done" }));
            Assert.That(ReportedRuns.Find(record => record.Scenario == callee).InstigatorName, Is.EqualTo("Player"));
        }

        [Test]
        public void StopScenarioNode_StopsTargetScenario()
        {
            var effect = new ScenarioTestHeldEffect();
            var target = CreateScenario(HoldingGraph(effect), "Target");
            RaiseWhen(target);

            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var stopScenario = graph.AddNode(new ScenarioStopScenarioNode());
            stopScenario.TargetScenario.Value = target;
            ConnectToIn(graph, when, ScenarioWhenNode.Then, stopScenario);
            ConnectToIn(graph, stopScenario, ScenarioStopScenarioNode.Done, AddSteps(graph, "Stopped"));
            var stopper = CreateScenario(graph, "Stopper");

            RaiseWhen(stopper);

            Assert.That(target.IsRunning, Is.False);
            Assert.That(effect.ReleaseCount, Is.EqualTo(1));
            Assert.That(Marks, Is.EqualTo(new[] { "Hold", "Stopped" }));
        }

        [Test]
        public void TryRun_OnDisabledScenario_WarnsAndDoesNotRun()
        {
            var graph = new ScenarioGraph();
            var called = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Called" });
            called.AddEvent(new CalledEvent());
            ConnectToIn(graph, called, ScenarioWhenNode.Then, AddSteps(graph, "Called Steps"));
            var scenario = CreateScenario(graph);
            var listScenario = CreateListScenario(new ScenarioTestRecordAction("List Step"));
            scenario.enabled = false;
            listScenario.enabled = false;

            LogAssert.Expect(LogType.Warning, new Regex("is disabled — not called"));
            LogAssert.Expect(LogType.Warning, new Regex("is disabled — not called"));

            Assert.That(scenario.TryRun(), Is.False);
            Assert.That(listScenario.TryRun(), Is.False);
            Assert.That(Marks, Is.Empty);
        }

        [UnityTest]
        public IEnumerator EmptyGraph_KeepsOldListMode()
        {
            var scenario = CreateListScenario(new ScenarioTestRecordAction("List Step"), new ScenarioTestWaitFramesAction(2),
                new ScenarioTestRecordAction("List End"));

            Assert.That(scenario.TryRun(), Is.True);
            Assert.That(scenario.IsRunning, Is.True);

            yield return WaitFrames(4);

            Assert.That(Marks, Is.EqualTo(new[] { "List Step", "List End" }));
            Assert.That(scenario.IsRunning, Is.False);
            Assert.That(ReportedRuns[0].StartNodeId, Is.EqualTo(-1));
        }
    }
}
