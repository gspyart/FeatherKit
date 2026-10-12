using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Поле переименования папки прямо в строке окна закладок — часть окна.
    /// - Enter или уход фокуса — принять, Esc — отменить
    /// </summary>
    public class FBookmarkFolderRenameField
    {
        private const string ControlName = "FeatherKit.Bookmarks.Rename";

        private readonly EditorWindow owner;
        private readonly FBookmarksStorage storage;

        private FBookmarkFolder folder;
        private string text;
        private bool isFocusPending;
        private bool wasFocused;


        public FBookmarkFolderRenameField(EditorWindow owner, FBookmarksStorage storage)
        {
            this.owner = owner;
            this.storage = storage;
        }


        public bool IsRenamingAny => folder != null;


        public bool IsRenaming(FBookmarkFolder target)
        {
            return folder != null && folder == target;
        }


        public void Start(FBookmarkFolder target)
        {
            folder = target;
            text = target.Name;
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
                folder = null;

            GUI.FocusControl(null);
            Event.current.Use();
            GUIUtility.ExitGUI(); // поле исчезло посреди кадра — дорисовывать его нельзя
        }


        public void Commit()
        {
            if (folder == null)
                return;

            var renamed = folder;
            folder = null;
            storage.RenameFolder(renamed, text);
        }


        // Ушёл фокус — имя принимаем, как в окне Project. Пока фокус ни разу не пришёл, ждём его.
        public void CommitOnFocusLoss()
        {
            if (folder == null || isFocusPending || Event.current.type != EventType.Repaint)
                return;

            if (GUI.GetNameOfFocusedControl() == ControlName)
                wasFocused = true;
            else if (wasFocused)
                Commit();
        }
    }
}
