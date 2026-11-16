using System.Collections.Generic;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Нода графа на холсте: шапка (ScenarioNodeHeader), порты, поля и стопки блоков прямо на ноде.
    /// - ширина по виду ноды: стопкам и исходам нужно место, нодам потока — нет
    /// - шапка и подписи выходов догоняют правки полей, не пересобирая ноду
    /// - кнопка в шапке сворачивает ноду до шапки и портов; двойной клик по шапке — своя подпись
    /// </summary>
    internal class ScenarioNodeView : Node
    {
        private const float StackNodeWidth = 320f;
        private const float CasesNodeWidth = 300f;
        private const float FlowNodeWidth = 240f;
        private const string SingleInputId = "in";
        private const string TitleLabelName = "title-label";

        private readonly ScenarioGraphView graphView;
        private readonly SerializedProperty nodeProperty;
        private readonly Dictionary<string, ScenarioPortView> outputsById = new Dictionary<string, ScenarioPortView>();


        public ScenarioNodeView(ScenarioGraphView graphView, ScenarioNode node, SerializedProperty nodeProperty)
        {
            this.graphView = graphView;
            Node = node;
            this.nodeProperty = nodeProperty.Copy();
            viewDataKey = $"scenario-node-{node.Id}";

            AddToClassList("scenario-node");
            style.width = WidthOf(node);

            Header = new ScenarioNodeHeader(this, node, this.nodeProperty);
            CreatePorts();

            var fields = new VisualElement();
            fields.AddToClassList("scenario-node__fields");
            ScenarioNodeFields.AddTo(fields, graphView, node, nodeProperty, isInspector: false);
            extensionContainer.Add(fields);
            extensionContainer.AddToClassList("scenario-node__body");

            RefreshCaptions();
            AddCollapseButton();
            SetPosition(new Rect(node.Position, Vector2.zero));

            titleContainer.RegisterCallback<MouseDownEvent>(HandleTitleDoubleClick);
            this.TrackSerializedObjectValue(nodeProperty.serializedObject, _ => RefreshCaptions());
        }


        public ScenarioNode Node { get; }

        public int NodeId => Node.Id;

        public ScenarioNodeHeader Header { get; }


        // Стопкам When и Do и условиям Guard нужно место под поля блоков; ноде потока — только под порты.
        private static float WidthOf(ScenarioNode node)
        {
            return node switch
            {
                ScenarioWhenNode or ScenarioStepsNode or ScenarioGuardNode => StackNodeWidth,
                ScenarioWaitForNode => CasesNodeWidth,
                _ => FlowNodeWidth
            };
        }


        // ── Порты ────────────────────────────────────────────────────────

        // Единственный вход «In» — точка без подписи: куда входит провод, и так видно.
        private void CreatePorts()
        {
            var ports = Node.CollectPorts();
            var hasSingleInput = ports.Inputs.Count == 1 && ports.Inputs[0].Id == SingleInputId;

            foreach (var input in ports.Inputs)
                inputContainer.Add(new ScenarioPortView(Direction.Input, Node.Id, input.Id, hasSingleInput ? "" : input.Label));

            foreach (var output in ports.Outputs)
            {
                var port = new ScenarioPortView(Direction.Output, Node.Id, output.Id, output.Label);
                outputsById[output.Id] = port;
                outputContainer.Add(port);
            }
        }


        // ── Свернуть ─────────────────────────────────────────────────────

        // Свернуть — через пересборку: свёрнутая нода убирает порты без проводов, и провода надо тянуть заново.
        private void AddCollapseButton()
        {
            var button = new Button(() =>
            {
                ScenarioCollapsedNodes.Remember(graphView.Host.Owner, NodeId, expanded);
                graphView.Rebuild();
            })
            {
                text = "▾",
                tooltip = "Collapse the node to its ports"
            };

            button.AddToClassList("scenario-node__collapse");
            titleContainer.Add(button);
        }

        /// <summary>Свернуть, если так запомнено. Звать, когда провода уже на месте: порты с проводами остаются видны.</summary>
        public void ApplyRememberedCollapse(ICollection<int> collapsedNodeIds)
        {
            if (collapsedNodeIds.Contains(NodeId))
                Collapse();
        }

        // Свёрнутая нода прячет поля и порты без проводов — как свёрнутая нода Shader Graph.
        // Развёрнутой ничего делать не надо: пересчёт раскладки ноды дорог, а на холсте их сотни.
        private void Collapse()
        {
            expanded = false;
            extensionContainer.style.display = DisplayStyle.None;
            RefreshExpandedState();

            var button = titleContainer.Q<Button>(className: "scenario-node__collapse");
            button.text = "▸";
            button.tooltip = "Expand the node";
        }


        // ── Своя подпись ─────────────────────────────────────────────────

        private void HandleTitleDoubleClick(MouseDownEvent mouseEvent)
        {
            if (mouseEvent.clickCount != 2 || mouseEvent.button != 0 || graphView.Editing == null)
                return;

            StartRenaming();
            mouseEvent.StopPropagation();
        }

        /// <summary>Поле вместо заголовка: Enter или уход фокуса — записать, Esc — отменить, пусто — снова название блока.</summary>
        public void StartRenaming()
        {
            var titleLabel = titleContainer.Q<Label>(TitleLabelName);
            var field = new TextField { value = string.IsNullOrEmpty(Node.CustomTitle) ? title : Node.CustomTitle };
            field.AddToClassList("scenario-node__title-field");
            titleLabel.parent.Insert(titleLabel.parent.IndexOf(titleLabel), field);
            titleLabel.style.display = DisplayStyle.None;

            var isFinished = false;

            void Finish(bool isSaved)
            {
                if (isFinished)
                    return;

                isFinished = true;
                field.RemoveFromHierarchy();
                titleLabel.style.display = DisplayStyle.Flex;

                if (isSaved)
                    graphView.ApplyEdit(editing => editing.SetNodeTitle(NodeId, field.value == Node.DefaultTitle ? "" : field.value));
            }

            field.RegisterCallback<FocusOutEvent>(_ => Finish(true));
            field.RegisterCallback<KeyDownEvent>(keyEvent =>
            {
                if (keyEvent.keyCode is KeyCode.Return or KeyCode.KeypadEnter or KeyCode.Escape)
                {
                    Finish(keyEvent.keyCode != KeyCode.Escape);
                    keyEvent.StopPropagation();
                }
            }, TrickleDown.TrickleDown);

            field.schedule.Execute(() =>
            {
                field.Focus();
                field.SelectAll();
            });
        }


        // ── Подписи ──────────────────────────────────────────────────────

        // Объект графа удалили или ноду убрали из данных: холст сейчас пересоберётся.
        private void RefreshCaptions()
        {
            if (nodeProperty.serializedObject.targetObject == null || nodeProperty.managedReferenceValue != Node)
                return;

            Header.Refresh();

            foreach (var output in Node.CollectPorts().Outputs)
            {
                if (outputsById.TryGetValue(output.Id, out var port))
                    port.portName = output.Label;
            }
        }
    }


    /// <summary>Какие ноды свёрнуты — до конца сессии редактора, по «владелец графа + id ноды». В данные графа не пишется.</summary>
    internal static class ScenarioCollapsedNodes
    {
        private const string SessionKeyPrefix = "FeatherKit.ScenarioGraph.Collapsed.";
        private const char Separator = ';';

        private static readonly Dictionary<EntityId, string> KeysByEntityId = new Dictionary<EntityId, string>();


        public static bool IsCollapsed(Object owner, int nodeId)
        {
            return CollapsedNodeIds(owner).Contains(nodeId);
        }

        /// <summary>Все свёрнутые ноды графа — одним чтением на пересборку холста.</summary>
        public static HashSet<int> CollapsedNodeIds(Object owner)
        {
            var nodeIds = new HashSet<int>();

            if (owner == null)
                return nodeIds;

            foreach (var text in Read(owner))
            {
                if (int.TryParse(text, out var nodeId))
                    nodeIds.Add(nodeId);
            }

            return nodeIds;
        }

        public static void Remember(Object owner, int nodeId, bool isCollapsed)
        {
            if (owner == null)
                return;

            var nodeIds = Read(owner);

            if (isCollapsed)
                nodeIds.Add(nodeId.ToString());
            else
                nodeIds.Remove(nodeId.ToString());

            SessionState.SetString(KeyOf(owner), string.Join(Separator.ToString(), nodeIds));
        }

        private static HashSet<string> Read(Object owner)
        {
            return new HashSet<string>(SessionState.GetString(KeyOf(owner), "").Split(new[] { Separator }, System.StringSplitOptions.RemoveEmptyEntries));
        }

        // GlobalObjectId переживает перезагрузку домена и сцены; у несохраняемого объекта его нет — тогда свой id сессии.
        private static string KeyOf(Object owner)
        {
            var entityId = owner.GetEntityId();

            if (!KeysByEntityId.TryGetValue(entityId, out var key))
            {
                var globalId = GlobalObjectId.GetGlobalObjectIdSlow(owner);
                key = SessionKeyPrefix + (globalId.identifierType != 0 ? globalId.ToString() : $"session-{entityId}");
                KeysByEntityId[entityId] = key;
            }

            return key;
        }
    }
}
