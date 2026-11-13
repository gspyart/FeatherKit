using System;
using System.Collections.Generic;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Холст графа сценария: ноды, провода, заметки и панель переменных поверх, на еле заметной сетке.
    /// - любая правка — через ScenarioGraphEditing; после неё, после undo и после правки в обход холста — пересборка из данных
    /// - части: движение (ScenarioCanvasNavigator), создание нод и заметок (ScenarioNodeCreation),
    ///   строка состояния (ScenarioCanvasOverlay), пропуск невидимых стопок (ScenarioCanvasDetailCulling)
    /// - проблемы графа (ScenarioGraphValidation) — значками на нодах
    /// </summary>
    internal class ScenarioGraphView : GraphView
    {
        private const long FrameAllDelayMilliseconds = 100;
        private const string StyleSheetPath = "Packages/com.gspy.featherkit/Editor/FeatherKit/Scenarios/Graph/ScenarioGraphView.uss";
        private const string DarkThemeClass = "scenario-graph--dark";
        private const string LightThemeClass = "scenario-graph--light";

        private readonly Dictionary<(int nodeId, Direction direction, string portId), ScenarioPortView> portsByKey =
            new Dictionary<(int, Direction, string), ScenarioPortView>();

        private bool isRebuilding;
        private int nodeIdToSelectAfterRebuild;
        private long shownFingerprint;


        public ScenarioGraphView(EditorWindow window)
        {
            style.flexGrow = 1f;
            ApplyStyle(this);

            Navigator = new ScenarioCanvasNavigator(this);
            this.AddManipulator(Navigator);
            // Свои клавиши GraphView (пробел, A, O, [ ]) срабатывают и при наборе текста в поле ноды — их ведёт навигатор.
            isReframable = false;
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            NodeCreation = new ScenarioNodeCreation(this, window);

            Blackboard = new ScenarioGraphBlackboard(this);
            Add(Blackboard);

            Overlay = new ScenarioCanvasOverlay(this);
            DetailCulling = new ScenarioCanvasDetailCulling(this);

            graphViewChanged = HandleGraphViewChanged;
        }


        /// <summary>Выделена ровно одна нода — она; иначе null. Для инспектора ноды.</summary>
        public event Action<ScenarioNodeView> SelectedNodeChanged;

        /// <summary>Холст пересобран из данных — для строки состояния и шапки окна.</summary>
        public event Action Rebuilt;


        public ScenarioGraphHost Host { get; private set; }

        public ScenarioGraphEditing Editing { get; private set; }

        public ScenarioCanvasNavigator Navigator { get; }

        public ScenarioNodeCreation NodeCreation { get; }

        public ScenarioCanvasOverlay Overlay { get; }

        public ScenarioGraphBlackboard Blackboard { get; }

        public ScenarioCanvasDetailCulling DetailCulling { get; }

        /// <summary>Проблемы графа на момент последней пересборки.</summary>
        public IReadOnlyList<ScenarioGraphProblem> Problems { get; private set; } = new List<ScenarioGraphProblem>();

        public ScenarioNodeView SelectedNodeView
        {
            get
            {
                var selectedNodes = selection.OfType<ScenarioNodeView>().ToList();

                return selectedNodes.Count == 1 ? selectedNodes[0] : null;
            }
        }

        // Удалять выделенное, пока набираешь текст в поле ноды, нельзя: Delete там стирает букву.
        protected override bool canDeleteSelection =>
            base.canDeleteSelection && !ScenarioCanvasNavigator.IsTypingText(panel?.focusController?.focusedElement);


        /// <summary>Таблица стилей окна графа и класс темы редактора — холсту и всему окну.</summary>
        public static void ApplyStyle(VisualElement element)
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);

            if (styleSheet != null && !element.styleSheets.Contains(styleSheet))
                element.styleSheets.Add(styleSheet);

            element.AddToClassList(EditorGUIUtility.isProSkin ? DarkThemeClass : LightThemeClass);
        }


        // ── Открыть граф и править ───────────────────────────────────────

        public void Show(ScenarioGraphHost host)
        {
            Host = host;
            Editing = host != null ? new ScenarioGraphEditing(host) : null;

            Rebuild();
            schedule.Execute(Navigator.FrameWholeGraph).ExecuteLater(FrameAllDelayMilliseconds);
        }


        /// <summary>Правка графа и сразу пересборка холста: старые элементы держали бы пути к сдвинувшимся нодам.</summary>
        public void ApplyEdit(Action<ScenarioGraphEditing> edit)
        {
            if (Editing == null || !Host.IsAlive)
                return;

            edit(Editing);
            Rebuild();
        }

        /// <summary>Холст сам показал правку без пересборки (сдвиг, набор текста) — проверке данных пересобирать незачем.</summary>
        public void RememberShownData()
        {
            shownFingerprint = FingerprintOf(Host);
        }

        /// <summary>Граф поменяли в обход холста — откат префаба, второе окно, инспектор: пересобрать.</summary>
        public void RebuildIfDataChanged()
        {
            if (FingerprintOf(Host) != shownFingerprint)
                Rebuild();
        }

        // Привязки объекта правят и в инспекторе Scenario: его счётчик правок — в отпечаток, иначе предупреждения отстанут.
        private static long FingerprintOf(ScenarioGraphHost host)
        {
            if (host is not { IsAlive: true })
                return 0L;

            var bindingsVersion = host.Bindings != null ? EditorUtility.GetDirtyCount(host.Bindings.Scenario) : 0;

            return ScenarioGraphFingerprint.Of(host.Graph) * 31L + bindingsVersion;
        }


        // ── Пересборка ───────────────────────────────────────────────────

        public void Rebuild()
        {
            var selectedNodeIds = nodeIdToSelectAfterRebuild != 0
                ? new HashSet<int> { nodeIdToSelectAfterRebuild }
                : new HashSet<int>(selection.OfType<ScenarioNodeView>().Select(nodeView => nodeView.NodeId));

            nodeIdToSelectAfterRebuild = 0;
            SaveEditedNotes();

            isRebuilding = true;
            ClearSelection();

            foreach (var element in graphElements.ToList())
            {
                if (element != Blackboard && !Blackboard.Contains(element))
                    RemoveElement(element);
            }

            portsByKey.Clear();
            Problems = new List<ScenarioGraphProblem>();

            if (Host is { IsAlive: true } && Host.Graph != null)
            {
                Host.SerializedGraph.Update();
                Host.Bindings?.SerializedScenario.UpdateIfRequiredOrScript();
                Problems = ScenarioGraphValidation.Check(Host.Graph, Host.IsGraphFile, Host.Bindings);
                AddNoteViews();
                AddNodeViews(selectedNodeIds);
                AddWireViews();
            }

            shownFingerprint = FingerprintOf(Host);
            Blackboard.Rebuild();
            isRebuilding = false;

            SelectedNodeChanged?.Invoke(SelectedNodeView);
            Rebuilt?.Invoke();
        }

        // Заметку правят прямо сейчас: текст — в данные, а правка продолжится в новой заметке.
        private void SaveEditedNotes()
        {
            foreach (var noteView in graphElements.OfType<ScenarioNoteView>().Where(noteView => noteView.IsEditing))
            {
                noteView.SaveTypedText();
                NodeCreation.NoteIdToEdit ??= noteView.NoteId;
            }
        }

        private void AddNoteViews()
        {
            foreach (var note in Host.Graph.Notes)
            {
                if (note == null)
                    continue;

                var noteView = new ScenarioNoteView(this, note);
                AddElement(noteView);
                noteView.RefreshLayer();

                if (note.Id == NodeCreation.NoteIdToEdit)
                    noteView.StartEditing();
            }

            NodeCreation.NoteIdToEdit = null;
        }

        private void AddNodeViews(HashSet<int> selectedNodeIds)
        {
            var nodes = Host.Graph.Nodes;
            var problemsByNodeId = Problems.ToLookup(problem => problem.NodeId);

            for (var index = 0; index < nodes.Count; index++)
            {
                var nodeProperty = Host.NodePropertyAt(index);

                if (nodes[index] == null || nodeProperty == null)
                    continue;

                var nodeView = new ScenarioNodeView(this, nodes[index], nodeProperty);
                AddNodeElement(nodeView);
                nodeView.Header.ShowProblems(problemsByNodeId[nodeView.NodeId].ToList());

                if (selectedNodeIds.Contains(nodeView.NodeId))
                    AddToSelection(nodeView);
            }

            foreach (var missingNodeView in ScenarioMissingNodeView.CreateAll(this, Host.Owner, Host.Graph))
                AddNodeElement(missingNodeView);
        }

        private void AddNodeElement(Node nodeView)
        {
            AddElement(nodeView);
            nodeView.Query<ScenarioPortView>().ForEach(port => portsByKey[(port.NodeId, port.direction, port.PortId)] = port);
        }

        // Провод к порту, которого у ноды нет, не рисуется, но и не удаляется: значок на ноде говорит о нём.
        private void AddWireViews()
        {
            foreach (var wire in Host.Graph.Wires)
            {
                if (!portsByKey.TryGetValue((wire.FromNodeId, Direction.Output, wire.FromPortId), out var output)
                    || !portsByKey.TryGetValue((wire.ToNodeId, Direction.Input, wire.ToPortId), out var input))
                    continue;

                var edge = output.ConnectTo(input);
                edge.userData = wire;
                AddElement(edge);
            }
        }


        /// <summary>Выделить эту ноду после ближайшей пересборки — например, только что созданную.</summary>
        public void SelectAfterRebuild(int nodeId)
        {
            nodeIdToSelectAfterRebuild = nodeId;
        }


        // ── Правки с холста ──────────────────────────────────────────────

        private GraphViewChange HandleGraphViewChanged(GraphViewChange change)
        {
            if (isRebuilding || Editing == null)
                return change;

            if (change.movedElements != null)
                SaveMovedElements(change.movedElements);

            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            {
                var createdEdges = change.edgesToCreate.ToList();
                change.edgesToCreate.Clear();
                ApplyEdit(editing => createdEdges.ForEach(edge => ConnectInData(editing, edge.output, edge.input, null)));
            }

            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                // Сдёрнутый с порта провод ждёт выбора в поиске нод — его данные не трогаем.
                var removedElements = change.elementsToRemove.Where(element => element is not Edge edge || !ScenarioNodeCreation.IsDraggedOff(edge)).ToList();
                change.elementsToRemove.Clear();
                ApplyEdit(editing => RemoveInData(editing, removedElements));
            }

            return change;
        }

        private void SaveMovedElements(List<GraphElement> movedElements)
        {
            foreach (var nodeView in movedElements.OfType<ScenarioNodeView>())
                Editing.MoveNode(nodeView.NodeId, AreaFromStyle(nodeView).position);

            foreach (var noteView in movedElements.OfType<ScenarioNoteView>())
                Editing.SetNoteArea(noteView.NoteId, AreaFromStyle(noteView));

            RememberShownData();
        }

        // Провода — первыми: удалённая нода и так снимет свои, а оставшиеся найдутся по ссылке.
        private static void RemoveInData(ScenarioGraphEditing editing, List<GraphElement> removedElements)
        {
            editing.Disconnect(removedElements.OfType<Edge>().Select(edge => edge.userData).OfType<ScenarioWire>());
            editing.RemoveNodes(removedElements.OfType<ScenarioNodeView>().Select(nodeView => nodeView.NodeId));
            editing.RemoveNotes(removedElements.OfType<ScenarioNoteView>().Select(noteView => noteView.NoteId));

            foreach (var variableField in removedElements.OfType<BlackboardField>())
            {
                if (variableField.userData is string variableId)
                    editing.RemoveVariable(variableId);
            }
        }


        /// <summary>Провод отпустили на порт. Старый провод, если его перетащили с другого порта, переезжает.</summary>
        public void ConnectPorts(Port output, Port input, ScenarioWire movedWire)
        {
            ApplyEdit(editing => ConnectInData(editing, output, input, movedWire));
        }

        private static void ConnectInData(ScenarioGraphEditing editing, Port output, Port input, ScenarioWire movedWire)
        {
            if (output is not ScenarioPortView from || input is not ScenarioPortView to)
                return;

            if (movedWire != null && movedWire.Connects(from.NodeId, from.PortId, to.NodeId, to.PortId))
                return;

            if (movedWire != null)
                editing.Disconnect(new[] { movedWire });

            editing.Connect(from.NodeId, from.PortId, to.NodeId, to.PortId);
        }


        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList()
                .Where(port => port.direction != startPort.direction && port.node != startPort.node && port.enabledInHierarchy)
                .ToList();
        }


        public override void BuildContextualMenu(ContextualMenuPopulateEvent menuEvent)
        {
            if (menuEvent.target is GraphView && Editing != null)
            {
                var graphPosition = contentViewContainer.WorldToLocal(menuEvent.mousePosition);
                menuEvent.menu.AppendAction("Create Comment", _ => NodeCreation.CreateComment(graphPosition));
            }

            base.BuildContextualMenu(menuEvent);
        }


        /// <summary>Место элемента из его стиля: так его ставят перетаскивание и растяжка, а layout догоняет кадром позже.</summary>
        public static Rect AreaFromStyle(GraphElement element)
        {
            var layoutArea = element.GetPosition();

            return new Rect(
                element.style.left.keyword == StyleKeyword.Undefined ? element.style.left.value.value : layoutArea.x,
                element.style.top.keyword == StyleKeyword.Undefined ? element.style.top.value.value : layoutArea.y,
                element.style.width.keyword == StyleKeyword.Undefined ? element.style.width.value.value : layoutArea.width,
                element.style.height.keyword == StyleKeyword.Undefined ? element.style.height.value.value : layoutArea.height);
        }


        // ── Выделение ────────────────────────────────────────────────────

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            ReportSelection();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            ReportSelection();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            ReportSelection();
        }

        private void ReportSelection()
        {
            if (!isRebuilding)
                SelectedNodeChanged?.Invoke(SelectedNodeView);
        }


        // ── Закрыть ──────────────────────────────────────────────────────

        public void Dispose()
        {
            NodeCreation.Dispose();
        }
    }
}
