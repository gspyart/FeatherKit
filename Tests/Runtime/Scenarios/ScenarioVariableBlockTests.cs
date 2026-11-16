using System.Collections.Generic;
using System.Text.RegularExpressions;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>Блоки переменных: Set Variable, Change Number, Compare Variable, Compare Number, Variable Changed, запись объектов в тип переменной.</summary>
    public class ScenarioVariableBlockTests : ScenarioPlayModeTestBase
    {
        // ── Set Variable ─────────────────────────────────────────────────

        [Test]
        public void SetVariable_FromValue_AndFromOtherVariable()
        {
            var graph = new ScenarioGraph();
            var delay = graph.AddVariable(new ScenarioVariableDefinition("Delay", ScenarioVariableKind.Number));
            var backup = graph.AddVariable(new ScenarioVariableDefinition("Backup", ScenarioVariableKind.Number));
            backup.InitialValue.NumberValue = 7f;

            var setFromValue = new SetVariableAction();
            setFromValue.Variable.VariableId = delay.Id;
            setFromValue.Value.NumberValue = 2.5f;
            var setFromBackup = new SetVariableAction();
            setFromBackup.Variable.VariableId = delay.Id;
            setFromBackup.SourceVariable.VariableId = backup.Id;
            var record = new ScenarioTestRecordNumberAction(ScenarioValue<float>.FromVariable(delay.Id));

            RaiseWhen(CreateStepsScenario(graph, setFromValue, record, setFromBackup, record));

            Assert.That(Marks, Is.EqualTo(new[] { "Steps", "2.5", "7" }));
        }

        [Test]
        public void SetVariable_FromVariableOfOtherKind_FailsRun()
        {
            var graph = new ScenarioGraph();
            var delay = graph.AddVariable(new ScenarioVariableDefinition("Delay", ScenarioVariableKind.Number));
            var caption = graph.AddVariable(new ScenarioVariableDefinition("Caption", ScenarioVariableKind.Text));
            var set = new SetVariableAction();
            set.Variable.VariableId = delay.Id;
            set.SourceVariable.VariableId = caption.Id;

            LogAssert.Expect(LogType.Error, new Regex("is a Text"));
            LogAssert.Expect(LogType.Exception, new Regex("is a Text"));

            RaiseWhen(CreateStepsScenario(graph, set));

            Assert.That(ReportedRuns[0].Outcome, Is.EqualTo(ScenarioRunOutcome.Failed));
        }


        // ── Change Number ────────────────────────────────────────────────

        [Test]
        public void ChangeNumber_CounterIsRounded_NumberIsNot()
        {
            var graph = new ScenarioGraph();
            var levers = graph.AddVariable(new ScenarioVariableDefinition("Levers", ScenarioVariableKind.Counter));
            levers.InitialValue.CounterValue = 3;
            var delay = graph.AddVariable(new ScenarioVariableDefinition("Delay", ScenarioVariableKind.Number));
            delay.InitialValue.NumberValue = 3f;

            var multiplyLevers = NumberChange(levers, ScenarioNumberOperation.Multiply, 1.5f);
            var multiplyDelay = NumberChange(delay, ScenarioNumberOperation.Multiply, 1.5f);

            RaiseWhen(CreateStepsScenario(graph, multiplyLevers, multiplyDelay,
                new ScenarioTestRecordCounterAction(ScenarioValue<int>.FromVariable(levers.Id)),
                new ScenarioTestRecordNumberAction(ScenarioValue<float>.FromVariable(delay.Id))));

            Assert.That(Marks, Is.EqualTo(new[] { "Steps", "4", "4.5" }));
        }

        [Test]
        public void ChangeNumber_DivideByZero_FailsRun()
        {
            var graph = new ScenarioGraph();
            var delay = graph.AddVariable(new ScenarioVariableDefinition("Delay", ScenarioVariableKind.Number));

            LogAssert.Expect(LogType.Error, new Regex("Division by zero"));
            LogAssert.Expect(LogType.Exception, new Regex("Division by zero"));

            RaiseWhen(CreateStepsScenario(graph, NumberChange(delay, ScenarioNumberOperation.Divide, 0f)));

            Assert.That(ReportedRuns[0].Outcome, Is.EqualTo(ScenarioRunOutcome.Failed));
        }

        [Test]
        public void ChangeNumber_RandomRange_StaysInRange()
        {
            var graph = new ScenarioGraph();
            var roll = graph.AddVariable(new ScenarioVariableDefinition("Roll", ScenarioVariableKind.Number));
            var change = NumberChange(roll, ScenarioNumberOperation.RandomRange, 2f);
            change.MaxAmount.Value = 3f;
            var isInRange = new CompareNumberCondition { Comparison = ScenarioComparison.GreaterOrEqual };
            isInRange.Number.VariableId = roll.Id;
            isInRange.Other.Value = 2f;
            var isBelowMax = new CompareNumberCondition { Comparison = ScenarioComparison.LessOrEqual };
            isBelowMax.Number.VariableId = roll.Id;
            isBelowMax.Other.Value = 3f;

            var when = AddWhen(graph);
            var check = graph.AddNode(new ScenarioIfNode());
            check.Conditions.Add(isInRange);
            check.Conditions.Add(isBelowMax);
            var roller = AddSteps(graph, "Roll", change);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, roller);
            ConnectToIn(graph, roller, ScenarioStepsNode.Done, check);
            ConnectToIn(graph, check, ScenarioIfNode.True, AddSteps(graph, "In Range"));
            var scenario = CreateScenario(graph);

            for (var attempt = 0; attempt < 20; attempt++)
                RaiseWhen(scenario);

            Assert.That(Marks.FindAll(mark => mark == "In Range").Count, Is.EqualTo(20));
        }

        private static ChangeNumberAction NumberChange(ScenarioVariableDefinition variable, ScenarioNumberOperation operation, float amount)
        {
            var change = new ChangeNumberAction { Operation = operation };
            change.Variable.VariableId = variable.Id;
            change.Amount.Value = amount;

            return change;
        }


        // ── Compare Variable ─────────────────────────────────────────────

        [TestCase(ScenarioVariableComparison.Greater, 1f, true)]
        [TestCase(ScenarioVariableComparison.LessOrEqual, 1f, false)]
        [TestCase(ScenarioVariableComparison.Equal, 2f, true)]
        public void CompareVariable_Numbers(ScenarioVariableComparison comparison, float other, bool expected)
        {
            var graph = new ScenarioGraph();
            var delay = graph.AddVariable(new ScenarioVariableDefinition("Delay", ScenarioVariableKind.Number));
            delay.InitialValue.NumberValue = 2f;
            var compare = new CompareVariableCondition { Comparison = comparison };
            compare.Variable.VariableId = delay.Id;
            compare.Value.NumberValue = other;

            Assert.That(CheckOnce(graph, compare), Is.EqualTo(expected));
        }

        [Test]
        public void CompareVariable_Text()
        {
            var graph = new ScenarioGraph();
            var caption = graph.AddVariable(new ScenarioVariableDefinition("Caption", ScenarioVariableKind.Text));
            caption.InitialValue.TextValue = "Boo";
            var sameText = new CompareVariableCondition { Comparison = ScenarioVariableComparison.Equal };
            sameText.Variable.VariableId = caption.Id;
            sameText.Value.TextValue = "Boo";

            Assert.That(CheckOnce(graph, sameText), Is.True);
        }

        [Test]
        public void CompareVariable_EmptyList()
        {
            var graph = new ScenarioGraph();
            var lamps = graph.AddVariable(new ScenarioVariableDefinition("Lamps", ScenarioVariableKind.ObjectList));
            var noLamps = new CompareVariableCondition { Comparison = ScenarioVariableComparison.IsEmpty };
            noLamps.Variable.VariableId = lamps.Id;

            Assert.That(CheckOnce(graph, noLamps), Is.True);
        }

        [Test]
        public void CompareVariable_LessOnText_FailsRun()
        {
            var graph = new ScenarioGraph();
            var caption = graph.AddVariable(new ScenarioVariableDefinition("Caption", ScenarioVariableKind.Text));
            var compare = new CompareVariableCondition { Comparison = ScenarioVariableComparison.Less };
            compare.Variable.VariableId = caption.Id;

            LogAssert.Expect(LogType.Error, new Regex("only = and ≠"));
            LogAssert.Expect(LogType.Exception, new Regex("only = and ≠"));

            CheckOnce(graph, compare);
        }

        // When с условием: Then → «Yes», Not Met → «No». Граф — свой на каждый вызов.
        private bool CheckOnce(ScenarioGraph graph, ScenarioCondition condition)
        {
            var when = AddWhen(graph);
            when.Conditions.Add(condition);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Yes"));
            ConnectToIn(graph, when, ScenarioWhenNode.NotMet, AddSteps(graph, "No"));
            RaiseWhen(CreateScenario(graph));

            return Marks.Contains("Yes");
        }


        // ── Variable Changed и запись объектов ───────────────────────────

        [Test]
        public void VariableChanged_FiresOnSetAndOnListChangedInPlace()
        {
            var graph = new ScenarioGraph();
            var delay = graph.AddVariable(new ScenarioVariableDefinition("Delay", ScenarioVariableKind.Number));
            var lamps = graph.AddVariable(new ScenarioVariableDefinition("Lamps", ScenarioVariableKind.ObjectList));
            var lampObject = CreateGameObject("Lamp");

            var set = new SetVariableAction();
            set.Variable.VariableId = delay.Id;
            set.Value.NumberValue = 1f;
            var add = new AddToListAction();
            add.List.VariableId = lamps.Id;
            add.Item.Value = lampObject;

            foreach (var watched in new[] { delay, lamps })
            {
                var changed = new VariableChangedEvent();
                changed.Variable.VariableId = watched.Id;
                var watcher = graph.AddNode(new ScenarioWhenNode { CustomTitle = "Watch " + watched.Name, RerunRule = ScenarioRerunRule.RunAlongside });
                watcher.AddEvent(changed);
                ConnectToIn(graph, watcher, ScenarioWhenNode.Then, AddSteps(graph, "Changed " + watched.Name));
            }

            RaiseWhen(CreateStepsScenario(graph, set, set, add));

            Assert.That(Marks, Is.EqualTo(new[] { "Steps", "Changed Delay", "Changed Lamps" }), "The same value again is not a change");
        }

        [Test]
        public void SetObject_StoresInVariableType_AndFailsWithoutComponent()
        {
            var graph = new ScenarioGraph();
            var lamp = graph.AddVariable(new ScenarioVariableDefinition("Lamp", ScenarioVariableKind.Object) { ObjectType = typeof(Light) });
            var lampObject = CreateGameObject("Lamp Object");
            lampObject.AddComponent<Light>();
            var speaker = CreateGameObject("Speaker");

            var setLamp = new SetVariableAction();
            setLamp.Variable.VariableId = lamp.Id;
            setLamp.Value.ObjectValue = lampObject;
            var setSpeaker = new SetVariableAction();
            setSpeaker.Variable.VariableId = lamp.Id;
            setSpeaker.Value.ObjectValue = speaker;

            LogAssert.Expect(LogType.Error, new Regex("takes Light: “Speaker” has none"));
            LogAssert.Expect(LogType.Exception, new Regex("takes Light"));

            RaiseWhen(CreateStepsScenario(graph, setLamp, new ScenarioTestRecordLightAction(ScenarioValue<Light>.FromVariable(lamp.Id)), setSpeaker));

            Assert.That(Marks, Is.EqualTo(new[] { "Steps", "Lamp Object" }));
        }


        // ── Списки по типу ───────────────────────────────────────────────

        [Test]
        public void ObjectList_GetInto_ReusesList_GetIsNew()
        {
            var graph = new ScenarioGraph();
            var lamps = graph.AddVariable(new ScenarioVariableDefinition("Lamps", ScenarioVariableKind.ObjectList) { ObjectType = typeof(Light) });
            lamps.InitialValue.ObjectListValue.Add(CreateGameObject("Lamp").AddComponent<Light>());
            var list = ScenarioObjectList<Light>.FromVariable(lamps.Id);
            var reused = new List<Light> { null, null };
            IReadOnlyList<Light> first = null;
            IReadOnlyList<Light> second = null;

            var probe = new ScenarioTestCallbackAction(run =>
            {
                list.GetInto(run, reused);
                first = list.Get(run);
                second = list.Get(run);
            });

            RaiseWhen(CreateStepsScenario(graph, probe));

            Assert.That(reused.Count, Is.EqualTo(1));
            Assert.That(reused[0].name, Is.EqualTo("Lamp"));
            Assert.That(first, Is.Not.SameAs(second));
        }

        [Test]
        public void TypedListThroughSlot_FailsWithHint()
        {
            var graph = new ScenarioGraph();
            var lamps = graph.AddVariable(new ScenarioVariableDefinition("Lamps", ScenarioVariableKind.ObjectList));

            LogAssert.Expect(LogType.Error, new Regex("use ScenarioObjectList"));
            LogAssert.Expect(LogType.Exception, new Regex("use ScenarioObjectList"));

            RaiseWhen(CreateStepsScenario(graph,
                new ScenarioTestReadTypedListAction(ScenarioVariableSlot<List<Light>>.FromVariable(lamps.Id))));
        }
    }
}
