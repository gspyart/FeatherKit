using System;
using System.Collections.Generic;
using System.IO;
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
    /// Находки тестировщика редактора этапа 2: копипаст, граф в файл и обратно.
    /// Тест с QA_Found ловит баг, пока его не починят; остальные — проверки, которые прошли.
    /// </summary>
    public class ScenarioEditorQaStage2Tests
    {
        private const string TempFolder = "Assets/ScenarioEditorQaStage2Temp";
        private const string StepPath = "graph.nodes.Array.data[0].steps.actions.Array.data[0]";

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


        // ── Make Graph File: забытые объекты в полях ─────────────────────

        [Test]
        [Category("QA_Found")]
        public void MakeGraphFile_TargetOnThisObjectWithLeftoverObject_StaysThisObject()
        {
            var lamp = SceneObject("Old Lamp");
            var scenario = ScenarioWithFieldsStep();
            SetStep(scenario, "target.kind", property => property.enumValueIndex = (int)ScenarioTargetKind.Self);
            SetStep(scenario, "target.specificObject", property => property.objectReferenceValue = lamp);

            Assert.That(ScenarioGraphFileConverter.MakeGraphFile(scenario, NewAssetPath("Target")), Is.Empty);

            var kind = StepProperty(scenario.GraphAsset, "target.kind").enumValueIndex;
            Assert.That((ScenarioTargetKind)kind, Is.EqualTo(ScenarioTargetKind.Self),
                "“This Object” with an object left from an earlier choice became “Variable” — in the game it acts on the old object");
            Assert.That(scenario.GraphAsset.Graph.Variables, Is.Empty, "A variable appeared for an object the field doesn't use");
        }

        [Test]
        [Category("QA_Found")]
        public void MakeGraphFile_PointOnWorldPointWithLeftoverTransform_StaysWorldPoint()
        {
            var lamp = SceneObject("Old Lamp");
            var scenario = ScenarioWithFieldsStep();
            SetStep(scenario, "point.kind", property => property.enumValueIndex = (int)ScenarioPointKind.WorldPoint);
            SetStep(scenario, "point.targetTransform", property => property.objectReferenceValue = lamp.transform);

            Assert.That(ScenarioGraphFileConverter.MakeGraphFile(scenario, NewAssetPath("Point")), Is.Empty);

            var kind = StepProperty(scenario.GraphAsset, "point.kind").enumValueIndex;
            Assert.That((ScenarioPointKind)kind, Is.EqualTo(ScenarioPointKind.WorldPoint));
        }

        [Test]
        [Category("QA_Found")]
        public void MakeGraphFile_ValueAlreadyUsesVariable_KeepsTheChosenVariable()
        {
            var lamp = SceneObject("Old Lamp").AddComponent<Light>();
            var scenario = ScenarioWithFieldsStep();
            var chosen = scenario.Graph.AddVariable(new ScenarioVariableDefinition("Chosen Lamp", ScenarioVariableKind.Object) { ObjectType = typeof(Light) });
            SetStep(scenario, "lamp.variableId", property => property.stringValue = chosen.Id);
            SetStep(scenario, "lamp.value", property => property.objectReferenceValue = lamp);

            Assert.That(ScenarioGraphFileConverter.MakeGraphFile(scenario, NewAssetPath("Value")), Is.Empty);

            Assert.That(StepProperty(scenario.GraphAsset, "lamp.variableId").stringValue, Is.EqualTo(chosen.Id),
                "The field read “Chosen Lamp”; after Make Graph File it reads a new variable made from the leftover object");
        }

        // Падает внутри MakeGraphFile: список чистится, пока по нему идёт перебор SerializedProperty.
        [Test]
        [Category("QA_Found")]
        public void MakeGraphFile_ObjectList_BecomesBoundListVariable()
        {
            var first = SceneObject("Lamp A").AddComponent<Light>();
            var second = SceneObject("Lamp B").AddComponent<Light>();
            var scenario = ScenarioWithFieldsStep();
            SetStep(scenario, "lights.items", property =>
            {
                property.arraySize = 2;
                property.GetArrayElementAtIndex(0).objectReferenceValue = first;
                property.GetArrayElementAtIndex(1).objectReferenceValue = second;
            });

            Assert.That(ScenarioGraphFileConverter.MakeGraphFile(scenario, NewAssetPath("List")), Is.Empty);

            var variable = scenario.GraphAsset.Graph.FindVariable(StepProperty(scenario.GraphAsset, "lights.variableId").stringValue);
            Assert.That(variable.Kind, Is.EqualTo(ScenarioVariableKind.ObjectList));
            Assert.That(variable.ObjectType, Is.EqualTo(typeof(Light)));
            Assert.That(scenario.VariableBindings.Single().Value.ObjectListValue, Is.EqualTo(new Object[] { first, second }));
        }


        // Та же причина: начальное значение-список переменной чистится посреди перебора.
        [Test]
        [Category("QA_Found")]
        public void MakeGraphFile_ObjectListVariableWithSceneObjects_BecomesSetOnObject()
        {
            var first = SceneObject("Lamp A").AddComponent<Light>();
            var second = SceneObject("Lamp B").AddComponent<Light>();
            var scenario = ScenarioWithFieldsStep();
            var lamps = scenario.Graph.AddVariable(new ScenarioVariableDefinition("Lamps", ScenarioVariableKind.ObjectList) { ObjectType = typeof(Light) });
            lamps.InitialValue.ObjectListValue = new List<Object> { first, second };

            Assert.That(ScenarioGraphFileConverter.MakeGraphFile(scenario, NewAssetPath("VariableList")), Is.Empty);

            var fileVariable = scenario.GraphAsset.Graph.FindVariable(lamps.Id);
            Assert.That(fileVariable.IsExposed, Is.True);
            Assert.That(fileVariable.InitialValue.ObjectListValue, Is.Empty);
            Assert.That(scenario.VariableBindings.Single().Value.ObjectListValue, Is.EqualTo(new Object[] { first, second }));
        }


        [Test]
        public void MakeGraphFile_Undo_BringsEmbeddedGraphBack()
        {
            var lamp = SceneObject("Lamp");
            var scenario = ScenarioWithFieldsStep();
            SetStep(scenario, "target.kind", property => property.enumValueIndex = (int)ScenarioTargetKind.Specific);
            SetStep(scenario, "target.specificObject", property => property.objectReferenceValue = lamp);

            Undo.IncrementCurrentGroup();
            Assume.That(ScenarioGraphFileConverter.MakeGraphFile(scenario, NewAssetPath("UndoMake")), Is.Empty);
            Undo.PerformUndo();

            Assert.That(scenario.GraphSource, Is.EqualTo(ScenarioGraphSource.Embedded));
            Assert.That(scenario.Graph.Nodes, Has.Count.EqualTo(1));
            Assert.That(StepProperty(scenario, "target.specificObject").objectReferenceValue, Is.SameAs(lamp));
            Assert.That(scenario.VariableBindings, Is.Empty);
        }

        [Test]
        public void EmbedInObject_SharedFile_OtherObjectKeepsFileAndBindings()
        {
            var first = SceneObject("Lamp A");
            var second = SceneObject("Lamp B");
            var owner = ScenarioWithFieldsStep();
            SetStep(owner, "target.kind", property => property.enumValueIndex = (int)ScenarioTargetKind.Specific);
            SetStep(owner, "target.specificObject", property => property.objectReferenceValue = first);
            Assume.That(ScenarioGraphFileConverter.MakeGraphFile(owner, NewAssetPath("Shared")), Is.Empty);
            var file = owner.GraphAsset;
            var variableId = file.Graph.Variables.Single().Id;

            var other = SceneObject("Other Owner").AddComponent<Scenario>();
            var serializedOther = new SerializedObject(other);
            serializedOther.FindProperty("graphSource").enumValueIndex = (int)ScenarioGraphSource.Asset;
            serializedOther.FindProperty("graphAsset").objectReferenceValue = file;
            serializedOther.ApplyModifiedPropertiesWithoutUndo();
            new ScenarioBindingsEditing(serializedOther).AddBinding(variableId).boxedValue = new ScenarioVariableValue { ObjectValue = second };
            serializedOther.ApplyModifiedPropertiesWithoutUndo();

            ScenarioGraphFileConverter.EmbedInObject(owner);

            Assert.That(owner.Graph.FindVariable(variableId).InitialValue.ObjectValue, Is.SameAs(first));
            Assert.That(other.GraphAsset, Is.SameAs(file));
            Assert.That(other.VariableBindings.Single().Value.ObjectValue, Is.SameAs(second));
            Assert.That(file.Graph.FindVariable(variableId).IsExposed, Is.True, "The file itself is untouched");
        }


        // ── Пропавшие классы при копии графа ─────────────────────────────

        // Скопировать данные ноды без класса Unity не умеет никаким способом — вместо тихой потери отказ, объект не меняется.
        [Test]
        [Category("QA_Found")]
        public void EmbedInObject_GraphFileWithNodeOfMissingClass_RefusesWithoutLoss()
        {
            var graph = ScenarioTestGraphs.WhenThenSteps();
            var doomed = graph.AddNode(new ScenarioStepsNode { CustomTitle = "Doomed" }, new Vector2(600f, 0f));
            graph.Connect(graph.Nodes[1], ScenarioStepsNode.Done, doomed, ScenarioStepsNode.In);
            var asset = GraphFileOnDiskWithMissingNode(graph, "Doomed");
            Assume.That(SerializationUtility.HasManagedReferencesWithMissingTypes(asset), "Setup: the node's class is missing");

            var scenario = SceneObject("Scenario Owner").AddComponent<Scenario>();
            var serializedScenario = new SerializedObject(scenario);
            serializedScenario.FindProperty("graphSource").enumValueIndex = (int)ScenarioGraphSource.Asset;
            serializedScenario.FindProperty("graphAsset").objectReferenceValue = asset;
            serializedScenario.ApplyModifiedPropertiesWithoutUndo();

            var refusals = ScenarioGraphFileConverter.EmbedInObject(scenario);

            Assert.That(refusals, Has.Count.EqualTo(1), "Copy drops the node whose class is missing — it must refuse, not lose it silently");
            Assert.That(refusals[0], Does.Contain("ScenarioQaGoneNode"));
            Assert.That(scenario.GraphSource, Is.EqualTo(ScenarioGraphSource.Asset), "The object still uses the file");
            Assert.That(SerializationUtility.HasManagedReferencesWithMissingTypes(asset), Is.True, "The file keeps the node");
        }


        // ── Копипаст ─────────────────────────────────────────────────────

        [Test]
        public void Paste_WaitForWithCaseWires_WiresOfCasesComeAlong()
        {
            var asset = GraphFile(ScenarioTestGraphs.SilhouetteWaitFor());
            var editing = new ScenarioGraphEditing(ScenarioGraphHost.For(asset));
            var graph = asset.Graph;
            var wireCount = graph.Wires.Count;

            var content = RoundTrip(ScenarioGraphClipboard.Collect(editing.Host, graph.Nodes.Select(node => node.Id).ToList(), new string[0], new string[0]));
            ScenarioGraphClipboard.Paste(editing, content, new Vector2(0f, 500f));

            Assert.That(graph.Wires.Count, Is.EqualTo(wireCount * 2));
        }

        [Test]
        public void Paste_Undo_RemovesPastedNodesAndCreatedVariables()
        {
            var source = ScenarioTestGraphs.ForEachOverList();
            var asset = GraphFile(new ScenarioGraph());
            var editing = new ScenarioGraphEditing(ScenarioGraphHost.For(asset));
            var content = RoundTrip(ScenarioGraphClipboard.Collect(ScenarioGraphHost.For(GraphFile(source)), source.Nodes.Select(node => node.Id).ToList(),
                new string[0], new string[0]));

            Undo.IncrementCurrentGroup();
            ScenarioGraphClipboard.Paste(editing, content, Vector2.zero);
            Assume.That(asset.Graph.Nodes, Is.Not.Empty);
            Undo.PerformUndo();

            Assert.That(asset.Graph.Nodes, Is.Empty);
            Assert.That(asset.Graph.Variables, Is.Empty);
        }


        // ── Окно ─────────────────────────────────────────────────────────

        // Шапка окна спрашивает это каждые полсекунды: граф-файл не на диске (собран кодом) — исключение в консоль раз за разом.
        [Test]
        [Category("QA_Found")]
        public void Host_GraphFileNotSavedToDisk_IsDirtyDoesNotThrow()
        {
            var host = ScenarioGraphHost.For(GraphFile(new ScenarioGraph()));

            Assert.That(() => host.IsDirty, Throws.Nothing);
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private Scenario ScenarioWithFieldsStep()
        {
            var scenario = SceneObject("Scenario Owner").AddComponent<Scenario>();
            var steps = scenario.Graph.AddNode(new ScenarioStepsNode(), Vector2.zero);
            steps.AddStep(new ScenarioQaStage2FieldsAction(0));

            return scenario;
        }

        private static void SetStep(Scenario scenario, string relativePath, Action<SerializedProperty> change)
        {
            var serializedScenario = new SerializedObject(scenario);
            change(serializedScenario.FindProperty($"{StepPath}.{relativePath}"));
            serializedScenario.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty StepProperty(Object graphOwner, string relativePath)
        {
            return new SerializedObject(graphOwner).FindProperty($"{StepPath}.{relativePath}");
        }

        private ScenarioGraphAsset GraphFile(ScenarioGraph graph)
        {
            var asset = ScriptableObject.CreateInstance<ScenarioGraphAsset>();
            asset.hideFlags = HideFlags.HideAndDontSave;
            asset.Graph = graph;
            createdObjects.Add(asset);

            return asset;
        }

        // Класс одной ноды «переименован»: в тексте файла у неё другое имя класса.
        private static ScenarioGraphAsset GraphFileOnDiskWithMissingNode(ScenarioGraph graph, string nodeTitle)
        {
            var path = NewAssetPath("Missing");
            var asset = ScriptableObject.CreateInstance<ScenarioGraphAsset>();
            asset.Graph = graph;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            var fullPath = Path.GetFullPath(path);
            var text = File.ReadAllText(fullPath);
            const string typeText = "type: {class: ScenarioStepsNode";
            var typeIndex = text.LastIndexOf(typeText, text.IndexOf($"customTitle: {nodeTitle}", StringComparison.Ordinal), StringComparison.Ordinal);
            File.WriteAllText(fullPath, text.Substring(0, typeIndex) + "type: {class: ScenarioQaGoneNode" + text.Substring(typeIndex + typeText.Length));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return AssetDatabase.LoadAssetAtPath<ScenarioGraphAsset>(path);
        }

        private GameObject SceneObject(string name)
        {
            var gameObject = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
            createdObjects.Add(gameObject);

            return gameObject;
        }

        private ScenarioClipboardContent RoundTrip(ScenarioClipboardContent content)
        {
            var copy = ScenarioClipboardContent.FromJson(content.ToJson());
            createdObjects.Add(content);
            createdObjects.Add(copy);

            return copy;
        }

        private static string NewAssetPath(string name)
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "ScenarioEditorQaStage2Temp");

            return $"{TempFolder}/{name}.asset";
        }
    }


    /// <summary>Шаг тестировщика: «значение или переменная», цель, точка и список объектов. В поиск не попадает — нет пустого конструктора.</summary>
    [Serializable]
    public class ScenarioQaStage2FieldsAction : ScenarioInstantAction
    {
        [SerializeField] private ScenarioValue<Light> lamp = new ScenarioValue<Light>();
        [SerializeField] private ScenarioTarget target = new ScenarioTarget();
        [SerializeField] private ScenarioPoint point = new ScenarioPoint();
        [SerializeField] private ScenarioObjectList<Light> lights = new ScenarioObjectList<Light>();


        public ScenarioQaStage2FieldsAction(int unused)
        {
        }


        protected override void Apply(ScenarioRun run)
        {
        }
    }
}
