using System;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Создание нод и заметок на холсте — часть ScenarioGraphView.
    /// - поиск нод по пробелу, меню и брошенному проводу; новая нода встаёт под курсор
    /// - брошенный в пустоту провод цел, пока в поиске ничего не выбрали
    /// - заметка по C (без модификаторов) или из меню — на свободное место у курсора, сразу в правке
    /// </summary>
    internal class ScenarioNodeCreation
    {
        private const float FreePlaceStep = 20f;
        private const int FreePlaceAttempts = 100;

        private readonly ScenarioGraphView graphView;
        private readonly EditorWindow window;
        private readonly ScenarioNodeSearch nodeSearch;

        private ScenarioWire wireToReplace;


        public ScenarioNodeCreation(ScenarioGraphView graphView, EditorWindow window)
        {
            this.graphView = graphView;
            this.window = window;

            nodeSearch = ScriptableObject.CreateInstance<ScenarioNodeSearch>();
            nodeSearch.hideFlags = HideFlags.HideAndDontSave;

            graphView.nodeCreationRequest = context => OpenSearch(context.screenMousePosition, null);
            graphView.RegisterCallback<KeyDownEvent>(HandleCommentKey);
        }


        /// <summary>Заметка, которую после пересборки холста открыть в правке. Холст забирает и обнуляет.</summary>
        public string NoteIdToEdit { get; set; }

        private Vector2 PanelToScreenOffset => graphView.Navigator.PanelToScreenOffset ?? window.position.position;


        // ── Координаты ───────────────────────────────────────────────────

        // Один перевод на всё: панель ↔ экран по сдвигу, который сообщила сама Unity; пока не сообщила — по position окна.
        public Vector2 CanvasToScreen(Vector2 canvasLocalPosition)
        {
            return PanelToScreen(graphView.LocalToWorld(canvasLocalPosition));
        }

        public Vector2 PanelToScreen(Vector2 worldPosition)
        {
            return worldPosition + PanelToScreenOffset;
        }

        public Vector2 ScreenToGraph(Vector2 screenPosition)
        {
            return graphView.contentViewContainer.WorldToLocal(screenPosition - PanelToScreenOffset);
        }

        // ── Поиск нод ────────────────────────────────────────────────────

        /// <summary>Открыть поиск; порт — с него бросили провод, новая нода соединится с ним.</summary>
        public void OpenSearch(Vector2 screenPosition, Port portOfDroppedWire)
        {
            if (graphView.Editing == null)
                return;

            wireToReplace = null;
            nodeSearch.Prepare(this, portOfDroppedWire);
            SearchWindow.Open(new SearchWindowContext(screenPosition), nodeSearch);
        }


        /// <summary>
        /// Провод отпустили в пустоте. Старый провод, сдёрнутый с порта, не удаляется: заменится, только если в поиске что-то выбрали.
        /// </summary>
        public void HandleWireDroppedOutside(Edge edge, Vector2 worldPosition)
        {
            var portOfDroppedWire = edge.output ?? edge.input;

            OpenSearch(PanelToScreen(worldPosition), portOfDroppedWire);
            wireToReplace = edge.userData as ScenarioWire;

            // GraphView уже убрал сдёрнутый провод с холста — вернуть картинку к данным.
            graphView.schedule.Execute(graphView.Rebuild);
        }

        /// <summary>Провод с одним свободным концом — его сдёрнули с порта и тащат: удалять его данные рано.</summary>
        public static bool IsDraggedOff(Edge edge)
        {
            return edge.input == null || edge.output == null;
        }


        public void CreateFromSearch(Type nodeOrBlockType, Vector2 screenPosition, Port portOfDroppedWire)
        {
            var graphPosition = ScreenToGraph(screenPosition);
            var replacedWire = wireToReplace;
            wireToReplace = null;

            if (nodeOrBlockType == typeof(ScenarioNote))
            {
                CreateComment(graphPosition);
                return;
            }

            graphView.ApplyEdit(editing =>
            {
                var node = editing.AddNode(nodeOrBlockType, graphPosition);
                graphView.SelectAfterRebuild(node.Id);

                if (portOfDroppedWire is not ScenarioPortView droppedFrom)
                    return;

                if (replacedWire != null)
                    editing.Disconnect(new[] { replacedWire });

                ConnectToNewNode(editing, droppedFrom, node);
            });
        }

        // Провод тянули с выхода — к первому входу новой ноды; со входа — от её первого выхода.
        private static void ConnectToNewNode(ScenarioGraphEditing editing, ScenarioPortView droppedFrom, ScenarioNode node)
        {
            var ports = node.CollectPorts();

            if (droppedFrom.direction == Direction.Output && ports.Inputs.Count > 0)
                editing.Connect(droppedFrom.NodeId, droppedFrom.PortId, node.Id, ports.Inputs[0].Id);
            else if (droppedFrom.direction == Direction.Input && ports.Outputs.Count > 0)
                editing.Connect(node.Id, ports.Outputs[0].Id, droppedFrom.NodeId, droppedFrom.PortId);
        }


        // ── Заметки ──────────────────────────────────────────────────────

        // На Mac Ctrl+C и Cmd+C — копирование, заметку создаёт только голая C.
        private void HandleCommentKey(KeyDownEvent keyEvent)
        {
            if (keyEvent.keyCode != KeyCode.C || ScenarioCanvasNavigator.HasModifiers(keyEvent)
                                              || ScenarioCanvasNavigator.IsTypingText(keyEvent.target as Focusable))
                return;

            CreateComment(graphView.contentViewContainer.WorldToLocal(graphView.LocalToWorld(graphView.Navigator.LastMousePosition)));
            keyEvent.StopPropagation();
        }

        /// <summary>Новая заметка у точки холста, на свободном от нод месте — сразу в режиме правки текста.</summary>
        public void CreateComment(Vector2 graphPosition)
        {
            var area = FreePlaceFor(new Rect(graphPosition, ScenarioGraphEditing.NewNoteSize));

            graphView.ApplyEdit(editing =>
            {
                var note = editing.AddNote(area.position);
                NoteIdToEdit = note.Id;
            });
        }

        // Сдвигаем вниз, пока заметка задевает ноду: заметка под нодой не видна.
        private Rect FreePlaceFor(Rect area)
        {
            var nodeAreas = graphView.nodes.Select(ScenarioGraphView.AreaFromStyle).ToList();

            for (var attempt = 0; attempt < FreePlaceAttempts && nodeAreas.Any(nodeArea => nodeArea.Overlaps(area)); attempt++)
                area.y += FreePlaceStep;

            return area;
        }


        // ── Закрыть ──────────────────────────────────────────────────────

        public void Dispose()
        {
            if (nodeSearch != null)
                Object.DestroyImmediate(nodeSearch);
        }
    }
}
