using System.Collections;
using System.Text.RegularExpressions;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>Ноды потока: Sequence, Parallel, Repeat, Wait For, Guard While — ожидание, обрыв по происхождению, выходы.</summary>
    public class ScenarioFlowNodeTests : ScenarioPlayModeTestBase
    {
        // ── Sequence ─────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Sequence_WaitsForEverythingGrown_IncludingBackgroundSteps()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var sequence = graph.AddNode(new ScenarioSequenceNode());
            ConnectToIn(graph, when, ScenarioWhenNode.Then, sequence);
            ConnectToIn(graph, sequence, ScenarioSequenceNode.ThenOutput(1),
                AddSteps(graph, "First", new ScenarioTestBackgroundRecordAction(3, "First Background")));
            ConnectToIn(graph, sequence, ScenarioSequenceNode.ThenOutput(2), AddSteps(graph, "Second"));
            ConnectToIn(graph, sequence, ScenarioSequenceNode.Done, AddSteps(graph, "Sequence Done"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "First" }));

            yield return WaitFrames(6);

            Assert.That(Marks, Is.EqualTo(new[] { "First", "First Background", "Second", "Sequence Done" }));
        }

        [Test]
        public void Sequence_OfInstantNodes_EndsInTheSameFrame()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var sequence = graph.AddNode(new ScenarioSequenceNode { ThenCount = 3 });
            ConnectToIn(graph, when, ScenarioWhenNode.Then, sequence);
            ConnectToIn(graph, sequence, ScenarioSequenceNode.ThenOutput(1), AddSteps(graph, "1"));
            ConnectToIn(graph, sequence, ScenarioSequenceNode.ThenOutput(3), AddSteps(graph, "3"));
            ConnectToIn(graph, sequence, ScenarioSequenceNode.Done, AddSteps(graph, "Done"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "1", "3", "Done" }));
            Assert.That(scenario.IsRunning, Is.False);
        }


        // ── Parallel ─────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator ParallelAllDone_WaitsForSlowestBranch()
        {
            var scenario = CreateScenario(ParallelGraph(ScenarioParallelCompletion.AllDone, new ScenarioTestHeldEffect()));

            RaiseWhen(scenario);
            yield return WaitFrames(3);

            Assert.That(Marks, Does.Not.Contain("Parallel Done"));

            yield return WaitFrames(5);

            Assert.That(Marks, Is.EqualTo(new[] { "Fast", "Slow", "Slow End", "Parallel Done" }));
        }

        [UnityTest]
        public IEnumerator ParallelFirstDone_CutsTheRest_AndReleasesTheirEffects()
        {
            var effect = new ScenarioTestHeldEffect();
            var scenario = CreateScenario(ParallelGraph(ScenarioParallelCompletion.FirstDone, effect));

            RaiseWhen(scenario);
            yield return WaitFrames(8);

            Assert.That(Marks, Is.EqualTo(new[] { "Fast", "Slow", "Parallel Done" }));
            Assert.That(effect.ReleaseCount, Is.EqualTo(1));
            Assert.That(scenario.IsRunning, Is.False);
        }

        // Branch 1 → «Fast» (1 кадр); Branch 2 → «Slow» (эффект, 4 кадра) → «Slow End»; Done → «Parallel Done».
        private static ScenarioGraph ParallelGraph(ScenarioParallelCompletion completion, ScenarioTestHeldEffect effect)
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var parallel = graph.AddNode(new ScenarioParallelNode { Completion = completion });
            var slow = AddSteps(graph, "Slow", new ScenarioTestHoldEffectAction(effect), new ScenarioTestWaitFramesAction(4));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, parallel);
            ConnectToIn(graph, parallel, ScenarioParallelNode.BranchOutput(1), AddSteps(graph, "Fast", new ScenarioTestWaitFramesAction(1)));
            ConnectToIn(graph, parallel, ScenarioParallelNode.BranchOutput(2), slow);
            ConnectToIn(graph, slow, ScenarioStepsNode.Done, AddSteps(graph, "Slow End"));
            ConnectToIn(graph, parallel, ScenarioParallelNode.Done, AddSteps(graph, "Parallel Done"));

            return graph;
        }


        // ── Repeat ───────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator RepeatTimes_WaitsEachRound_ThenFinished()
        {
            var graph = RepeatGraph(new ScenarioRepeatNode { Mode = ScenarioRepeatMode.Times, RepeatCount = 3 }, tickFrames: 1);
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Tick" }));

            yield return WaitFrames(6);

            Assert.That(Marks, Is.EqualTo(new[] { "Tick", "Tick", "Tick", "Finished" }));
        }

        [UnityTest]
        public IEnumerator RepeatWhile_StopsWhenConditionsFail()
        {
            var repeat = new ScenarioRepeatNode { Mode = ScenarioRepeatMode.While };
            var condition = new ScenarioTestSwitchCondition(true);
            repeat.Conditions.Add(condition);
            var scenario = CreateScenario(RepeatGraph(repeat, tickFrames: 1));

            RaiseWhen(scenario);
            yield return WaitFrames(3);
            condition.IsOn = false;
            var ticks = Marks.Count;
            yield return WaitFrames(4);

            Assert.That(Marks[Marks.Count - 1], Is.EqualTo("Finished"));
            Assert.That(Marks.Count, Is.LessThanOrEqualTo(ticks + 2));
        }

        [UnityTest]
        public IEnumerator RepeatForever_StopInput_CutsRoundAndGoesFinished()
        {
            var effect = new ScenarioTestHeldEffect();
            var repeat = new ScenarioRepeatNode { Mode = ScenarioRepeatMode.Forever };
            var graph = RepeatGraph(repeat, tickFrames: 3, effect);
            var stopWhen = AddWhen(graph, "Stop When");
            graph.Connect(stopWhen, ScenarioWhenNode.Then, repeat, ScenarioRepeatNode.Stop);
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            yield return WaitFrames(7);
            RaiseWhen(scenario, "Stop When");
            yield return WaitFrames(2);

            var marksAfterStop = Marks.Count;
            yield return WaitFrames(8);

            Assert.That(Marks[Marks.Count - 1], Is.EqualTo("Finished"));
            Assert.That(Marks.Count, Is.EqualTo(marksAfterStop));
            Assert.That(effect.ReleaseCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(scenario.IsRunning, Is.False);
        }

        [Test]
        public void RepeatForever_WithoutWaiting_FailsAsLoop()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var repeat = graph.AddNode(new ScenarioRepeatNode { Mode = ScenarioRepeatMode.Forever });
            ConnectToIn(graph, when, ScenarioWhenNode.Then, repeat);
            var scenario = CreateScenario(graph);

            LogAssert.Expect(LogType.Error, new Regex("Loop without waiting"));
            LogAssert.Expect(LogType.Exception, new Regex("Loop without waiting"));

            RaiseWhen(scenario);

            Assert.That(scenario.IsRunning, Is.False);
        }

        // When → Repeat: Each Time → «Tick» (кадры, эффект); Finished → «Finished».
        private static ScenarioGraph RepeatGraph(ScenarioRepeatNode repeat, int tickFrames, ScenarioTestHeldEffect effect = null)
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            graph.AddNode(repeat);

            var tick = effect != null
                ? AddSteps(graph, "Tick", new ScenarioTestHoldEffectAction(effect), new ScenarioTestWaitFramesAction(tickFrames))
                : AddSteps(graph, "Tick", new ScenarioTestWaitFramesAction(tickFrames));

            ConnectToIn(graph, when, ScenarioWhenNode.Then, repeat);
            ConnectToIn(graph, repeat, ScenarioRepeatNode.EachTime, tick);
            ConnectToIn(graph, repeat, ScenarioRepeatNode.Finished, AddSteps(graph, "Finished"));

            return graph;
        }


        // ── Wait For ─────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator WaitFor_ConditionCase_GoesToItsOutput_AndReleasesEvents()
        {
            var scenario = CreateScenario(ScenarioTestGraphs.SilhouetteWaitFor());
            var waitLook = NodeOf<ScenarioWaitForNode>(scenario, ScenarioTestGraphs.WaitLookTitle);
            var leftZone = WaitCaseEvent(waitLook, 1);

            Assert.That(leftZone.IsAttached, Is.False, "Case events are not listened to before the wait");

            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { ScenarioTestGraphs.SilhouetteOnTitle }));
            Assert.That(leftZone.IsAttached, Is.True);
            Assert.That(scenario.IsPerforming(waitLook.Id), Is.True);

            WaitCaseCondition(waitLook, 0).IsOn = true;
            yield return null;

            Assert.That(Marks, Is.EqualTo(new[] { ScenarioTestGraphs.SilhouetteOnTitle, ScenarioTestGraphs.SilhouetteOffTitle }));
            Assert.That(leftZone.IsAttached, Is.False, "Case events are released after the wait");
        }

        [UnityTest]
        public IEnumerator WaitFor_EventCase_WireBack_WaitsAgain()
        {
            var scenario = CreateScenario(ScenarioTestGraphs.SilhouetteWaitFor());
            var waitLook = NodeOf<ScenarioWaitForNode>(scenario, ScenarioTestGraphs.WaitLookTitle);
            var leftZone = WaitCaseEvent(waitLook, 1);

            RaiseWhen(scenario);
            yield return null;
            leftZone.Raise();
            yield return WaitFrames(2);

            Assert.That(Marks, Is.EqualTo(new[] { ScenarioTestGraphs.SilhouetteOnTitle, ScenarioTestGraphs.SilhouetteOnTitle }));
            Assert.That(leftZone.IsAttached, Is.True, "Back on the Wait For node, its events are listened to again");

            WaitCaseCondition(waitLook, 0).IsOn = true;
            yield return WaitFrames(2);

            Assert.That(Marks[Marks.Count - 1], Is.EqualTo(ScenarioTestGraphs.SilhouetteOffTitle));
        }

        [UnityTest]
        public IEnumerator WaitFor_NothingHappens_TimesOut()
        {
            var scenario = CreateScenario(ScenarioTestGraphs.SilhouetteWaitFor());

            RaiseWhen(scenario);
            yield return new WaitForSeconds(1.3f);

            Assert.That(Marks, Is.EqualTo(new[] { ScenarioTestGraphs.SilhouetteOnTitle, ScenarioTestGraphs.TimedOutTitle }));
        }

        [Test]
        public void WaitFor_TwoCasesAtOnce_UpperOneWins_InTheSameFrame()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var waitFor = graph.AddNode(new ScenarioWaitForNode());
            var upper = waitFor.AddCase(new ScenarioWaitCase(new ScenarioTestSwitchCondition(true)));
            var lower = waitFor.AddCase(new ScenarioWaitCase(new ScenarioTestSwitchCondition(true)));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, waitFor);
            ConnectToIn(graph, waitFor, upper.Output, AddSteps(graph, "Upper"));
            ConnectToIn(graph, waitFor, lower.Output, AddSteps(graph, "Lower"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Upper" }));
        }

        [UnityTest]
        public IEnumerator WaitFor_RunAlongside_SharesEvents_UntilLastWaitEnds()
        {
            var graph = ScenarioTestGraphs.SilhouetteWaitFor();
            ScenarioTestGraphs.FindNode<ScenarioWhenNode>(graph, ScenarioTestGraphs.WhenTitle).RerunRule = ScenarioRerunRule.RunAlongside;
            var scenario = CreateScenario(graph);
            var waitLook = NodeOf<ScenarioWaitForNode>(scenario, ScenarioTestGraphs.WaitLookTitle);
            var leftZone = WaitCaseEvent(waitLook, 1);

            RaiseWhen(scenario);
            yield return null;
            RaiseWhen(scenario);

            Assert.That(leftZone.IsAttached, Is.True);

            WaitCaseCondition(waitLook, 0).IsOn = true;
            yield return WaitFrames(2);

            Assert.That(Marks.FindAll(mark => mark == ScenarioTestGraphs.SilhouetteOffTitle).Count, Is.EqualTo(2));
            Assert.That(leftZone.IsAttached, Is.False);
        }

        [UnityTest]
        public IEnumerator WaitFor_CutOffByStop_ReleasesEvents()
        {
            var scenario = CreateScenario(ScenarioTestGraphs.SilhouetteWaitFor());
            var leftZone = WaitCaseEvent(NodeOf<ScenarioWaitForNode>(scenario, ScenarioTestGraphs.WaitLookTitle), 1);

            RaiseWhen(scenario);
            yield return null;
            scenario.Stop();

            Assert.That(leftZone.IsAttached, Is.False);
        }

        private static ScenarioTestManualEvent WaitCaseEvent(ScenarioWaitForNode waitFor, int caseIndex)
        {
            return (ScenarioTestManualEvent)waitFor.Cases[caseIndex].AwaitedEvent;
        }

        private static ScenarioTestSwitchCondition WaitCaseCondition(ScenarioWaitForNode waitFor, int caseIndex)
        {
            return (ScenarioTestSwitchCondition)waitFor.Cases[caseIndex].Condition;
        }


        // ── Guard While ──────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Guard_BodyEndsOnItsOwn_GoesToDone()
        {
            var scenario = CreateScenario(ScenarioTestGraphs.GuardWhile());

            RaiseWhen(scenario);
            yield return new WaitForSeconds(1.3f);

            Assert.That(Marks, Is.EqualTo(new[] { ScenarioTestGraphs.BodyTitle, ScenarioTestGraphs.GuardDoneTitle }));
        }

        [Test]
        public void Guard_ConditionFalseOnEntry_GoesToBroken()
        {
            var scenario = CreateScenario(ScenarioTestGraphs.GuardWhile());
            ((ScenarioTestSwitchCondition)FirstGuardCondition(scenario)).IsOn = false;

            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { ScenarioTestGraphs.BrokenTitle }));
        }

        [UnityTest]
        public IEnumerator Guard_ConditionBreaks_CutsBodyWithBackground_AndReleasesItsEffects()
        {
            var effect = new ScenarioTestHeldEffect();
            var outsideEffect = new ScenarioTestHeldEffect();
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var guard = graph.AddNode(new ScenarioGuardNode());
            var condition = new ScenarioTestSwitchCondition(true);
            guard.Conditions.Add(condition);
            var body = AddSteps(graph, "Body", new ScenarioTestHoldEffectAction(effect),
                new ScenarioTestBackgroundRecordAction(6, "Body Background"), new ScenarioTestWaitFramesAction(2));
            var outside = AddSteps(graph, "Outside", new ScenarioTestHoldEffectAction(outsideEffect));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, guard);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, outside);
            ConnectToIn(graph, guard, ScenarioGuardNode.Body, body);
            ConnectToIn(graph, body, ScenarioStepsNode.Done, AddSteps(graph, "Body Next", new ScenarioTestWaitFramesAction(10)));
            ConnectToIn(graph, guard, ScenarioGuardNode.Broken, AddSteps(graph, "Broken"));
            ConnectToIn(graph, guard, ScenarioGuardNode.Done, AddSteps(graph, "Guard Done"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            yield return WaitFrames(4);

            Assert.That(scenario.IsPerforming(body.Id), Is.True, "The body's background step is still going");

            condition.IsOn = false;
            yield return WaitFrames(10);

            Assert.That(Marks, Is.EqualTo(new[] { "Outside", "Body", "Body Next", "Broken" }));
            Assert.That(effect.ReleaseCount, Is.EqualTo(1));
            Assert.That(outsideEffect.IsReleased, Is.False, "Effects outside the body stay held");
        }

        private static ScenarioCondition FirstGuardCondition(Scenario scenario)
        {
            var guard = NodeOf<ScenarioGuardNode>(scenario, ScenarioTestGraphs.GuardTitle);
            var conditions = (System.Collections.Generic.List<ScenarioCondition>)typeof(ScenarioConditionList)
                .GetField("conditions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(guard.Conditions);

            return conditions[0];
        }
    }
}
