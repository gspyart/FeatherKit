using System;
using FeatherKit.LevelNotes;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Поле текста новой заметки у курсора, по образцу окна групп (<see cref="ObjectGroupWindow"/>).
    /// Сверху — тип кнопками цвета флажка, ниже — многострочный текст.
    /// Enter — создать, Shift+Enter — новая строка, Tab — следующий тип, Esc — отменить; клик мимо — создать, если текст есть.
    /// Заметку само не создаёт — отвечает тому, кто открыл.
    /// </summary>
    public class LevelNoteTextPopup : EditorWindow
    {
        private const float WindowWidth = 340f;
        private const float WindowHeight = 150f;
        private const float Padding = 6f;
        private const float KindRowHeight = 20f;
        private const float HintHeight = 16f;
        private const string TextFieldControl = "LevelNoteText";
        private const string Hint = "Enter — создать · Shift+Enter — строка · Tab — тип · Esc — отмена";

        private static GUIStyle textAreaStyle;
        private static GUIStyle kindLabelStyle;

        private string text = "";
        private LevelNoteKind kind;
        private bool isTextFocusRequested = true;
        private bool isFinished;

        private Action<LevelNoteKind> kindChanged;
        private Action<string, LevelNoteKind> confirmed;
        private Action closed;

        // Кто был в фокусе до окна и где курсор — после закрытия им возвращается фокус (<see cref="PopupFocusRestorer"/>).
        private EditorWindow windowFocusedBeforeOpen;
        private Vector2 openScreenPosition;


        private static GUIStyle TextAreaStyle => textAreaStyle ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };

        private static GUIStyle KindLabelStyle => kindLabelStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };

        private bool HasText => text.Trim().Length > 0;


        // ── Открытие ─────────────────────────────────────

        public static void Open(Vector2 screenPosition, LevelNoteKind kind, Action<LevelNoteKind> kindChanged,
            Action<string, LevelNoteKind> confirmed, Action closed)
        {
            foreach (var openWindow in Resources.FindObjectsOfTypeAll<LevelNoteTextPopup>())
                openWindow.Close();

            var window = CreateInstance<LevelNoteTextPopup>();
            window.kind = kind;
            window.kindChanged = kindChanged;
            window.confirmed = confirmed;
            window.closed = closed;
            window.windowFocusedBeforeOpen = focusedWindow;
            window.openScreenPosition = screenPosition;

            window.ShowPopup();
            window.position = new Rect(screenPosition, new Vector2(WindowWidth, WindowHeight));
            window.Focus();
        }


        // ── Клавиатура ───────────────────────────────────

        // Разбираем до поля: иначе Enter и Tab съест само поле.
        private void HandleKeyboard()
        {
            var currentEvent = Event.current;

            if (currentEvent.type != EventType.KeyDown)
                return;

            var isEnter = currentEvent.keyCode is KeyCode.Return or KeyCode.KeypadEnter || currentEvent.character is '\n' or '\r';

            if (isEnter && !currentEvent.shift)
            {
                currentEvent.Use();
                Confirm();
            }
            else if (currentEvent.keyCode == KeyCode.Tab || currentEvent.character == '\t')
            {
                currentEvent.Use();

                if (currentEvent.keyCode == KeyCode.Tab)
                    SelectKind(NextKind(currentEvent.shift ? -1 : 1));
            }
            else if (currentEvent.keyCode == KeyCode.Escape)
            {
                currentEvent.Use();
                Finish();
            }
        }

        private LevelNoteKind NextKind(int step)
        {
            var kinds = LevelNoteKindStyle.AllKinds;
            var index = 0;

            for (var kindIndex = 0; kindIndex < kinds.Count; kindIndex++)
            {
                if (kinds[kindIndex] == kind)
                    index = kindIndex;
            }

            return kinds[(index + step + kinds.Count) % kinds.Count];
        }


        // ── Тип ──────────────────────────────────────────

        private void DrawKindButtons()
        {
            var kinds = LevelNoteKindStyle.AllKinds;
            var buttonWidth = (position.width - Padding * 2f) / kinds.Count;

            for (var index = 0; index < kinds.Count; index++)
            {
                var buttonKind = kinds[index];
                var buttonRect = new Rect(Padding + index * buttonWidth, Padding, buttonWidth - 2f, KindRowHeight);
                var isCurrent = buttonKind == kind;

                var color = LevelNoteKindStyle.ColorOf(buttonKind);
                EditorGUI.DrawRect(buttonRect, isCurrent ? color : color * new Color(1f, 1f, 1f, 0.25f));

                KindLabelStyle.normal.textColor = isCurrent ? Color.black : color;

                if (GUI.Button(buttonRect, LevelNoteKindStyle.LabelOf(buttonKind), KindLabelStyle))
                {
                    SelectKind(buttonKind);
                    isTextFocusRequested = true;
                }
            }
        }

        private void SelectKind(LevelNoteKind newKind)
        {
            kind = newKind;
            kindChanged?.Invoke(kind);
            Repaint();
        }


        // ── Текст ────────────────────────────────────────

        private void DrawTextField()
        {
            var textTop = Padding * 2f + KindRowHeight;
            var textRect = new Rect(Padding, textTop, position.width - Padding * 2f, position.height - textTop - HintHeight - Padding);

            GUI.SetNextControlName(TextFieldControl);
            text = EditorGUI.TextArea(textRect, text, TextAreaStyle);

            if (isTextFocusRequested && Event.current.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl(TextFieldControl);
                isTextFocusRequested = false;
            }

            var hintRect = new Rect(Padding, position.height - HintHeight - Padding * 0.5f, position.width - Padding * 2f, HintHeight);
            GUI.Label(hintRect, Hint, EditorStyles.centeredGreyMiniLabel);
        }


        // ── Завершение ───────────────────────────────────

        // Пустой текст — не заметка: окно просто закрывается.
        private void Confirm()
        {
            if (!isFinished && HasText)
                confirmed?.Invoke(text.Trim(), kind);

            Finish();
        }


        private void Finish()
        {
            if (isFinished)
                return;

            isFinished = true;
            closed?.Invoke();
            Close();
        }


        // ── Unity ────────────────────────────────────────

        private void OnDestroy()
        {
            if (!isFinished)
            {
                isFinished = true;
                closed?.Invoke();
            }

            PopupFocusRestorer.RestoreAfterClose(windowFocusedBeforeOpen, openScreenPosition);
        }

        private void OnGUI()
        {
            HandleKeyboard();
            DrawKindButtons();
            DrawTextField();
        }

        // Клик мимо — как Enter: набранный текст не теряется, лишнюю заметку снимет Ctrl+Z.
        private void OnLostFocus()
        {
            if (this != null && !isFinished)
                Confirm();
        }
    }
}
