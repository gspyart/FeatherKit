using System.Linq;
using System.Reflection;
using FeatherKit.EditorTools;
using FeatherKit.Scenarios;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.Tests.Scenarios
{
    /// <summary>
    /// Находки тестировщика редактора графа: каждый тест ловит баг, пока его не починят.
    /// Холст создаётся без окна на экране — проверяется логика, а не вёрстка.
    /// </summary>
    public class ScenarioEditorQaTests
    {
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
        }


        // ── Заметки ──────────────────────────────────────────────────────

        [Test]
        [Category("QA-Found")]
        public void NoteBeingEdited_CanvasRebuilt_TypedTextIsKept()
        {
            var noteView = graphView.graphElements.OfType<ScenarioNoteView>().Single();
            noteView.StartEditing();
            var textField = (TextField)typeof(ScenarioNoteView).GetField("textField", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(noteView);
            textField.value = "Typed, not yet committed";

            // Пересборка — undo, смена выделения, правка из другого окна или инспектора.
            graphView.Rebuild();

            Assert.That(graphFile.Graph.Notes.Single().Text, Is.EqualTo("Typed, not yet committed"),
                "Text typed into a note is lost when the canvas is rebuilt before the note loses focus");
        }


        // ── Холст догоняет данные ────────────────────────────────────────

        [Test]
        [Category("QA-Found")]
        public void NodeMovedOutsideCanvas_DataCheck_CanvasFollows()
        {
            var node = graphFile.Graph.Nodes[1];
            var serializedGraph = graphView.Host.SerializedGraph;
            serializedGraph.Update();

            // Так значение меняют Revert префаба, второе окно или инспектор: число нод то же.
            var nodeIndex = graphFile.Graph.Nodes.ToList().IndexOf(node);
            serializedGraph.FindProperty($"graph.nodes.Array.data[{nodeIndex}].position").vector2Value = new Vector2(1234f, 567f);
            serializedGraph.ApplyModifiedPropertiesWithoutUndo();

            graphView.RebuildIfDataChanged();

            var nodeView = graphView.nodes.OfType<ScenarioNodeView>().Single(view => view.NodeId == node.Id);
            Assert.That(ScenarioGraphView.AreaFromStyle(nodeView).position, Is.EqualTo(new Vector2(1234f, 567f)),
                "The canvas re-checks only counts of nodes, wires, variables and notes — a moved node stays at the old place");
        }

        [Test]
        [Category("QA-Found")]
        public void VariableRenamedOutsideCanvas_DataCheck_PanelShowsNewName()
        {
            var serializedGraph = graphView.Host.SerializedGraph;
            serializedGraph.Update();
            serializedGraph.FindProperty("graph.variables.Array.data[0].variableName").stringValue = "Renamed Elsewhere";
            serializedGraph.ApplyModifiedPropertiesWithoutUndo();

            graphView.RebuildIfDataChanged();

            var names = graphView.Query<UnityEditor.Experimental.GraphView.BlackboardField>().ToList().Select(field => field.text);
            Assert.That(names, Does.Contain("Renamed Elsewhere"));
        }


        // ── Зум ──────────────────────────────────────────────────────────

        [Test]
        [Category("QA-Found")]
        public void FrameAll_CanZoomOutAsFarAsTheWheel()
        {
            // «A» и «Frame All» зовут GraphView.FrameAll, а он не отдаляет дальше minScale (0.25 без SetupZoom).
            // Колесо отдаляет до 0.15 — большой граф кнопкой целиком не показать.
            Assert.That(graphView.minScale, Is.LessThanOrEqualTo(0.15f + 0.0001f));
        }
    }
}
