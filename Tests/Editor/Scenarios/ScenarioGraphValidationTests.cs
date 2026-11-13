using System;
using System.Collections;
using System.Linq;
using FeatherKit.EditorTools;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEngine;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>Проверка графа: какие проблемы находит и на какой ноде, и что чистый граф проблем не даёт.</summary>
    public class ScenarioGraphValidationTests
    {
        [Test]
        public void TestGraphs_HaveNoProblems()
        {
            Assert.That(ScenarioGraphValidation.Check(ScenarioTestGraphs.SilhouetteWaitFor()), Is.Empty);
            Assert.That(ScenarioGraphValidation.Check(ScenarioTestGraphs.WithVariables()), Is.Empty);
        }

        [Test]
        public void VariableNotFound_IsErrorOnItsNode()
        {
            var graph = new ScenarioGraph();
            var steps = graph.AddNode(new ScenarioStepsNode());
            steps.AddStep(new ScenarioValueTestAction(ScenarioValue<Light>.FromVariable("gone")));

            var problem = ScenarioGraphValidation.Check(graph).Single();

            Assert.That(problem.NodeId, Is.EqualTo(steps.Id));
            Assert.That(problem.IsError, Is.True);
            Assert.That(problem.Message, Does.Contain("not found"));
        }

        [Test]
        public void VariableOfWrongKind_IsError_WithBothKindsNamed()
        {
            var graph = new ScenarioGraph();
            var doorOpen = graph.AddVariable(new ScenarioVariableDefinition("Door Open", ScenarioVariableKind.Flag));
            var steps = graph.AddNode(new ScenarioStepsNode());
            steps.AddStep(new ScenarioValueTestAction(delay: ScenarioValue<float>.FromVariable(doorOpen.Id)));

            var problem = ScenarioGraphValidation.Check(graph).Single();

            Assert.That(problem.Message, Does.Contain("Flag").And.Contain("Number"));
        }

        [Test]
        public void EmptyObjectValue_IsWarning()
        {
            var graph = new ScenarioGraph();
            graph.AddNode(new ScenarioStepsNode()).AddStep(new ScenarioValueTestAction(new ScenarioValue<Light>()));

            var problem = ScenarioGraphValidation.Check(graph).Single();

            Assert.That(problem.IsError, Is.False);
            Assert.That(problem.Message, Does.Contain("Lamp"));
        }

        [Test]
        public void EmptyBlockInStack_IsError()
        {
            var graph = new ScenarioGraph();
            var when = graph.AddNode(new ScenarioWhenNode());
            when.AddEvent(null);

            Assert.That(ScenarioGraphValidation.Check(graph).Single().IsError, Is.True);
        }

        [Test]
        public void WireToMissingPort_IsErrorOnBothEnds()
        {
            var graph = ScenarioTestGraphs.SilhouetteWaitFor();
            var waitLook = ScenarioTestGraphs.FindNode<ScenarioWaitForNode>(graph, ScenarioTestGraphs.WaitLookTitle);

            // Так исход удаляют в обход API: провод его выхода остаётся висеть.
            var cases = (IList)typeof(ScenarioWaitForNode).GetField("cases", System.Reflection.BindingFlags.Instance
                                                                             | System.Reflection.BindingFlags.NonPublic).GetValue(waitLook);
            cases.RemoveAt(0);

            var problems = ScenarioGraphValidation.Check(graph);

            Assert.That(problems.Single().NodeId, Is.EqualTo(waitLook.Id));
            Assert.That(problems.Single().Message, Does.Contain("no longer has"));
        }

        [Test]
        public void GraphFile_SceneObjectVariableWithoutSetOnObject_IsVariableProblem()
        {
            var graph = new ScenarioGraph();
            var silhouette = graph.AddVariable(new ScenarioVariableDefinition("Silhouette", ScenarioVariableKind.Object));
            silhouette.ObjectType = typeof(GameObject);

            Assert.That(ScenarioGraphValidation.Check(graph), Is.Empty, "An embedded graph keeps scene objects itself");

            var problem = ScenarioGraphValidation.Check(graph, isGraphFile: true).Single();
            Assert.That(problem.VariableId, Is.EqualTo(silhouette.Id));
            Assert.That(problem.NodeId, Is.Zero);

            silhouette.IsExposed = true;
            Assert.That(ScenarioGraphValidation.Check(graph, isGraphFile: true), Is.Empty);
        }

        [Test]
        public void GraphFile_ExposedObjectWithoutValueOnObject_IsVariableProblem()
        {
            var graph = new ScenarioGraph();
            var lamp = graph.AddVariable(new ScenarioVariableDefinition("Lamp", ScenarioVariableKind.Object) { IsExposed = true });
            lamp.ObjectType = typeof(Light);
            var owner = UnityEditor.EditorUtility.CreateGameObjectWithHideFlags("Variable Problems Test", HideFlags.HideAndDontSave);

            try
            {
                var bindings = new ScenarioBindingsEditing(new UnityEditor.SerializedObject(owner.AddComponent<Scenario>()));

                var problem = ScenarioGraphValidation.CheckVariables(graph, true, bindings).Single();
                Assert.That(problem.VariableId, Is.EqualTo(lamp.Id));
                Assert.That(problem.Message, Does.Contain(owner.name));

                bindings.AddBinding(lamp.Id).FindPropertyRelative("objectValue").objectReferenceValue = owner.AddComponent<Light>();
                bindings.SerializedScenario.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(ScenarioGraphValidation.CheckVariables(graph, true, bindings), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void VariableProblem_SameRuleAsFits()
        {
            var number = new ScenarioVariableDefinition("Delay", ScenarioVariableKind.Number);

            Assert.That(ScenarioGraphValidation.VariableProblemOf(number, typeof(float)), Is.Null);
            Assert.That(ScenarioGraphValidation.VariableProblemOf(number, typeof(bool)), Is.Not.Null);
            Assert.That(ScenarioGraphValidation.VariableProblemOf(null, typeof(float)), Does.Contain("not found"));
        }
    }


    /// <summary>
    /// Тестовый шаг с полями «значение или переменная»; не переданное поле пусто и проверкой не смотрится.
    /// Конструктор с параметрами — в меню «+» и поиск нод не попадает.
    /// </summary>
    [Serializable]
    public class ScenarioValueTestAction : ScenarioInstantAction
    {
        [ScenarioLabel("Lamp")] [SerializeField] private ScenarioValue<Light> lamp;
        [ScenarioLabel("Delay")] [SerializeField] private ScenarioValue<float> delay;


        public ScenarioValueTestAction(ScenarioValue<Light> lamp = null, ScenarioValue<float> delay = null)
        {
            this.lamp = lamp;
            this.delay = delay;
        }


        protected override void Apply(ScenarioRun run)
        {
        }
    }
}
