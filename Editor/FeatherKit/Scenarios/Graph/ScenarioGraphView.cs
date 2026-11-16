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
    /// Холст графа сценария: ноды, провода, заметки, рамки и панель переменных поверх, на еле заметной сетке.
    /// - любая правка — через ScenarioGraphEditing; после неё, после undo и после правки в обход холста — пересборка из данных
    /// - части: движение, создание, правки мышью, буфер обмена, строка состояния, миникарта, пропуск стопок, подсветка в игре —
    ///   каждая своим классом ScenarioCanvas… / ScenarioNodeCreation / ScenarioGraphClipboard / ScenarioRunHighlighter
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

        private readonly HashSet<int> nodeIdsToSelectAfterRebuild = new HashSet<int>();
        private long shownFingerprint;


        public ScenarioGraphView(EditorWindow window)
        {
            Window = window;
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
            Clipboard = new ScenarioGraphClipboard(this);

            Blackboard = new ScenarioGraphBlackboard(this);
            Add(Blackboard);

            Overlay = new ScenarioCanvasOverlay(this);
            MiniMap = new ScenarioCanvasMiniMap(this);
            DetailCulling = new ScenarioCanvasDetailCulling(this);
            RunHighlighter = new ScenarioRunHighlighter(this);

            CanvasChanges = new ScenarioCanvasChanges(this);
        }


        /// <summary>Выделена ровно одна нода — она; иначе null. Для инспектора ноды.</summary>
        public event Action<ScenarioNodeView> SelectedNodeChanged;

        /// <summary>Холст пересобран из данных — для строки состояния и шапки окна.</summary>
        public event Action Rebuilt;


        /// <summary>Окно холста — для плашек-уведомлений поверх него.</summary>
        public EditorWindow Window { get; }

        public ScenarioGraphHost Host { get; private set; }

        public ScenarioGraphEditing Editing { get; private set; }

        public ScenarioCanvasNavigator Navigator { get; }

        public ScenarioNodeCreation NodeCreation { get; }

        public ScenarioGraphClipboard Clipboard { get; }

        public ScenarioCanvasChanges CanvasChanges { get; }

        public ScenarioCanvasMiniMap MiniMap { get; }

        public ScenarioCanvasOverlay Overlay { get; }

        public ScenarioGraphBlackboard Blackboard { get; }

        public ScenarioCanvasDetailCulling DetailCulling { get; }

        public ScenarioRunHighlighter RunHighlighter { get; }

        /// <summary>Холст сейчас пересобирается: события элементов о добавлении и выделении — не правки человека.</summary>
        public bool IsRebuilding { get; private set; }

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
        protected override bool canDeleteSelection => base.canDeleteSelection && !IsTypingText;

        // Cmd+C в поле ноды копирует текст, а не ноды.
        protected override bool canCopySelection => base.canCopySelection && !IsTypingText;

        protected override bool canCutSelection => base.canCutSelection && !IsTypingText;

        protected override bool canDuplicateSelection => base.canDuplicateSelection && !IsTypingText;

        protected override bool canPaste => base.canPaste && !IsTypingText;

        private bool IsTypingText => ScenarioCanvasNavigator.IsTypingText(panel?.focusController?.focusedElement);


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
            RunHighlighter.ChooseDefaultTarget();
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
            var selectedNodeIds = nodeIdsToSelectAfterRebuild.Count > 0
                ? new HashSet<int>(nodeIdsToSelectAfterRebuild)
                : new HashSet<int>(selection.OfType<ScenarioNodeView>().Select(nodeView => nodeView.NodeId));

            nodeIdsToSelectAfterRebuild.Clear();
            SaveEditedNotes();

            IsRebuilding = true;
            ClearSelection();

            // Панель переменных и миникарта лежат поверх холста, а не на нём: их не трогаем.
            foreach (var element in graphElements.ToList())
            {
                if (contentViewContainer.Contains(element))
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
                AddGroupViews();
                AddWireViews();

                var collapsedNodeIds = ScenarioCollapsedNodes.CollapsedNodeIds(Host.Owner);

                foreach (var nodeView in nodes.ToList().OfType<ScenarioNodeView>())
                    nodeView.ApplyRememberedCollapse(collapsedNodeIds);
            }

            shownFingerprint = FingerprintOf(Host);
            Blackboard.Rebuild();
            IsRebuilding = false;

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

        private void AddGroupViews()
        {
            var nodeViewsById = nodes.OfType<ScenarioNodeView>().ToDictionary(nodeView => nodeView.NodeId);

            foreach (var group in Host.Graph.Groups)
            {
                if (group == null)
                    continue;

                // Рамка без нод на холсте встала бы в (0,0) пустой: её не рисуем.
                var nodeViews = group.NodeIds.Where(nodeViewsById.ContainsKey).Select(nodeId => (GraphElement)nodeViewsById[nodeId]).ToList();

                if (nodeViews.Count == 0)
                    continue;

                var groupView = new ScenarioGroupView(this, group);
                AddElement(groupView);
                groupView.Fill(nodeViews);
            }
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


        /// <summary>Выделить эти ноды после ближайшей пересборки — например, только что созданную или вставленные.</summary>
        public void SelectAfterRebuild(params int[] nodeIds)
        {
            SelectAfterRebuild((IEnumerable<int>)nodeIds);
        }

        public void SelectAfterRebuild(IEnumerable<int> nodeIds)
        {
            nodeIdsToSelectAfterRebuild.Clear();
            nodeIdsToSelectAfterRebuild.UnionWith(nodeIds);
        }


        // ── Связь портов и меню ──────────────────────────────────────────

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

            if (menuEvent.target is GraphView or ScenarioNodeView && NodeCreation.CanGroupSelection)
                menuEvent.menu.AppendAction("Group Selection", _ => NodeCreation.GroupSelection());

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
            if (!IsRebuilding)
                SelectedNodeChanged?.Invoke(SelectedNodeView);
        }


        // ── Закрыть ──────────────────────────────────────────────────────

        public void Dispose()
        {
            NodeCreation.Dispose();
            RunHighlighter.Dispose();
        }
    }
}
