using System;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Поле переименования папки или вкладки прямо в строке окна закладок — часть окна.
    /// - за раз переименовывают что-то одно
    /// - Enter или уход фокуса — принять, Esc — отменить
    /// </summary>
    public class FBookmarksRenameField
    {
        private const string ControlName = "FeatherKit.Bookmarks.Rename";

        private readonly EditorWindow owner;

        private object target;
        private Action<string> applyName;
        private string text;
        private bool isFocusPending;
        private bool wasFocused;


        public FBookmarksRenameField(EditorWindow owner)
        {
            this.owner = owner;
        }


        public bool IsRenamingAny => target != null;


        public bool IsRenaming(object candidate)
        {
            return target != null && target == candidate;
        }


        /// <summary>Начать переименование: что переименовываем, его имя сейчас и как записать новое.</summary>
        public void Start(object renamed, string currentName, Action<string> apply)
        {
            target = renamed;
            applyName = apply;
            text = currentName;
            isFocusPending = true;
            wasFocused = false;
            owner.Repaint();
        }


        public void Draw(Rect rect)
        {
            var current = Event.current;

            if (current.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == ControlName)
            {
                if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
                    Finish(true);
                else if (current.keyCode == KeyCode.Escape)
                    Finish(false);
            }

            GUI.SetNextControlName(ControlName);
            text = EditorGUI.TextField(rect, text);

            if (isFocusPending && current.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl(ControlName);
                isFocusPending = false;
            }
        }

        private void Finish(bool isAccepted)
        {
            if (isAccepted)
                Commit();
            else
                target = null;

            GUI.FocusControl(null);
            Event.current.Use();
            GUIUtility.ExitGUI(); // поле исчезло посреди кадра — дорисовывать его нельзя
        }


        public void Commit()
        {
            if (target == null)
                return;

            var apply = applyName;
            target = null;
            applyName = null;
            apply(text);
        }


        // Ушёл фокус — имя принимаем, как в окне Project. Пока фокус ни разу не пришёл, ждём его.
        public void CommitOnFocusLoss()
        {
            if (target == null || isFocusPending || Event.current.type != EventType.Repaint)
                return;

            if (GUI.GetNameOfFocusedControl() == ControlName)
                wasFocused = true;
            else if (wasFocused)
                Commit();
        }
    }
}
