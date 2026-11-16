using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Заметка-комментарий на холсте: многострочный текст на цветной подложке, без портов, в игре не участвует.
    /// - лежит под нодами; выделенная или правящаяся поднимается наверх, длинный текст обрезается рамкой
    /// - двойной клик — правка прямо на холсте: текст пишется по ходу набора одной записью undo, Esc отменяет
    /// - размер — за край или угол, цвет — из контекстного меню
    /// </summary>
    internal class ScenarioNoteView : GraphElement, IResizable
    {
        private const string Placeholder = "Double-click to write a comment";

        private static readonly (string name, Color color)[] Palette =
        {
            ("Red", Hex(0x7A3B3B)),
            ("Orange", Hex(0x7F5A33)),
            ("Yellow", Hex(0x77703A)),
            ("Green", Hex(0x3F6B43)),
            ("Teal", Hex(0x33666A)),
            ("Blue", Hex(0x3B5478)),
            ("Purple", Hex(0x5E4677)),
            ("Pink", Hex(0x7A4664))
        };

        // Цвет, с которым заметку создаёт контракт. Заметка, сохранённая до поля цвета, читается прозрачной — рисуем её этим.
        private static readonly Color DefaultColor = new ScenarioNote("", Rect.zero).Color;

        private readonly ScenarioGraphView graphView;
        private readonly Label textLabel;
        private readonly TextField textField;
        private string savedText;
        private string textBeforeEditing;
        private int editingUndoGroup = -1;


        public ScenarioNoteView(ScenarioGraphView graphView, ScenarioNote note)
        {
            this.graphView = graphView;
            NoteId = note.Id;
            savedText = note.Text ?? "";

            capabilities |= Capabilities.Selectable | Capabilities.Movable | Capabilities.Deletable
                            | Capabilities.Resizable | Capabilities.Ascendable | Capabilities.Copiable;

            AddToClassList("scenario-note");
            style.backgroundColor = note.Color.a > 0f ? note.Color : DefaultColor;

            textLabel = new Label();
            textLabel.AddToClassList("scenario-note__text");

            textField = new TextField { multiline = true };
            textField.AddToClassList("scenario-note__field");
            textField.style.display = DisplayStyle.None;
            textField.RegisterValueChangedCallback(change => SaveTypedText());
            textField.RegisterCallback<FocusOutEvent>(_ => FinishEditing(keepsText: true));
            textField.RegisterCallback<KeyDownEvent>(HandleEditingKey, TrickleDown.TrickleDown);

            Add(textLabel);
            Add(textField);
            Add(new ResizableElement());

            RefreshText();
            SetPosition(note.Area);

            RegisterCallback<MouseDownEvent>(HandleDoubleClick);
            this.AddManipulator(new ContextualMenuManipulator(BuildNoteMenu));
        }


        public string NoteId { get; }

        public bool IsEditing => textField.style.display == DisplayStyle.Flex;


        // ── Слой ─────────────────────────────────────────────────────────

        public override void OnSelected()
        {
            base.OnSelected();
            RefreshLayer();
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            RefreshLayer();
        }

        // Заметка в одном слое с нодами, но в самом низу; пока с ней работают — наверху, иначе её не видно целиком.
        public void RefreshLayer()
        {
            if (selected || IsEditing)
                BringToFront();
            else
                SendToBack();
        }


        // ── Текст ────────────────────────────────────────────────────────

        public void StartEditing()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Edit Comment");
            editingUndoGroup = Undo.GetCurrentGroup();
            textBeforeEditing = savedText;

            textField.SetValueWithoutNotify(savedText);
            textLabel.style.display = DisplayStyle.None;
            textField.style.display = DisplayStyle.Flex;
            RefreshLayer();

            textField.schedule.Execute(() => textField.Focus());
        }

        /// <summary>Записать набранное в граф: по ходу набора и перед пересборкой холста. Вся правка — одна запись undo.</summary>
        public void SaveTypedText()
        {
            if (!IsEditing || textField.value == savedText || graphView.Editing == null)
                return;

            savedText = textField.value;
            graphView.Editing.SetNoteText(NoteId, savedText);

            if (editingUndoGroup >= 0)
                Undo.CollapseUndoOperations(editingUndoGroup);

            graphView.RememberShownData();
        }

        private void HandleDoubleClick(MouseDownEvent mouseEvent)
        {
            if (mouseEvent.clickCount != 2 || mouseEvent.button != (int)MouseButton.LeftMouse)
                return;

            StartEditing();
            mouseEvent.StopPropagation();
        }

        // Esc откатывает всю правку целиком — одной записью undo.
        private void HandleEditingKey(KeyDownEvent keyEvent)
        {
            if (keyEvent.keyCode != KeyCode.Escape)
                return;

            var undoGroup = editingUndoGroup;
            var hasSavedChanges = savedText != textBeforeEditing;

            savedText = textBeforeEditing;
            FinishEditing(keepsText: false);

            if (hasSavedChanges && undoGroup >= 0)
                Undo.RevertAllDownToGroup(undoGroup);

            keyEvent.StopImmediatePropagation();
        }

        private void FinishEditing(bool keepsText)
        {
            if (!IsEditing)
                return;

            if (keepsText)
                SaveTypedText();

            textField.style.display = DisplayStyle.None;
            textLabel.style.display = DisplayStyle.Flex;
            editingUndoGroup = -1;

            RefreshText();
            RefreshLayer();
        }

        private void RefreshText()
        {
            var isEmpty = string.IsNullOrEmpty(savedText);

            textLabel.text = isEmpty ? Placeholder : savedText;
            textLabel.EnableInClassList("scenario-note__text--empty", isEmpty);
        }


        // ── Цвет ─────────────────────────────────────────────────────────

        private void BuildNoteMenu(ContextualMenuPopulateEvent menuEvent)
        {
            menuEvent.menu.AppendAction("Edit Text", _ => StartEditing());
            menuEvent.menu.AppendAction("Color/Default", _ => SetColor(DefaultColor));

            foreach (var (name, color) in Palette)
                menuEvent.menu.AppendAction($"Color/{name}", _ => SetColor(color));

            menuEvent.menu.AppendSeparator();
        }

        private void SetColor(Color color)
        {
            graphView.ApplyEdit(editing => editing.SetNoteColor(NoteId, color));
        }


        // ── Размер ───────────────────────────────────────────────────────

        // IResizable
        public void OnStartResize()
        {
        }

        // IResizable
        public void OnResized()
        {
            var area = ScenarioGraphView.AreaFromStyle(this);
            graphView.ApplyEdit(editing => editing.SetNoteArea(NoteId, area));
        }


        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }
    }
}
