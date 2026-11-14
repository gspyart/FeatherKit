using System.Linq;
using FeatherKit.EditorTools;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Холст графа без окна на экране: догоняет данные, зум в общих пределах, заметки, иконки, привязки, пропавшие ноды.
    /// Вёрстку не проверяет — только логику.
    /// </summary>
    public class ScenarioEditorCanvasTests
    {
        private const string TempFolder = "Assets/ScenarioEditorCanvasTestTemp";

        private ScenarioGraphWindow window;
        private ScenarioGraphView graphView;
        private ScenarioGraphAsset graphFile;


        [SetUp]
        public void CreateCanvas()
        {
            graphFile = ScriptableObject.CreateInstance<ScenarioGraphAsset>();
            graphFile.Graph = ScenarioTestGraphs.WithVariables();
            window = ScriptableObject.CreateInstance<ScenarioGraphWindow>();
            graphView = new ScenarioGraphView(window);
            graphView.Show(ScenarioGraphHost.For(graphFile));
        }

        [TearDown]
        public void DestroyCanvas()
        {
            graphView.Dispose();
            Undo.ClearUndo(graphFile);
            Object.DestroyImmediate(window);
            Object.DestroyImmediate(graphFile);

            if (AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.DeleteAsset(TempFolder);
        }


        // ── Отпечаток данных ─────────────────────────────────────────────

        [Test]
        public void Fingerprint_ChangesWithWhatCanvasDrawsItself()
        {
            var graph = graphFile.Graph;
            var before = ScenarioGraphFingerprint.Of(graph);

            graph.Notes[0].Text = "Changed elsewhere";
            Assert.That(ScenarioGraphFingerprint.Of(graph), Is.Not.EqualTo(before), "Note text");

            var afterNote = ScenarioGraphFingerprint.Of(graph);
            graph.Variables[0].Name = "Renamed";
            Assert.That(ScenarioGraphFingerprint.Of(graph), Is.Not.EqualTo(afterNote), "Variable name");
        }

        [Test]
        public void Fingerprint_SameWhenOnlyBoundFieldValueChanges()
        {
            var graph = graphFile.Graph;
            var before = ScenarioGraphFingerprint.Of(graph);

            // Начальное значение рисует привязанное поле — холст пересобирать незачем.
            graph.Variables[0].InitialValue.FlagValue = !graph.Variables[0].InitialValue.FlagValue;

            Assert.That(ScenarioGraphFingerprint.Of(graph), Is.EqualTo(before));
        }


        // ── Зум ──────────────────────────────────────────────────────────

        [Test]
        public void Zoom_SameLimitsForWheelAndFrameAll()
        {
            Assert.That(graphView.minScale, Is.EqualTo(ScenarioCanvasNavigator.MinScale));
            Assert.That(graphView.maxScale, Is.EqualTo(ScenarioCanvasNavigator.MaxScale));

            graphView.Navigator.ZoomAround(Vector2.zero, 0.001f);
            Assert.That(graphView.viewTransform.scale.x, Is.EqualTo(ScenarioCanvasNavigator.MinScale).Within(0.0001f));
        }


        // ── Заметки ──────────────────────────────────────────────────────

        [Test]
        public void NoteTyping_IsOneUndoStep()
        {
            var noteView = graphView.graphElements.OfType<ScenarioNoteView>().Single();
            var textBefore = graphFile.Graph.Notes[0].Text;

            noteView.StartEditing();
            SetTypedText(noteView, "T");
            noteView.SaveTypedText();
            Undo.IncrementCurrentGroup();
            SetTypedText(noteView, "Two letters");
            noteView.SaveTypedText();

            Assert.That(graphFile.Graph.Notes[0].Text, Is.EqualTo("Two letters"));

            Undo.PerformUndo();
            Assert.That(graphFile.Graph.Notes[0].Text, Is.EqualTo(textBefore), "One undo takes back the whole editing session");
        }

        private static void SetTypedText(ScenarioNoteView noteView, string text)
        {
            noteView.Q<TextField>().SetValueWithoutNotify(text);
        }


        // ── Иконки ───────────────────────────────────────────────────────

        [Test]
        public void Icons_AllFound()
        {
            foreach (var name in ScenarioIcons.BuiltInNames)
                Assert.That(EditorGUIUtility.FindTexture(name), Is.Not.Null, name);

            Assert.That(ScenarioIcons.When, Is.Not.Null);
            Assert.That(ScenarioIcons.Flow, Is.Not.Null);
            Assert.That(ScenarioIcons.WaitFor, Is.Not.Null);
            Assert.That(ScenarioIcons.Guard, Is.Not.Null);
            Assert.That(ScenarioIcons.Repeat, Is.Not.Null);
            Assert.That(ScenarioIcons.IconOf(typeof(FlickerAction)), Is.Not.Null);
            Assert.That(ScenarioIcons.IconOf(graphFile.Graph.Variables[0]), Is.Not.Null);
        }


        [Test]
        public void Icons_DifferForMainNodeKinds_AndRepeatFoundByTitle()
        {
            var icons = new[]
            {
                ScenarioIcons.IconOf(typeof(ScenarioStepsNode)),
                ScenarioIcons.IconOf(typeof(ScenarioWhenNode)),
                ScenarioIcons.IconOf(typeof(ScenarioWaitForNode)),
                ScenarioIcons.IconOf(typeof(ScenarioGuardNode)),
                ScenarioIcons.IconOf(typeof(ScenarioRepeatTestNode))
            };

            Assert.That(icons, Is.All.Not.Null);
            Assert.That(icons.Distinct().Count(), Is.EqualTo(icons.Length));
            Assert.That(ScenarioIcons.IconOf(typeof(ScenarioRepeatTestNode)), Is.SameAs(ScenarioIcons.Repeat));
            Assert.That(ScenarioIcons.IconOf(typeof(ScenarioTestRecordAction)), Is.Not.EqualTo(ScenarioIcons.Flow),
                "A block without a category gets the neutral icon, not the flow one");
        }


        // ── Панель переменных ────────────────────────────────────────────

        [Test]
        public void VariableRow_StaysExpanded_WhenTypeChangeRebuildsCanvas()
        {
            var lamp = graphFile.Graph.Variables.Single(variable => variable.Name == "Lamp");
            RowOf(lamp.Id).expanded = true;

            // Так меняют тип через выбор типа: правка и полная пересборка холста.
            graphView.ApplyEdit(editing => editing.SetVariableObjectType(lamp.Id, typeof(AudioSource)));

            Assert.That(RowOf(lamp.Id).expanded, Is.True, "Changing the type must not collapse the variable's row");
            Assert.That(RowOf(graphFile.Graph.Variables[0].Id).expanded, Is.False, "Rows nobody opened stay closed");
        }

        private UnityEditor.Experimental.GraphView.BlackboardRow RowOf(string variableId)
        {
            return graphView.Blackboard.Query<UnityEditor.Experimental.GraphView.BlackboardRow>().ToList()
                .Single(row => row.Q<UnityEditor.Experimental.GraphView.BlackboardField>().userData as string == variableId);
        }


        // ── Подсветка в игре ─────────────────────────────────────────────

        [Test]
        public void RunHighlighter_CanvasDisposed_StopsListeningToWires()
        {
            var owner = EditorUtility.CreateGameObjectWithHideFlags("Highlight Test", HideFlags.HideAndDontSave);
            var watchingView = new ScenarioGraphView(window);

            try
            {
                var scenario = owner.AddComponent<Scenario>();
                watchingView.RunHighlighter.Target = scenario;
                Assert.That(WireFiredHandlersOf(scenario), Is.Not.Null);

                watchingView.Dispose();

                Assert.That(WireFiredHandlersOf(scenario), Is.Null, "A closed window must not keep the scenario's wire event");
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static System.Delegate WireFiredHandlersOf(Scenario scenario)
        {
            return (System.Delegate)typeof(Scenario).GetField("WireFired", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(scenario);
        }


        // ── Привязки ─────────────────────────────────────────────────────

        [Test]
        public void Bindings_AddFindRemove_AndObjectWithoutValue()
        {
            var owner = EditorUtility.CreateGameObjectWithHideFlags("Bindings Test", HideFlags.HideAndDontSave);

            try
            {
                var bindings = new ScenarioBindingsEditing(new SerializedObject(owner.AddComponent<Scenario>()));
                var lamp = graphFile.Graph.Variables.Single(variable => variable.Name == "Lamp");

                Assert.That(bindings.ObjectVariablesWithoutValue(graphFile.Graph), Is.EqualTo(new[] { lamp }));

                bindings.AddBinding(lamp.Id).FindPropertyRelative("objectValue").objectReferenceValue = owner.AddComponent<Light>();
                bindings.SerializedScenario.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(bindings.ValueOf(lamp.Id), Is.Not.Null);
                Assert.That(bindings.ObjectVariablesWithoutValue(graphFile.Graph), Is.Empty);

                bindings.AddBinding("gone variable");
                Assert.That(bindings.CountUnused(graphFile.Graph), Is.EqualTo(1));

                bindings.RemoveUnused(graphFile.Graph);
                bindings.RemoveBinding(lamp.Id);
                Assert.That(bindings.ValueOf(lamp.Id), Is.Null);
                Assert.That(bindings.CountUnused(graphFile.Graph), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }


        // ── Пропавшие ноды ───────────────────────────────────────────────

        [Test]
        public void MissingNode_Shown_AndDeletedAnywayWithItsWires()
        {
            var asset = CreateGraphFileWithMissingNode(out var missingNodeId);
            var missingGraphView = new ScenarioGraphView(window);

            try
            {
                missingGraphView.Show(ScenarioGraphHost.For(asset));
                var missingView = missingGraphView.nodes.OfType<ScenarioMissingNodeView>().Single();
                Assert.That(missingView.NodeId, Is.EqualTo(missingNodeId));

                var nullIndex = asset.Graph.Nodes.ToList().FindIndex(node => node == null);
                missingGraphView.Editing.RemoveMissingNode(nullIndex, missingNodeId);

                Assert.That(asset.Graph.Nodes.Any(node => node == null), Is.False);
                Assert.That(asset.Graph.Wires.Any(wire => wire.FromNodeId == missingNodeId || wire.ToNodeId == missingNodeId), Is.False);
            }
            finally
            {
                missingGraphView.Dispose();
                Undo.ClearUndo(asset);
            }
        }

        // Как переименование класса: в файле у ноды Guard меняется имя класса, и Unity её больше не находит.
        private static ScenarioGraphAsset CreateGraphFileWithMissingNode(out int missingNodeId)
        {
            AssetDatabase.CreateFolder("Assets", TempFolder.Substring("Assets/".Length));
            var path = TempFolder + "/Missing.asset";

            var asset = ScriptableObject.CreateInstance<ScenarioGraphAsset>();
            asset.Graph = ScenarioTestGraphs.GuardWhile();
            missingNodeId = ScenarioTestGraphs.FindNode<ScenarioGuardNode>(asset.Graph, ScenarioTestGraphs.GuardTitle).Id;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            var fullPath = System.IO.Path.GetFullPath(path);
            System.IO.File.WriteAllText(fullPath, System.IO.File.ReadAllText(fullPath).Replace("class: ScenarioGuardNode", "class: ScenarioGoneNode"));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return AssetDatabase.LoadAssetAtPath<ScenarioGraphAsset>(path);
        }
    }


    /// <summary>Нода «Repeat» под другим именем класса: иконка ищется по названию, как у будущей ноды рантайма.</summary>
    [System.Serializable]
    [ScenarioBlock("Flow/Repeat")]
    public class ScenarioRepeatTestNode : ScenarioFlowNode
    {
        public ScenarioRepeatTestNode(int times)
        {
        }


        protected override System.Collections.IEnumerator Perform(ScenarioRun run, ScenarioInput input)
        {
            yield break;
        }
    }
}
