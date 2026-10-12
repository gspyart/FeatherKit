using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Хранилище закладок: личное, в UserSettings проекта — в git не попадает.
    /// - папки и закладки, их порядок и активная папка
    /// - все правки с отменой через Undo и сразу на диск
    /// </summary>
    [FilePath("UserSettings/FeatherKit/Bookmarks.asset", FilePathAttribute.Location.ProjectFolder)]
    public class FBookmarksStorage : ScriptableSingleton<FBookmarksStorage>
    {
        private const string DefaultFolderName = "Общее";

        [SerializeField] private List<FBookmarkFolder> folders = new List<FBookmarkFolder>();
        [SerializeField] private int activeFolderIndex;


        /// <summary>Что-то поменялось — окну пора перерисоваться.</summary>
        public event Action Changed;


        public IReadOnlyList<FBookmarkFolder> Folders => folders;

        /// <summary>Куда кладут закладки из контекстного меню. Нет папок — заводится «Общее».</summary>
        public FBookmarkFolder ActiveFolder
        {
            get
            {
                if (folders.Count == 0)
                    folders.Add(new FBookmarkFolder(DefaultFolderName));

                return folders[Mathf.Clamp(activeFolderIndex, 0, folders.Count - 1)];
            }
        }


        // ── Папки ────────────────────────────────────────────────────────

        public FBookmarkFolder AddFolder(string name)
        {
            RecordUndo("Новая папка закладок");

            var folder = new FBookmarkFolder(name);
            folders.Add(folder);
            activeFolderIndex = folders.Count - 1;

            SaveAndNotify();

            return folder;
        }


        public void RenameFolder(FBookmarkFolder folder, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || folder.Name == name)
                return;

            RecordUndo("Переименовать папку закладок");
            folder.Name = name.Trim();
            SaveAndNotify();
        }


        public void RemoveFolder(FBookmarkFolder folder)
        {
            RecordUndo("Удалить папку закладок");
            folders.Remove(folder);
            SaveAndNotify();
        }


        public void MoveFolder(FBookmarkFolder folder, int targetIndex)
        {
            var from = folders.IndexOf(folder);

            if (from < 0)
                return;

            RecordUndo("Переставить папку закладок");
            folders.RemoveAt(from);
            folders.Insert(Mathf.Clamp(from < targetIndex ? targetIndex - 1 : targetIndex, 0, folders.Count), folder);
            SaveAndNotify();
        }


        public void SetActiveFolder(FBookmarkFolder folder)
        {
            var index = folders.IndexOf(folder);

            if (index < 0 || index == activeFolderIndex)
                return;

            activeFolderIndex = index;
            SaveAndNotify();
        }


        // Свёрнутость — вид, а не содержимое: без Undo, иначе отмена листала бы клики по папкам.
        public void SetExpanded(FBookmarkFolder folder, bool isExpanded)
        {
            folder.IsExpanded = isExpanded;
            SaveAndNotify();
        }


        // ── Закладки ─────────────────────────────────────────────────────

        /// <summary>Добавить в папку. Возвращает, сколько не вышло: объекты из игры, несохранённой сцены.</summary>
        public int AddBookmarks(FBookmarkFolder folder, IReadOnlyList<Object> targets, int insertIndex = -1)
        {
            RecordUndo("Добавить в закладки");

            var rejected = 0;
            var index = insertIndex < 0 ? folder.Bookmarks.Count : insertIndex;

            for (var i = 0; i < targets.Count; i++)
            {
                if (!FBookmark.TryCreate(targets[i], out var bookmark))
                {
                    rejected++;
                    continue;
                }

                if (folder.Contains(bookmark.GlobalId))
                    continue;

                folder.Bookmarks.Insert(Mathf.Min(index++, folder.Bookmarks.Count), bookmark);
            }

            folder.IsExpanded = true;
            SaveAndNotify();

            return rejected;
        }


        public void RemoveBookmark(FBookmarkFolder folder, FBookmark bookmark)
        {
            RecordUndo("Удалить закладку");
            folder.Bookmarks.Remove(bookmark);
            SaveAndNotify();
        }


        // Та же закладка в целевой папке уже есть — просто убираем дубль из старой.
        public void MoveBookmark(FBookmark bookmark, FBookmarkFolder from, FBookmarkFolder to, int targetIndex)
        {
            var fromIndex = from.Bookmarks.IndexOf(bookmark);

            if (fromIndex < 0)
                return;

            RecordUndo("Переставить закладку");
            from.Bookmarks.RemoveAt(fromIndex);

            if (from == to && fromIndex < targetIndex)
                targetIndex--;

            if (from == to || !to.Contains(bookmark.GlobalId))
                to.Bookmarks.Insert(Mathf.Clamp(targetIndex, 0, to.Bookmarks.Count), bookmark);

            SaveAndNotify();
        }


        /// <summary>Поискать заново ненайденные: открылась сцена, изменилась иерархия или проект.</summary>
        public void RetryNotFound()
        {
            foreach (var folder in folders)
            {
                foreach (var bookmark in folder.Bookmarks)
                    bookmark.RetryIfNotFound();
            }

            Changed?.Invoke();
        }


        // ── Сохранение ───────────────────────────────────────────────────

        private void RecordUndo(string actionName)
        {
            Undo.RecordObject(this, actionName);
        }


        private void SaveAndNotify()
        {
            Save(true);
            Changed?.Invoke();
        }

        // После отмены данные уже прежние, осталось записать их и показать.
        private void OnUndoRedo()
        {
            SaveAndNotify();
        }


        // ── Unity ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }
    }
}
