using System.Collections.Generic;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Проверки тестировщика, этап 2, переменные и списки: сравнение переменных разных видов, Change Number на счётчике
    /// (округление, Random Range, большие числа), уничтоженные объекты в списках, For Each, ноды с общим состоянием.
    /// </summary>
    public class ScenarioQaStage2VariableTests : ScenarioPlayModeTestBase
    {
        // Переменные экземпляра закрыты от тестов (internal) — читаем через отражение.
        private static ScenarioGraphVariables VariablesOf(Scenario scenario)
        {
            return (ScenarioGraphVariables)typeof(Scenario)
                .GetProperty("GraphVariables", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(scenario);
        }


        // ── Compare Variable: две переменные разных видов ────────────────

        // «Рычагов» (Counter) 3, «Нужно» (Number) 3. Сравнение должно сказать «равно» или упасть понятной ошибкой,
        // а не молча прочитать у Number поле счётчика (там 0).
        [Test, Category("QA_Found")]
        public void CompareVariable_CounterWithNumberVariable_DoesNotSilentlyCompareWrongField()
        {
            var graph = new ScenarioGraph();
            var levers = graph.AddVariable(new ScenarioVariableDefinition("Levers", ScenarioVariableKind.Counter));
            levers.InitialValue.CounterValue = 3;
            var needed = graph.AddVariable(new ScenarioVariableDefinition("Needed", ScenarioVariableKind.Number));
            needed.InitialValue.NumberValue = 3f;
            var compare = new CompareVariableCondition { Comparison = ScenarioVariableComparison.Equal };
            compare.Variable.VariableId = levers.Id;
            compare.OtherVariable.VariableId = needed.Id;
            var when = AddWhen(graph);
            when.Conditions.Add(compare);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Equal"));
            ConnectToIn(graph, when, ScenarioWhenNode.NotMet, AddSteps(graph, "Not Equal"));
            var scenario = CreateScenario(graph);

            LogAssert.ignoreFailingMessages = true;
            RaiseWhen(scenario);
            LogAssert.ignoreFailingMessages = false;

            Assert.That(Marks, Does.Not.Contain("Not Equal"), "3 = 3.0 came out «not equal»: the Number variable was read as a Counter");
        }


        // ── Change Number на счётчике ────────────────────────────────────

        // Random Range 1..3 на счётчике: игрок ждёт 1, 2 и 3 поровну. Округление дробного броска даёт краям половину шанса.
        [Test, Category("QA_Found")]
        public void ChangeNumber_RandomRangeOnCounter_GivesEveryValueEqually()
        {
            const int Rolls = 3000;

            var graph = new ScenarioGraph();
            var roll = graph.AddVariable(new ScenarioVariableDefinition("Roll", ScenarioVariableKind.Counter));
            var change = NumberChange(roll, ScenarioNumberOperation.RandomRange, 1f);
            change.MaxAmount.Value = 3f;
            var counts = new Dictionary<int, int>();
            var record = new ScenarioTestCallbackAction(run =>
            {
                var value = VariablesOf(run.Scenario).GetCounter(roll.Id);
                counts[value] = counts.TryGetValue(value, out var count) ? count + 1 : 1;
            });
            var scenario = CreateStepsScenario(graph, change, record);

            for (var index = 0; index < Rolls; index++)
                RaiseWhen(scenario);

            var shares = $"1: {Share(counts, 1, Rolls):P0}, 2: {Share(counts, 2, Rolls):P0}, 3: {Share(counts, 3, Rolls):P0}";
            Assert.That(counts.Keys, Is.EquivalentTo(new[] { 1, 2, 3 }), shares);
            Assert.That(Share(counts, 1, Rolls), Is.GreaterThan(0.29f), shares);
            Assert.That(Share(counts, 3, Rolls), Is.GreaterThan(0.29f), shares);
        }

        private static float Share(Dictionary<int, int> counts, int value, int total)
        {
            return counts.TryGetValue(value, out var count) ? (float)count / total : 0f;
        }

        // 5 / 2 и 7 / 2 на счётчике должны округляться одинаково (оба вверх или оба вниз).
        [Test, Category("QA_Found")]
        public void ChangeNumber_DivideCounter_RoundsHalvesTheSameWay()
        {
            var five = DivideCounter(5, 2f);
            var seven = DivideCounter(7, 2f);

            Assert.That(five - 2.5f > 0f, Is.EqualTo(seven - 3.5f > 0f), $"5 / 2 = {five}, 7 / 2 = {seven}");
        }

        private int DivideCounter(int start, float by)
        {
            var graph = new ScenarioGraph();
            var counter = graph.AddVariable(new ScenarioVariableDefinition("Counter", ScenarioVariableKind.Counter));
            counter.InitialValue.CounterValue = start;
            var scenario = CreateStepsScenario(graph, NumberChange(counter, ScenarioNumberOperation.Divide, by));

            RaiseWhen(scenario);

            return VariablesOf(scenario).GetCounter(counter.Id);
        }

        // Счётчик идёт через float: после 16 777 216 прибавка единицы теряется или даже уменьшает значение.
        [Test, Category("QA_Found")]
        public void ChangeNumber_AddOneToBigCounter_IsExact()
        {
            var graph = new ScenarioGraph();
            var ticks = graph.AddVariable(new ScenarioVariableDefinition("Ticks", ScenarioVariableKind.Counter));
            ticks.InitialValue.CounterValue = 16777217;
            var scenario = CreateStepsScenario(graph, NumberChange(ticks, ScenarioNumberOperation.Add, 1f));

            RaiseWhen(scenario);

            Assert.That(VariablesOf(scenario).GetCounter(ticks.Id), Is.EqualTo(16777218));
        }

        private static ChangeNumberAction NumberChange(ScenarioVariableDefinition variable, ScenarioNumberOperation operation, float amount)
        {
            var change = new ChangeNumberAction { Operation = operation };
            change.Variable.VariableId = variable.Id;
            change.Amount.Value = amount;

            return change;
        }


        // ── Уничтоженные объекты в списках ───────────────────────────────

        // Лампы коридора: одну игрок разбил (объект уничтожен). For Each должен пройти по живым,
        // а не отдать телу «пустой» элемент — следующий шаг упадёт, и остальные лампы останутся гореть.
        [Test, Category("QA_Found")]
        public void ForEach_SkipsDestroyedObjects()
        {
            var lampA = CreateGameObject("Lamp A");
            var lampB = CreateGameObject("Lamp B");
            var lampC = CreateGameObject("Lamp C");
            var graph = new ScenarioGraph();
            var current = graph.AddVariable(new ScenarioVariableDefinition("Current", ScenarioVariableKind.Object));
            current.ObjectType = typeof(GameObject);
            var forEach = graph.AddNode(new ScenarioForEachNode());
            forEach.List.Items.AddRange(new Object[] { lampA, lampB, lampC });
            forEach.Item.VariableId = current.Id;
            var when = AddWhen(graph);
            var body = graph.AddNode(new ScenarioStepsNode());
            body.AddStep(new ScenarioTestCallbackAction(run =>
            {
                var lamp = VariablesOf(run.Scenario).GetObject(current.Id);
                Marks.Add(lamp != null ? lamp.name : "destroyed");
            }));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, forEach);
            ConnectToIn(graph, forEach, ScenarioForEachNode.Each, body);
            var scenario = CreateScenario(graph);
            Object.DestroyImmediate(lampB);

            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Lamp A", "Lamp C" }));
        }

        // В списке остались только уничтоженные объекты — «Is Empty» должно быть верно, и List Count — 0.
        [Test, Category("QA_Found")]
        public void ListOfDestroyedObjects_IsEmpty_AndCountsZero()
        {
            var lamp = CreateGameObject("Lamp");
            var graph = new ScenarioGraph();
            var lamps = graph.AddVariable(new ScenarioVariableDefinition("Lamps", ScenarioVariableKind.ObjectList));
            lamps.ObjectType = typeof(GameObject);
            lamps.InitialValue.ObjectListValue.Add(lamp);
            var count = graph.AddVariable(new ScenarioVariableDefinition("Count", ScenarioVariableKind.Counter));
            count.InitialValue.CounterValue = -1;
            var isEmpty = new CompareVariableCondition { Comparison = ScenarioVariableComparison.IsEmpty };
            isEmpty.Variable.VariableId = lamps.Id;
            var listCount = new ListCountAction();
            listCount.List.VariableId = lamps.Id;
            listCount.Count.VariableId = count.Id;
            var when = AddWhen(graph);
            when.Conditions.Add(isEmpty);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Empty", listCount));
            ConnectToIn(graph, when, ScenarioWhenNode.NotMet, AddSteps(graph, "Not Empty", listCount));
            var scenario = CreateScenario(graph);
            Object.DestroyImmediate(lamp);

            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Empty" }), "All lamps are broken, but the list is «not empty»");
            Assert.That(VariablesOf(scenario).GetCounter(count.Id), Is.Zero);
        }


        // ── Ноды с общим состоянием ──────────────────────────────────────

        // Once — одна на ноду, а не на запуск: два запуска рядом проходят её один раз.
        [Test]
        public void Once_SharedByRunAlongsideRuns_LetsOnlyFirstThrough()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            when.RerunRule = ScenarioRerunRule.RunAlongside;
            var once = graph.AddNode(new ScenarioOnceNode());
            var hold = AddSteps(graph, "Hold", new ScenarioTestWaitFramesAction(5));
            ConnectToIn(graph, when, ScenarioWhenNode.Then, hold);
            ConnectToIn(graph, hold, ScenarioStepsNode.Done, once);
            ConnectToIn(graph, once, ScenarioOnceNode.Out, AddSteps(graph, "Scream"));
            var scenario = CreateScenario(graph);

            RaiseWhen(scenario);
            RaiseWhen(scenario);
            scenario.Stop();
            RaiseWhen(scenario);

            Assert.That(Marks, Is.EqualTo(new[] { "Hold", "Hold", "Hold" }));
        }

        // Случаи с постоянными id: повтор значения — два выхода, побеждает первый; проверка графа о повторе предупреждает.
        [Test]
        public void SwitchOnCounter_RepeatedValue_FirstCaseWins()
        {
            var graph = new ScenarioGraph();
            var floor = graph.AddVariable(new ScenarioVariableDefinition("Floor", ScenarioVariableKind.Counter));
            floor.InitialValue.CounterValue = 1;
            var switchNode = graph.AddNode(new ScenarioSwitchOnCounterNode());
            var firstOne = switchNode.AddCase(1);
            var secondOne = switchNode.AddCase(1);
            switchNode.AddCase(-2);
            SetField(switchNode, "counter", ScenarioCounterReference.FromVariable(floor.Id));
            var when = AddWhen(graph);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, switchNode);
            ConnectToIn(graph, switchNode, firstOne.Output, AddSteps(graph, "One"));
            ConnectToIn(graph, switchNode, secondOne.Output, AddSteps(graph, "Second One"));
            ConnectToIn(graph, switchNode, ScenarioSwitchOnCounterNode.Default, AddSteps(graph, "Default"));

            Assert.That(switchNode.CollectPorts().Outputs.Count, Is.EqualTo(4), "1, 1, -2 and Default");
            Assert.That(switchNode.TryFindRepeatedValue(out var repeated) && repeated == 1, Is.True);

            RaiseWhen(CreateScenario(graph));

            Assert.That(Marks, Is.EqualTo(new[] { "One" }));
        }
    }
}
