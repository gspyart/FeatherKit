using System;
using System.Collections.Generic;
using System.Linq;
using FeatherKit.EditorTools;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Редактор этапа 2 без окна на экране: копипаст, рамки, граф в файл и обратно, поле значения по виду переменной,
    /// проверки новых полей, свёрнутые ноды, описания и цвета категорий.
    /// </summary>
    public class ScenarioEditorStage2Tests
    {
        private const string TempFolder = "Assets/ScenarioEditorTestTemp";

        private readonly List<Object> createdObjects = new List<Object>();


        [TearDown]
        public void DestroyCreated()
        {
            foreach (var created in createdObjects.Where(created => created != null))
            {
                Undo.ClearUndo(created);
                Object.DestroyImmediate(created);
            }

            createdObjects.Clear();

            if (AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.DeleteAsset(TempFolder);
        }


        // ── Копипаст ─────────────────────────────────────────────────────

        [Test]
        public void Paste_SameGraph_NewIdsWiresBetweenCopiesAndOwnVariables()
        {
            var editing = EditingOf(GraphFileWith(ScenarioTestGraphs.ForEachOverList()));
            var graph = editing.Host.Graph;
            var nodeIds = graph.Nodes.Select(node => node.Id).ToList();
            var forEach = ScenarioTestGraphs.FindNode<ScenarioForEachNode>(graph, ScenarioTestGraphs.ForEachTitle);
            var each = ScenarioTestGraphs.FindNode<ScenarioStepsNode>(graph, ScenarioTestGraphs.EachTitle);

            var content = RoundTrip(ScenarioGraphClipboard.Collect(editing.Host, new[] { forEach.Id, each.Id }, new string[0], new string[0]));
            var pastedIds = ScenarioGraphClipboard.Paste(editing, content, new Vector2(0f, 300f));

            Assert.That(pastedIds, Has.Count.EqualTo(2));
            Assert.That(pastedIds.Intersect(nodeIds), Is.Empty, "Pasted nodes get new ids");
            Assert.That(graph.Variables, Has.Count.EqualTo(2), "Own variables are not duplicated");

            var pastedForEach = graph.Nodes.OfType<ScenarioForEachNode>().Single(node => pastedIds.Contains(node.Id));
            Assert.That(pastedForEach, Is.Not.SameAs(forEach), "A deep copy, not the same object");
            Assert.That(pastedForEach.List.VariableId, Is.EqualTo(forEach.List.VariableId));
            Assert.That(graph.Wires.Count(wire => pastedIds.Contains(wire.FromNodeId) && pastedIds.Contains(wire.ToNodeId)), Is.EqualTo(1),
                "Only the wire between the copied nodes comes along");
        }

        [Test]
        public void Paste_OtherGraph_CreatesMissingVariablesWithUniqueNames()
        {
            var source = ScenarioTestGraphs.ForEachOverList();
            var forEach = ScenarioTestGraphs.FindNode<ScenarioForEachNode>(source, ScenarioTestGraphs.ForEachTitle);
            var target = new ScenarioGraph();
            target.AddVariable(new ScenarioVariableDefinition(ScenarioTestGraphs.LampsVariableName, ScenarioVariableKind.Flag));
            var editing = EditingOf(GraphFileWith(target));

            var content = ScenarioGraphClipboard.Collect(ScenarioGraphHost.For(GraphFileWith(source)), new[] { forEach.Id }, new string[0], new string[0]);
            ScenarioGraphClipboard.Paste(editing, RoundTrip(content), Vector2.zero);

            var lamps = target.FindVariable(forEach.List.VariableId);
            Assert.That(lamps, Is.Not.Null, "The list variable came along");
            Assert.That(lamps.Kind, Is.EqualTo(ScenarioVariableKind.ObjectList));
            Assert.That(lamps.Name, Is.Not.EqualTo(ScenarioTestGraphs.LampsVariableName), "Name taken in the target — renamed");
            Assert.That(target.FindVariable(forEach.Item.VariableId), Is.Not.Null);
        }

        [Test]
        public void Paste_IntoGraphFile_ClearsSceneObjects()
        {
            var lamp = SceneObject("Lamp").AddComponent<Light>();
            var source = new ScenarioGraph();
            var steps = source.AddNode(new ScenarioStepsNode(), Vector2.zero);
            steps.AddStep(new ScenarioTestRecordPointAction(new ScenarioPoint(ScenarioPointKind.Object) { TargetTransform = lamp.transform }));
            var target = GraphFileWith(new ScenarioGraph());

            var content = ScenarioGraphClipboard.Collect(ScenarioGraphHost.For(GraphFileWith(source)), new[] { steps.Id }, new string[0], new string[0]);
            ScenarioGraphClipboard.Paste(EditingOf(target), RoundTrip(content), Vector2.zero);

            var pasted = (ScenarioTestRecordPointAction)FirstStepOf(target);
            Assert.That(pasted.Point.TargetTransform, Is.Null);
        }


        [Test]
        public void Paste_IntoObjectGraph_KeepsSceneObjects()
        {
            var lamp = SceneObject("Lamp").AddComponent<Light>();
            var scenario = SceneObject("Scenario Owner").AddComponent<Scenario>();
            var steps = scenario.Graph.AddNode(new ScenarioStepsNode(), Vector2.zero);
            steps.AddStep(new ScenarioTestRecordPointAction(new ScenarioPoint(ScenarioPointKind.Object) { TargetTransform = lamp.transform }));
            var editing = new ScenarioGraphEditing(ScenarioGraphHost.For(scenario));

            var content = ScenarioGraphClipboard.Collect(editing.Host, new[] { steps.Id }, new string[0], new string[0]);
            var pastedIds = ScenarioGraphClipboard.Paste(editing, RoundTrip(content), new Vector2(0f, 200f));

            var pasted = (ScenarioTestRecordPointAction)editing.Host.NodeProperty(pastedIds[0])
                .FindPropertyRelative("steps.actions.Array.data[0]").managedReferenceValue;
            Assert.That(pasted.Point.TargetTransform, Is.SameAs(lamp.transform), "Text loses scene objects — they come back by id");
        }


        // ── Рамки ────────────────────────────────────────────────────────

        [Test]
        public void Groups_NodeLivesInOneGroup_RemovingGroupKeepsNodes()
        {
            var editing = EditingOf(GraphFileWith(ScenarioTestGraphs.GateAndOnce()));
            var graph = editing.Host.Graph;
            var gate = ScenarioTestGraphs.FindNode<ScenarioGateNode>(graph, ScenarioTestGraphs.GateTitle);
            var once = ScenarioTestGraphs.FindNode<ScenarioOnceNode>(graph, ScenarioTestGraphs.OnceTitle);

            var first = editing.Groups.Add(new[] { gate.Id, once.Id });
            var second = editing.Groups.Add(new[] { once.Id }, "Second");
            editing.Groups.Rename(first.Id, "Lever");

            Assert.That(first.NodeIds, Is.EqualTo(new[] { gate.Id }), "Once moved to the second group");
            Assert.That(second.NodeIds, Is.EqualTo(new[] { once.Id }));
            Assert.That(first.Title, Is.EqualTo("Lever"));

            editing.Groups.Remove(new[] { first.Id, second.Id });

            Assert.That(graph.Groups, Is.Empty);
            Assert.That(graph.FindNode(gate.Id), Is.Not.Null, "Nodes stay");
        }

        [Test]
        public void Paste_GroupComesWithItsNodes()
        {
            var editing = EditingOf(GraphFileWith(ScenarioTestGraphs.GateAndOnce()));
            var graph = editing.Host.Graph;
            var gate = ScenarioTestGraphs.FindNode<ScenarioGateNode>(graph, ScenarioTestGraphs.GateTitle);
            var group = editing.Groups.Add(new[] { gate.Id }, "Lever");

            var pastedIds = ScenarioGraphClipboard.Paste(editing,
                RoundTrip(ScenarioGraphClipboard.Collect(editing.Host, group.NodeIds, new string[0], new[] { group.Id })), new Vector2(0f, 400f));

            var pastedGroup = graph.Groups.Single(candidate => candidate != group);
            Assert.That(pastedGroup.Title, Is.EqualTo("Lever"));
            Assert.That(pastedGroup.NodeIds, Is.EqualTo(pastedIds));
        }


        // ── Граф в файл и обратно ────────────────────────────────────────

        [Test]
        public void MakeGraphFile_SceneObjectsBecomeBoundVariables_EmbedBringsThemBack()
        {
            var lamp = SceneObject("Lamp").AddComponent<Light>();
            var scenario = SceneObject("Scenario Owner").AddComponent<Scenario>();
            var steps = scenario.Graph.AddNode(new ScenarioStepsNode(), Vector2.zero);
            steps.AddStep(new ScenarioTestRecordPointAction(new ScenarioPoint(ScenarioPointKind.Object) { TargetTransform = lamp.transform }));

            var refusals = ScenarioGraphFileConverter.MakeGraphFile(scenario, NewAssetPath("Made"));

            Assert.That(refusals, Is.Empty);
            Assert.That(scenario.GraphSource, Is.EqualTo(ScenarioGraphSource.Asset));

            var fileGraph = scenario.GraphAsset.Graph;
            var point = ((ScenarioTestRecordPointAction)FirstStepOf(scenario.GraphAsset)).Point;
            var variable = fileGraph.FindVariable(point.VariableId);

            Assert.That(point.Kind, Is.EqualTo(ScenarioPointKind.Variable));
            Assert.That(point.TargetTransform, Is.Null, "The file keeps no scene object");
            Assert.That(variable.IsExposed, Is.True);
            Assert.That(scenario.VariableBindings.Single().Value.ObjectValue, Is.SameAs(lamp.transform), "The object went to this object's binding");

            ScenarioGraphFileConverter.EmbedInObject(scenario);

            Assert.That(scenario.GraphSource, Is.EqualTo(ScenarioGraphSource.Embedded));
            var embedded = scenario.Graph.FindVariable(variable.Id);
            Assert.That(embedded.IsExposed, Is.False);
            Assert.That(embedded.InitialValue.ObjectValue, Is.SameAs(lamp.transform), "The binding became the initial value");
            Assert.That(scenario.VariableBindings, Is.Empty);
        }

        [Test]
        public void MakeGraphFile_PlainSceneField_RefusesAndChangesNothing()
        {
            var lamp = SceneObject("Lamp").AddComponent<Light>();
            var scenario = SceneObject("Scenario Owner").AddComponent<Scenario>();
            var steps = scenario.Graph.AddNode(new ScenarioStepsNode(), Vector2.zero);
            steps.AddStep(new ScenarioTestPlainLightAction(lamp));
            var path = NewAssetPath("Refused");

            var refusals = ScenarioGraphFileConverter.MakeGraphFile(scenario, path);

            Assert.That(refusals, Has.Count.EqualTo(1));
            Assert.That(refusals[0], Does.Contain("Lamp"));
            Assert.That(scenario.GraphSource, Is.EqualTo(ScenarioGraphSource.Embedded));
            Assert.That(AssetDatabase.LoadAssetAtPath<ScenarioGraphAsset>(path), Is.Null);
        }


        // ── Значение по виду переменной ──────────────────────────────────

        [Test]
        public void VariableValueDrawer_FindsVariableNextToIt_AndHidesWhenReplaced()
        {
            var graph = new ScenarioGraph();
            var color = graph.AddVariable(new ScenarioVariableDefinition("Tint", ScenarioVariableKind.Color));
            var other = graph.AddVariable(new ScenarioVariableDefinition("Other Tint", ScenarioVariableKind.Color));
            var steps = graph.AddNode(new ScenarioStepsNode(), Vector2.zero);
            var block = new ScenarioTestSetVariableAction(color.Id);
            steps.AddStep(block);
            var host = ScenarioGraphHost.For(GraphFileWith(graph));
            var value = host.NodeProperty(steps.Id).FindPropertyRelative("steps.actions.Array.data[0].value");
            var drawer = new ScenarioVariableValueDrawer();

            Assert.That(ScenarioVariableValueDrawer.VariableOf(value), Is.SameAs(color), "Through the “variable” field");
            Assert.That(ScenarioVariableValueDrawer.VariableOf(host.VariableProperty(other.Id).FindPropertyRelative("initialValue")),
                Is.SameAs(other), "A variable's own initial value");
            Assert.That(drawer.GetPropertyHeight(value, GUIContent.none), Is.GreaterThan(0f));

            block.SourceVariable.VariableId = other.Id;
            host.SerializedGraph.Update();
            value = host.NodeProperty(steps.Id).FindPropertyRelative("steps.actions.Array.data[0].value");

            Assert.That(drawer.GetPropertyHeight(value, GUIContent.none), Is.EqualTo(0f), "Value from another variable — no own value field");
        }

        [Test]
        public void NewVariableFromField_InGraphFile_SceneTypeIsSetOnObject()
        {
            var asset = GraphFileWith(new ScenarioGraph());
            var serializedAsset = new SerializedObject(asset);

            var lights = ScenarioVariablePicking.CreateVariable(serializedAsset, ScenarioVariableKind.ObjectList, typeof(Light), "Lamps", null);
            var count = ScenarioVariablePicking.CreateVariable(serializedAsset, ScenarioVariableKind.Counter, null, "Count",
                value => value.CounterValue = 3);

            Assert.That(lights.IsExposed, Is.True, "Lights live in the scene — only through Set on Object");
            Assert.That(ScenarioVariableKinds.Fits(lights, typeof(List<Light>)), Is.True);
            Assert.That(count.IsExposed, Is.False);
            Assert.That(count.InitialValue.CounterValue, Is.EqualTo(3), "The field's value became the initial value");
        }


        // ── Проверки ─────────────────────────────────────────────────────

        [Test]
        public void Validation_NewFields()
        {
            var graph = new ScenarioGraph();
            var flag = graph.AddVariable(new ScenarioVariableDefinition("Flag", ScenarioVariableKind.Flag));
            var steps = graph.AddNode(new ScenarioStepsNode(), Vector2.zero);
            steps.AddStep(new ScenarioTestStorePointAction(new ScenarioPoint(ScenarioPointKind.Variable) { VariableId = flag.Id },
                new ScenarioVariableSlot<Vector3>()));

            var messages = ScenarioGraphValidation.Check(graph).Select(problem => problem.Message).ToList();

            Assert.That(messages, Has.Some.Contains("nowhere to write"));
            Assert.That(messages, Has.Some.Contains("a point needs an Object or a Vector3"));
        }

        [Test]
        public void Validation_ObjectListOfSceneObjectsInGraphFile_NeedsSetOnObject()
        {
            var graph = ScenarioTestGraphs.ForEachOverList();
            graph.Variables.Single(variable => variable.Kind == ScenarioVariableKind.ObjectList).IsExposed = false;

            var problems = ScenarioGraphValidation.CheckVariables(graph, isGraphFile: true, bindings: null);

            Assert.That(problems.Select(problem => problem.Message), Has.Some.Contains("turn on Set on Object"));
        }


        // ── Прочее ───────────────────────────────────────────────────────

        [Test]
        public void CollapsedNodes_RememberedPerOwner()
        {
            var first = GraphFileWith(new ScenarioGraph());
            var second = GraphFileWith(new ScenarioGraph());

            ScenarioCollapsedNodes.Remember(first, 7, true);

            Assert.That(ScenarioCollapsedNodes.IsCollapsed(first, 7), Is.True);
            Assert.That(ScenarioCollapsedNodes.IsCollapsed(second, 7), Is.False);

            ScenarioCollapsedNodes.Remember(first, 7, false);
            Assert.That(ScenarioCollapsedNodes.IsCollapsed(first, 7), Is.False);
        }

        [Test]
        public void Search_ShowsBlockDescription()
        {
            var gate = ScenarioNodeSearch.CollectOptions().Single(option => option.CreatedType == typeof(ScenarioGateNode));

            Assert.That(gate.Description, Is.Not.Empty);
            Assert.That(gate.Description, Is.EqualTo(ScenarioBlockAttribute.DescriptionOf(typeof(ScenarioGateNode))));
        }

        [Test]
        public void NewCategories_HaveDistinctColors()
        {
            var categories = new[] { "Space", "Lists", "Physics", "Rendering", "Debug", "Variables", "Object", "Transform", "Trigger" };
            var colors = categories.Select(category => ScenarioBlockStyle.ColorOfCategory(category)).ToList();

            Assert.That(colors.Distinct().Count(), Is.EqualTo(categories.Length));
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private ScenarioGraphAsset GraphFileWith(ScenarioGraph graph)
        {
            var asset = ScriptableObject.CreateInstance<ScenarioGraphAsset>();
            asset.hideFlags = HideFlags.HideAndDontSave;
            asset.Graph = graph;
            createdObjects.Add(asset);

            return asset;
        }

        // Скрытый и несохраняемый: тест не должен помечать изменённой открытую сцену.
        private GameObject SceneObject(string name)
        {
            var gameObject = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
            createdObjects.Add(gameObject);

            return gameObject;
        }

        private static object FirstStepOf(Object graphOwner)
        {
            return new SerializedObject(graphOwner).FindProperty("graph.nodes.Array.data[0].steps.actions.Array.data[0]").managedReferenceValue;
        }

        private static ScenarioGraphEditing EditingOf(ScenarioGraphAsset asset)
        {
            return new ScenarioGraphEditing(ScenarioGraphHost.For(asset));
        }

        // Как через системный буфер: в текст и обратно.
        private ScenarioClipboardContent RoundTrip(ScenarioClipboardContent content)
        {
            var copy = ScenarioClipboardContent.FromJson(content.ToJson());
            createdObjects.Add(content);
            createdObjects.Add(copy);

            return copy;
        }

        private string NewAssetPath(string name)
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "ScenarioEditorTestTemp");

            return $"{TempFolder}/{name}.asset";
        }
    }


    /// <summary>Тестовый шаг с точкой: в копипасте и конвертере в файл точка на объекте сцены.</summary>
    [Serializable]
    public class ScenarioTestRecordPointAction : ScenarioInstantAction
    {
        [SerializeField] private ScenarioPoint point;


        public ScenarioTestRecordPointAction(ScenarioPoint point)
        {
            this.point = point;
        }


        public ScenarioPoint Point => point;


        protected override void Apply(ScenarioRun run)
        {
        }
    }


    /// <summary>Тестовый шаг с объектом сцены в обычном поле: такой граф в файл не переносится.</summary>
    [Serializable]
    public class ScenarioTestPlainLightAction : ScenarioInstantAction
    {
        [SerializeField] private Light light;


        public ScenarioTestPlainLightAction(Light light)
        {
            this.light = light;
        }


        protected override void Apply(ScenarioRun run)
        {
        }
    }


    /// <summary>Как будет у Set Variable: выбор переменной, значение её вида и «взять из другой переменной».</summary>
    [Serializable]
    public class ScenarioTestSetVariableAction : ScenarioInstantAction
    {
        [SerializeField] private ScenarioTestVariableChoice variable = new ScenarioTestVariableChoice();
        [SerializeField] private ScenarioVariableValue value = new ScenarioVariableValue();
        [SerializeField] private ScenarioTestVariableChoice sourceVariable = new ScenarioTestVariableChoice();


        public ScenarioTestSetVariableAction(string variableId)
        {
            variable.VariableId = variableId;
        }


        public ScenarioTestVariableChoice SourceVariable => sourceVariable;


        protected override void Apply(ScenarioRun run)
        {
        }
    }


    /// <summary>Заменитель ScenarioVariableChoice до стыковки: та же форма данных — одно поле variableId.</summary>
    [Serializable]
    public class ScenarioTestVariableChoice
    {
        [SerializeField] private string variableId;


        public string VariableId
        {
            get => variableId;
            set => variableId = value;
        }
    }
}
