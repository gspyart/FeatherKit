using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>Проверки тестировщика: переменные графа — привязка неподходящего объекта, пропавший id в поле-значении.</summary>
    public class ScenarioQaVariablesTests : ScenarioPlayModeTestBase
    {
        // Переменная «Lamp» ждёт Light; на объекте к ней привязали объект без Light (например, после смены типа
        // переменной). По правилу части «чужой вид — ошибка, а не тихий ноль» запуск должен упасть с понятной ошибкой.
        [Test, Category("QA_Found")]
        public void BindingWithoutRequiredComponent_IsAnError_NotSilentNull()
        {
            var graph = new ScenarioGraph();
            var lamp = graph.AddVariable(new ScenarioVariableDefinition("Lamp", ScenarioVariableKind.Object));
            lamp.ObjectType = typeof(Light);
            lamp.IsExposed = true;
            var when = AddWhen(graph);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Steps",
                new ScenarioTestRecordLightAction(ScenarioValue<Light>.FromVariable(lamp.Id))));
            var notALamp = CreateGameObject("Speaker");
            notALamp.AddComponent<AudioSource>();
            var binding = new ScenarioVariableBinding(lamp.Id);
            binding.Value.ObjectValue = notALamp;

            var scenario = CreateScenario(CreateGraphAsset(graph), "Scenario", binding);
            LogAssert.ignoreFailingMessages = true;
            RaiseWhen(scenario);
            LogAssert.ignoreFailingMessages = false;

            Assert.That(Marks, Is.EqualTo(new[] { "Steps" }), "The step got null silently: " + string.Join(", ", Marks));
        }

        // Поле-значение (ScenarioValue) ссылается на удалённую переменную — запуск падает понятной ошибкой.
        [Test]
        public void ValueFieldWithRemovedVariable_FailsRunWithClearMessage()
        {
            var graph = new ScenarioGraph();
            var when = AddWhen(graph);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Steps",
                new ScenarioTestRecordLightAction(ScenarioValue<Light>.FromVariable("removed"))));
            var scenario = CreateScenario(graph);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Graph variable “removed” not found"));
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("Graph variable “removed” not found"));

            RaiseWhen(scenario);

            Assert.That(ReportedRuns[0].Outcome, Is.EqualTo(ScenarioRunOutcome.Failed));
        }

        // Переменная Counter, а поле ждёт Light — вид не тот: ошибка, а не ноль.
        [Test]
        public void ValueFieldWithWrongKindVariable_FailsRun()
        {
            var graph = new ScenarioGraph();
            var levers = graph.AddVariable(new ScenarioVariableDefinition("Levers", ScenarioVariableKind.Counter));
            var when = AddWhen(graph);
            ConnectToIn(graph, when, ScenarioWhenNode.Then, AddSteps(graph, "Steps",
                new ScenarioTestRecordLightAction(ScenarioValue<Light>.FromVariable(levers.Id))));
            var scenario = CreateScenario(graph);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("is a Counter, not a Object"));
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("is a Counter, not a Object"));

            RaiseWhen(scenario);

            Assert.That(ReportedRuns[0].Outcome, Is.EqualTo(ScenarioRunOutcome.Failed));
        }
    }
}
