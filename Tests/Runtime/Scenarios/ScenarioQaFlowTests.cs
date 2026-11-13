using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Проверки тестировщика: обрыв куска посреди шага, Guard с проводом назад, Wait For и сигнал,
    /// который сам же и запустил запуск, ошибки внутри нод потока, пауза, рост цепочки веток.
    /// Красные тесты помечены QA-Found — это найденные баги.
    /// </summary>
    public class ScenarioQaFlowTests : ScenarioPlayModeTestBase
    {
        private const BindingFlags PrivateField = BindingFlags.Instance | BindingFlags.NonPublic;


        // ── Обрыв куска посреди шага ─────────────────────────────────────

        // Шаг «Tick» шлёт сигнал → другая When → вход Stop у Repeat → виток с «Tick» обрывается.
        // Оборванная нода не должна доделывать свою стопку и пускать новые фоновые шаги.
        [UnityTest, Category("QA_Found")]
        public IEnumerator RepeatStop_FromInsideRound_CutsRestOfThatDoNode()
        {
            var stopSignal = CreateScriptableObject<ScenarioSignal>();
            var lateEffect = new ScenarioTestHeldEffect();
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var repeat = graph.AddNode(new ScenarioRepeatNode { Mode = ScenarioRepeatMode.Forever });
            var tick = AddSteps(graph, "Tick",
                new ScenarioQaSendSignalAction(stopSignal),
                new ScenarioTestRecordAction("After Signal"),
                new ScenarioTestHoldEffectAction(lateEffect),
                new ScenarioTestBackgroundRecordAction(2, "Late Background"),
                new ScenarioTestWaitFramesAction(1));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, repeat);
            ConnectToIn(graph, repeat, ScenarioRepeatNode.EachTime, tick);
            ConnectToIn(graph, repeat, ScenarioRepeatNode.Finished, AddSteps(graph, "Finished"));
            var stopWhen = AddSignalWhen(graph, "Stop When", stopSignal);
            graph.Connect(stopWhen, ScenarioWhenNode.Then, repeat, ScenarioRepeatNode.Stop);
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            yield return WaitFrames(6);

            Assert.That(lateEffect.ReleaseCount, Is.LessThanOrEqualTo(1), "An effect taken after the cut is let go at once");
            Assert.That(Marks, Is.EqualTo(new[] { "Tick", "Finished" }),
                "Steps after the cut ran anyway, and a background step was started from the cut node: "
                + string.Join(", ", Marks));
        }


        // ── Guard While с проводом назад ─────────────────────────────────

        // Body → Do → провод назад в Guard: стражи вкладываются друг в друга. Условие нарушилось —
        // Broken должен сработать один раз, а Done — ни разу.
        [UnityTest, Category("QA_Found")]
        public IEnumerator Guard_WireBackIntoItself_EveryFrameCheck_BreaksOnce()
        {
            var condition = new ScenarioTestSwitchCondition(true);
            var scenario = CreateScenario(GuardLoopGraph(condition, checkInterval: 0f, new ScenarioTestWaitFramesAction(2)));

            RaiseWhen(scenario);
            yield return WaitFrames(9);
            condition.IsOn = false;
            yield return WaitFrames(4);

            Assert.That(Marks.FindAll(mark => mark == "Broken").Count, Is.EqualTo(1));
            Assert.That(Marks, Does.Not.Contain("Guard Done"),
                "The innermost guard breaks first; every outer guard then sees its body ended and goes to Done");
            Assert.That(scenario.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator Guard_WireBackIntoItself_WithCheckInterval_BreaksOnce()
        {
            var condition = new ScenarioTestSwitchCondition(true);
            var scenario = CreateScenario(GuardLoopGraph(condition, checkInterval: 0.1f, new ScenarioQaWaitSecondsAction(0.03f)));

            RaiseWhen(scenario);
            yield return new WaitForSeconds(0.5f);
            condition.IsOn = false;
            yield return new WaitForSeconds(0.5f);

            Assert.That(Marks.FindAll(mark => mark == "Broken").Count, Is.EqualTo(1),
                "Each nested guard checks on its own schedule and fires Broken on its own");
            Assert.That(Marks, Does.Not.Contain("Guard Done"));
            Assert.That(scenario.IsRunning, Is.False);
        }

        // Wait For внутри Body: условие сторожа нарушилось — ожидание обрывается и отпускает события исходов.
        [UnityTest]
        public IEnumerator Guard_Breaks_WhileBodyWaitsFor_ReleasesCaseEvents()
        {
            var condition = new ScenarioTestSwitchCondition(true);
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var guard = graph.AddNode(new ScenarioGuardNode());
            guard.Conditions.Add(condition);
            var waitFor = graph.AddNode(new ScenarioWaitForNode());
            var go = waitFor.AddCase(new ScenarioWaitCase(new ScenarioTestManualEvent("go")));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, guard);
            ConnectToIn(graph, guard, ScenarioGuardNode.Body, waitFor);
            ConnectToIn(graph, waitFor, go.Output, AddSteps(graph, "Go"));
            ConnectToIn(graph, guard, ScenarioGuardNode.Broken, AddSteps(graph, "Broken"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            yield return WaitFrames(2);

            Assert.That(go.AwaitedEvent.IsAttached, Is.True);

            condition.IsOn = false;
            yield return WaitFrames(2);
            ((ScenarioTestManualEvent)go.AwaitedEvent).Raise();
            yield return WaitFrames(2);

            Assert.That(go.AwaitedEvent.IsAttached, Is.False);
            Assert.That(Marks, Is.EqualTo(new[] { "Broken" }));
            Assert.That(scenario.IsRunning, Is.False);
        }

        // Stop This Run внутри Body: запуск кончается целиком, сторож не идёт ни в Broken, ни в Done.
        [UnityTest]
        public IEnumerator StopThisRun_InsideGuardBody_EndsRun_WithoutBrokenOrDone()
        {
            var effect = new ScenarioTestHeldEffect();
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var guard = graph.AddNode(new ScenarioGuardNode());
            guard.Conditions.Add(new ScenarioTestSwitchCondition(true));
            var body = AddSteps(graph, "Body", new ScenarioTestHoldEffectAction(effect), new ScenarioTestWaitFramesAction(2));
            var stopRun = graph.AddNode(new ScenarioStopRunNode());
            ConnectToIn(graph, when, ScenarioWhenNode.Then, guard);
            ConnectToIn(graph, guard, ScenarioGuardNode.Body, body);
            ConnectToIn(graph, body, ScenarioStepsNode.Done, stopRun);
            ConnectToIn(graph, guard, ScenarioGuardNode.Broken, AddSteps(graph, "Broken"));
            ConnectToIn(graph, guard, ScenarioGuardNode.Done, AddSteps(graph, "Guard Done"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            yield return WaitFrames(5);

            Assert.That(Marks, Is.EqualTo(new[] { "Body" }));
            Assert.That(effect.ReleaseCount, Is.EqualTo(1));
            Assert.That(scenario.IsRunning, Is.False);
            Assert.That(ReportedRuns.ConvertAll(record => record.Outcome), Is.EqualTo(new[] { ScenarioRunOutcome.Stopped }));
        }

        // When → Guard: Body → «Body» (ожидание) → назад в Guard.In; Broken → «Broken»; Done → «Guard Done».
        private static ScenarioGraph GuardLoopGraph(ScenarioCondition condition, float checkInterval, ScenarioAction bodyWait)
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var guard = graph.AddNode(new ScenarioGuardNode { CheckInterval = checkInterval });
            guard.Conditions.Add(condition);
            var body = AddSteps(graph, "Body", bodyWait);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, guard);
            ConnectToIn(graph, guard, ScenarioGuardNode.Body, body);
            ConnectToIn(graph, body, ScenarioStepsNode.Done, guard);
            ConnectToIn(graph, guard, ScenarioGuardNode.Broken, AddSteps(graph, "Broken"));
            ConnectToIn(graph, guard, ScenarioGuardNode.Done, AddSteps(graph, "Guard Done"));

            return graph;
        }


        // ── Wait For и сигнал, который запустил запуск ───────────────────

        // «Стук»: When (сигнал Knock) → «Knock» → Wait For (снова Knock) → «Burst».
        // Второй стук должен выломать дверь у первого запуска, а второй запуск — ждать третьего стука.
        [UnityTest, Category("QA_Found")]
        public IEnumerator WaitFor_RunAlongside_NewRunDoesNotEatTheSignalThatStartedIt()
        {
            var knock = CreateScriptableObject<ScenarioSignal>();
            var scenario = CreateScenario(KnockGraph(knock, ScenarioRerunRule.RunAlongside));

            ScenarioSignals.Send(knock, null);
            yield return WaitFrames(2);
            ScenarioSignals.Send(knock, null);
            yield return WaitFrames(3);

            Assert.That(Marks, Is.EqualTo(new[] { "Knock", "Knock", "Burst" }),
                "The second run's Wait For counted the very signal that started this run");
            Assert.That(scenario.IsRunning, Is.True, "The second run still waits for the third knock");
        }

        [UnityTest, Category("QA_Found")]
        public IEnumerator WaitFor_Restart_NewRunDoesNotEatTheSignalThatRestartedIt()
        {
            var knock = CreateScriptableObject<ScenarioSignal>();
            var scenario = CreateScenario(KnockGraph(knock, ScenarioRerunRule.Restart));

            ScenarioSignals.Send(knock, null);
            yield return WaitFrames(2);
            ScenarioSignals.Send(knock, null);
            yield return WaitFrames(3);

            Assert.That(Marks, Is.EqualTo(new[] { "Knock", "Knock" }),
                "The restarted run's Wait For counted the very signal that restarted it");
            Assert.That(scenario.IsRunning, Is.True);
        }

        private ScenarioGraph KnockGraph(ScenarioSignal knock, ScenarioRerunRule rerunRule)
        {
            var graph = new ScenarioGraph();
            var when = AddSignalWhen(graph, "Knock When", knock, rerunRule);
            var knockSteps = AddSteps(graph, "Knock");
            var waitFor = graph.AddNode(new ScenarioWaitForNode());
            var knockAgain = waitFor.AddCase(new ScenarioWaitCase(SignalEvent(knock), "Knock Again"));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, knockSteps);
            ConnectToIn(graph, knockSteps, ScenarioStepsNode.Done, waitFor);
            ConnectToIn(graph, waitFor, knockAgain.Output, AddSteps(graph, "Burst"));

            return graph;
        }


        // ── Restart во время Wait For ────────────────────────────────────

        [UnityTest]
        public IEnumerator Restart_DuringWaitFor_ReleasesEffect_AndOnlyNewRunReacts()
        {
            var effect = new ScenarioTestHeldEffect();
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            when.RerunRule = ScenarioRerunRule.Restart;
            var start = AddSteps(graph, "Start", new ScenarioTestHoldEffectAction(effect));
            var waitFor = graph.AddNode(new ScenarioWaitForNode());
            var go = waitFor.AddCase(new ScenarioWaitCase(new ScenarioTestManualEvent("go")));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, start);
            ConnectToIn(graph, start, ScenarioStepsNode.Done, waitFor);
            ConnectToIn(graph, waitFor, go.Output, AddSteps(graph, "Go"));
            var scenario = CreateScenario(graph);
            var goEvent = (ScenarioTestManualEvent)go.AwaitedEvent;

            RaiseWhen(scenario);
            yield return null;
            RaiseWhen(scenario);

            Assert.That(effect.ReleaseCount, Is.EqualTo(1), "The cut run lets its effect go");
            Assert.That(goEvent.IsAttached, Is.True, "The new run listens again");

            yield return null;
            goEvent.Raise();
            yield return WaitFrames(2);

            Assert.That(Marks, Is.EqualTo(new[] { "Start", "Start", "Go" }));
            Assert.That(goEvent.IsAttached, Is.False);
        }


        // ── Ошибки внутри нод потока ─────────────────────────────────────

        [UnityTest]
        public IEnumerator FailingBranchInsideParallel_FailsRun_AndCutsSiblings()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var parallel = graph.AddNode(new ScenarioParallelNode());
            var slow = AddSteps(graph, "Slow", new ScenarioTestWaitFramesAction(4));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, parallel);
            ConnectToIn(graph, parallel, ScenarioParallelNode.BranchOutput(1),
                AddSteps(graph, "Bad", new ScenarioTestWaitFramesAction(1), new ScenarioTestFailAction("bad")));
            ConnectToIn(graph, parallel, ScenarioParallelNode.BranchOutput(2), slow);
            ConnectToIn(graph, slow, ScenarioStepsNode.Done, AddSteps(graph, "Slow End"));
            ConnectToIn(graph, parallel, ScenarioParallelNode.Done, AddSteps(graph, "Parallel Done"));
            var scenario = CreateScenario(graph);

            LogAssert.Expect(LogType.Error, new Regex(ScenarioTestFailAction.Message));
            LogAssert.Expect(LogType.Exception, new Regex(ScenarioTestFailAction.Message));

            RaiseWhen(scenario);
            yield return WaitFrames(8);

            Assert.That(Marks, Is.EqualTo(new[] { "Bad", "Slow" }));
            Assert.That(ReportedRuns.ConvertAll(record => record.Outcome), Is.EqualTo(new[] { ScenarioRunOutcome.Failed }));
            Assert.That(scenario.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator FailingConditionInWaitFor_FailsRun_AndReleasesCaseEvents()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var waitFor = graph.AddNode(new ScenarioWaitForNode());
            var go = waitFor.AddCase(new ScenarioWaitCase(new ScenarioTestManualEvent("go")));
            waitFor.AddCase(new ScenarioWaitCase(new ScenarioQaThrowingCondition("bad")));
            var start = AddSteps(graph, "Start", new ScenarioTestWaitFramesAction(1));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, start);
            ConnectToIn(graph, start, ScenarioStepsNode.Done, waitFor);
            var scenario = CreateScenario(graph);

            LogAssert.Expect(LogType.Error, new Regex(ScenarioQaThrowingCondition.Message));
            LogAssert.Expect(LogType.Exception, new Regex(ScenarioQaThrowingCondition.Message));

            RaiseWhen(scenario);
            yield return WaitFrames(4);

            Assert.That(go.AwaitedEvent.IsAttached, Is.False);
            Assert.That(ReportedRuns.ConvertAll(record => record.Outcome), Is.EqualTo(new[] { ScenarioRunOutcome.Failed }));
        }


        // ── Parallel First Done: ветки одинаково быстрые ─────────────────

        [UnityTest]
        public IEnumerator ParallelFirstDone_EquallyFastBranches_BothFinish_DoneOnce()
        {
            var effectA = new ScenarioTestHeldEffect();
            var effectB = new ScenarioTestHeldEffect();
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var parallel = graph.AddNode(new ScenarioParallelNode { Completion = ScenarioParallelCompletion.FirstDone });
            var branchA = AddSteps(graph, "A", new ScenarioTestHoldEffectAction(effectA), new ScenarioTestWaitFramesAction(2));
            var branchB = AddSteps(graph, "B", new ScenarioTestHoldEffectAction(effectB), new ScenarioTestWaitFramesAction(2));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, parallel);
            ConnectToIn(graph, parallel, ScenarioParallelNode.BranchOutput(1), branchA);
            ConnectToIn(graph, parallel, ScenarioParallelNode.BranchOutput(2), branchB);
            ConnectToIn(graph, branchA, ScenarioStepsNode.Done, AddSteps(graph, "A End"));
            ConnectToIn(graph, branchB, ScenarioStepsNode.Done, AddSteps(graph, "B End"));
            ConnectToIn(graph, parallel, ScenarioParallelNode.Done, AddSteps(graph, "Parallel Done"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            yield return WaitFrames(5);

            Assert.That(Marks, Is.EquivalentTo(new[] { "A", "B", "A End", "B End", "Parallel Done" }));
            Assert.That(effectA.IsReleased || effectB.IsReleased, Is.False, "Nobody was cut — nothing to let go");
        }


        // ── Пауза: timeScale 0 ───────────────────────────────────────────

        [UnityTest]
        public IEnumerator WaitForTimeout_DoesNotRunOut_WhilePaused()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var waitFor = graph.AddNode(new ScenarioWaitForNode { Timeout = 0.2f });
            ConnectToIn(graph, when, ScenarioWhenNode.Then, waitFor);
            ConnectToIn(graph, waitFor, ScenarioWaitForNode.TimedOut, AddSteps(graph, "Timed Out"));
            var scenario = CreateScenario(graph);

            try
            {
                RaiseWhen(scenario);
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(0.5f);

                Assert.That(Marks, Is.Empty, "Paused game — the timeout stands still");
            }
            finally
            {
                Time.timeScale = 1f;
            }

            yield return new WaitForSeconds(0.4f);

            Assert.That(Marks, Is.EqualTo(new[] { "Timed Out" }));
        }


        // ── Петля по проводам ────────────────────────────────────────────

        // Мгновенная петля через Sequence (Then 1 → Do → назад в Sequence) — ловится как вечный цикл, а не роняет игру.
        [Test]
        public void InstantLoopThroughSequence_FailsAsLoop()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var sequence = graph.AddNode(new ScenarioSequenceNode { ThenCount = 1 });
            var step = graph.AddNode(new ScenarioStepsNode());
            ConnectToIn(graph, when, ScenarioWhenNode.Then, sequence);
            ConnectToIn(graph, sequence, ScenarioSequenceNode.ThenOutput(1), step);
            ConnectToIn(graph, step, ScenarioStepsNode.Done, sequence);
            var scenario = CreateScenario(graph);

            LogAssert.Expect(LogType.Error, new Regex("Loop without waiting"));
            LogAssert.Expect(LogType.Exception, new Regex("Loop without waiting"));

            RaiseWhen(scenario);

            Assert.That(scenario.IsRunning, Is.False);
        }

        // Do → Wait For (событие) → назад в Do: петля на весь уровень. Цепочка «из какой ветки выросла» не должна расти с каждым витком.
        [UnityTest, Category("QA_Found")]
        public IEnumerator WireLoop_BranchAncestryDoesNotGrowWithEachRound()
        {
            const int Rounds = 300;

            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            var loop = graph.AddNode(new ScenarioStepsNode());
            var waitFor = graph.AddNode(new ScenarioWaitForNode());
            var again = waitFor.AddCase(new ScenarioWaitCase(new ScenarioTestManualEvent("again")));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, loop);
            ConnectToIn(graph, loop, ScenarioStepsNode.Done, waitFor);
            ConnectToIn(graph, waitFor, again.Output, loop);
            var scenario = CreateScenario(graph);
            var againEvent = (ScenarioTestManualEvent)again.AwaitedEvent;

            RaiseWhen(scenario);

            for (var round = 0; round < Rounds; round++)
            {
                yield return null;
                againEvent.Raise();
            }

            yield return null;

            var depth = DeepestBranchAncestry(scenario);
            Assert.That(depth, Is.LessThan(50), $"Ancestry chain after {Rounds} rounds: {depth} branches kept alive");
        }

        private static int DeepestBranchAncestry(Scenario scenario)
        {
            var runs = typeof(Scenario).GetField("runs", PrivateField).GetValue(scenario);
            var trackedRuns = (IList)runs.GetType().GetField("trackedRuns", PrivateField).GetValue(runs);
            var parentField = typeof(ScenarioBranch).GetField("parent", PrivateField);
            var deepest = 0;

            foreach (var trackedRun in trackedRuns)
            {
                var run = (ScenarioRun)trackedRun.GetType().GetProperty("Run").GetValue(trackedRun);
                var branches = (List<ScenarioBranch>)typeof(ScenarioRun).GetField("branches", PrivateField).GetValue(run);

                foreach (var branch in branches)
                {
                    var depth = 0;

                    for (var current = branch; current != null; current = (ScenarioBranch)parentField.GetValue(current))
                        depth++;

                    deepest = Mathf.Max(deepest, depth);
                }
            }

            return deepest;
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static ScenarioWhenNode AddSignalWhen(ScenarioGraph graph, string title, ScenarioSignal signal,
            ScenarioRerunRule rerunRule = ScenarioRerunRule.SkipIfRunning)
        {
            var when = graph.AddNode(new ScenarioWhenNode { CustomTitle = title, RerunRule = rerunRule });
            when.AddEvent(SignalEvent(signal));

            return when;
        }

        private static SignalReceivedEvent SignalEvent(ScenarioSignal signal)
        {
            var signalEvent = new SignalReceivedEvent();
            SetField(signalEvent, "signal", signal);

            return signalEvent;
        }
    }
}
