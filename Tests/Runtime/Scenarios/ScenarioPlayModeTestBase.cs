using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FeatherKit.Contexts;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Основа PlayMode-тестов рантайма графа: поднимает игру, ставит сценарии на объекты, убирает за собой.
    /// - граф кладётся в закрытые поля Scenario до его Awake — так, как их заполнил бы инспектор
    /// - журнал меток ScenarioTestRecordAction и запуски RunReported собираются заново на каждый тест
    /// </summary>
    public abstract class ScenarioPlayModeTestBase
    {
        private const BindingFlags PrivateField = BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly List<Object> createdObjects = new List<Object>();


        protected List<string> Marks => ScenarioTestRecordAction.RecordedMarks;

        protected List<ScenarioRunRecord> ReportedRuns { get; } = new List<ScenarioRunRecord>();


        // ── Подготовка и уборка ──────────────────────────────────────────

        [SetUp]
        public void LaunchGame()
        {
            Marks.Clear();
            ReportedRuns.Clear();
            Scenario.RunReported += ReportedRuns.Add;

            if (AppContext.Current == null)
                AppContext.Launch(new ScenarioTestAppContext());
        }

        [TearDown]
        public void ShutDownGame()
        {
            Scenario.RunReported -= ReportedRuns.Add;

            foreach (var createdObject in createdObjects)
            {
                if (createdObject != null)
                    Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
            AppContext.Shutdown();
        }


        // ── Сценарии ─────────────────────────────────────────────────────

        protected Scenario CreateScenario(ScenarioGraph graph, string objectName = "Scenario",
            params ScenarioVariableBinding[] bindings)
        {
            return CreateScenario(objectName, scenario =>
            {
                SetField(scenario, "graphSource", ScenarioGraphSource.Embedded);
                SetField(scenario, "graph", graph);
                SetField(scenario, "variableBindings", new List<ScenarioVariableBinding>(bindings));
            });
        }

        protected Scenario CreateScenario(ScenarioGraphAsset graphAsset, string objectName = "Scenario",
            params ScenarioVariableBinding[] bindings)
        {
            return CreateScenario(objectName, scenario =>
            {
                SetField(scenario, "graphSource", ScenarioGraphSource.Asset);
                SetField(scenario, "graphAsset", graphAsset);
                SetField(scenario, "variableBindings", new List<ScenarioVariableBinding>(bindings));
            });
        }

        /// <summary>Сценарий старым режимом: пустой граф, шаги списком.</summary>
        protected Scenario CreateListScenario(params ScenarioAction[] actions)
        {
            return CreateScenario("List Scenario", scenario =>
                SetField(scenario, "actions", new List<ScenarioAction>(actions)));
        }

        // Объект создаётся выключенным: Awake сценария должен увидеть уже заполненные поля.
        private Scenario CreateScenario(string objectName, System.Action<Scenario> fillFields)
        {
            var gameObject = CreateGameObject(objectName);
            gameObject.SetActive(false);

            var scenario = gameObject.AddComponent<Scenario>();
            fillFields(scenario);
            gameObject.SetActive(true);

            return scenario;
        }


        protected ScenarioGraphAsset CreateGraphAsset(ScenarioGraph graph)
        {
            var graphAsset = ScriptableObject.CreateInstance<ScenarioGraphAsset>();
            graphAsset.Graph = graph;
            graphAsset.hideFlags = HideFlags.HideAndDontSave;
            createdObjects.Add(graphAsset);

            return graphAsset;
        }

        protected GameObject CreateGameObject(string objectName)
        {
            var gameObject = new GameObject(objectName) { hideFlags = HideFlags.HideAndDontSave };
            createdObjects.Add(gameObject);

            return gameObject;
        }

        protected T CreateScriptableObject<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            instance.hideFlags = HideFlags.HideAndDontSave;
            createdObjects.Add(instance);

            return instance;
        }


        // ── Сборка графов ────────────────────────────────────────────────

        protected static ScenarioWhenNode AddWhen(ScenarioGraph graph, string title = ScenarioTestGraphs.WhenTitle)
        {
            var whenNode = graph.AddNode(new ScenarioWhenNode { CustomTitle = title });
            whenNode.AddEvent(new ScenarioTestManualEvent(title));

            return whenNode;
        }

        /// <summary>Нода Do: первым шагом пишет свою подпись, дальше — переданные шаги.</summary>
        protected static ScenarioStepsNode AddSteps(ScenarioGraph graph, string title, params ScenarioAction[] steps)
        {
            var stepsNode = graph.AddNode(new ScenarioStepsNode { CustomTitle = title });
            stepsNode.AddStep(new ScenarioTestRecordAction(title));

            foreach (var step in steps)
                stepsNode.AddStep(step);

            return stepsNode;
        }

        protected static void ConnectToIn(ScenarioGraph graph, ScenarioNode fromNode, ScenarioOutput output, ScenarioNode toNode)
        {
            var input = toNode is ScenarioFlowNode ? ScenarioFlowNode.In : ScenarioStepsNode.In;
            graph.Connect(fromNode, output, toNode, input);
        }


        // ── Ноды и события ───────────────────────────────────────────────

        /// <summary>Нода из графа, который исполняет сценарий: у граф-файла это своя копия, а не ассет.</summary>
        protected static T NodeOf<T>(Scenario scenario, string title) where T : ScenarioNode
        {
            return ScenarioTestGraphs.FindNode<T>(scenario.Graph, title);
        }

        /// <summary>Сработать первому событию ноды When с этой подписью.</summary>
        protected static void RaiseWhen(Scenario scenario, string title = ScenarioTestGraphs.WhenTitle, GameObject instigator = null)
        {
            var whenNode = NodeOf<ScenarioWhenNode>(scenario, title);
            ((ScenarioTestManualEvent)whenNode.Events[0]).Raise(instigator);
        }

        protected static IEnumerator WaitFrames(int frames)
        {
            for (var frame = 0; frame < frames; frame++)
                yield return null;
        }


        // ── Общие помощники ──────────────────────────────────────────────

        protected static void SetField(object target, string fieldName, object value)
        {
            var field = FindField(target.GetType(), fieldName);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} has no field “{fieldName}”");
            field.SetValue(target, value);
        }

        // Поле может лежать в базовом классе: приватные поля базы через наследника не видны.
        private static FieldInfo FindField(System.Type type, string fieldName)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(fieldName, PrivateField);

                if (field != null)
                    return field;
            }

            return null;
        }


        /// <summary>Игра для тестов: только встроенное — раннеры корутин и апдейта.</summary>
        private class ScenarioTestAppContext : AppContext
        {
            protected override void RegisterServices()
            {
            }
        }
    }
}
