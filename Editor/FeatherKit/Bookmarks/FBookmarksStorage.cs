using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Хранилище закладок: общее, в ProjectSettings проекта — попадает в git.
    /// - вкладки, в каждой свои папки и закладки; их порядок и что сейчас активно
    /// - все правки с отменой через Undo и сразу на диск
    /// </summary>
    [FilePath("ProjectSettings/FeatherKit/Bookmarks.asset", FilePathAttribute.Location.ProjectFolder)]
    public class FBookmarksStorage : ScriptableSingleton<FBookmarksStorage>
    {
        private const string DefaultTabName = "Основная";

        [SerializeField] private List<FBookmarkTab> tabs = new List<FBookmarkTab>();
        [SerializeField] private int activeTabIndex;


        /// <summary>Что-то поменялось — окну пора перерисоваться.</summary>
        public event Action Changed;


        public IReadOnlyList<FBookmarkTab> Tabs => tabs;

        /// <summary>Открытая вкладка. Нет вкладок — заводится «Основная».</summary>
        public FBookmarkTab ActiveTab
        {
            get
            {
                if (tabs.Count == 0)
                    tabs.Add(new FBookmarkTab(DefaultTabName));

                return tabs[Mathf.Clamp(activeTabIndex, 0, tabs.Count - 1)];
            }
        }

        /// <summary>Куда кладут закладки из контекстного меню: активная папка открытой вкладки.</summary>
        public FBookmarkFolder ActiveFolder => ActiveTab.ActiveFolder;


        // ── Вкладки ──────────────────────────────────────────────────────

        public FBookmarkTab AddTab(string name)
        {
            RecordUndo("Новая вкладка закладок");

            var tab = new FBookmarkTab(name);
            tabs.Add(tab);
            activeTabIndex = tabs.Count - 1;

            SaveAndNotify();

            return tab;
        }


        public void RenameTab(FBookmarkTab tab, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || tab.Name == name)
                return;

            RecordUndo("Переименовать вкладку закладок");
            tab.Name = name.Trim();
            SaveAndNotify();
        }


        // Удалили открытую — откроется соседняя, что встала на её место.
        public void RemoveTab(FBookmarkTab tab)
        {
            var active = ActiveTab;

            RecordUndo("Удалить вкладку закладок");
            tabs.Remove(tab);

            activeTabIndex = tabs.Contains(active)
                ? tabs.IndexOf(active)
                : Mathf.Clamp(activeTabIndex, 0, Mathf.Max(0, tabs.Count - 1));

            SaveAndNotify();
        }


        public void MoveTab(FBookmarkTab tab, int targetIndex)
        {
            var from = tabs.IndexOf(tab);

            if (from < 0)
                return;

            var active = ActiveTab;

            RecordUndo("Переставить вкладку закладок");
            MoveInList(tabs, from, targetIndex);
            activeTabIndex = tabs.IndexOf(active);
            SaveAndNotify();
        }


        // Какая вкладка открыта — вид, а не содержимое: без Undo, как и свёрнутость папок.
        public void SetActiveTab(FBookmarkTab tab)
        {
            var index = tabs.IndexOf(tab);

            if (index < 0 || index == activeTabIndex)
                return;

            activeTabIndex = index;
            SaveAndNotify();
        }


        // ── Папки открытой вкладки ───────────────────────────────────────

        public FBookmarkFolder AddFolder(string name)
        {
            RecordUndo("Новая папка закладок");

            var folder = new FBookmarkFolder(name);
            ActiveTab.Folders.Add(folder);
            ActiveTab.SetActiveFolder(folder);

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
            var tab = ActiveTab;
            var active = tab.ActiveFolder;

            RecordUndo("Удалить папку закладок");
            tab.Folders.Remove(folder);
            tab.SetActiveFolder(active);
            SaveAndNotify();
        }


        public void MoveFolder(FBookmarkFolder folder, int targetIndex)
        {
            var tab = ActiveTab;
            var from = tab.Folders.IndexOf(folder);

            if (from < 0)
                return;

            var active = tab.ActiveFolder;

            RecordUndo("Переставить папку закладок");
            MoveInList(tab.Folders, from, targetIndex);
            tab.SetActiveFolder(active);
            SaveAndNotify();
        }


        public void SetActiveFolder(FBookmarkFolder folder)
        {
            if (ActiveTab.SetActiveFolder(folder))
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


        /// <summary>Скопировать в активную папку другой вкладки. Возвращает false, если там уже есть такая.</summary>
        public bool CopyBookmarkToTab(FBookmark bookmark, FBookmarkTab tab)
        {
            var folder = tab.ActiveFolder;

            if (folder.Contains(bookmark.GlobalId))
                return false;

            RecordUndo("Копировать закладку во вкладку");
            folder.Bookmarks.Add(bookmark.Copy());
            folder.IsExpanded = true;
            SaveAndNotify();

            return true;
        }


        /// <summary>Поискать заново ненайденные: открылась сцена, изменилась иерархия или проект.</summary>
        public void RetryNotFound()
        {
            foreach (var tab in tabs)
            {
                foreach (var folder in tab.Folders)
                {
                    foreach (var bookmark in folder.Bookmarks)
                        bookmark.RetryIfNotFound();
                }
            }

            Changed?.Invoke();
        }


        // ── Общие помощники ──────────────────────────────────────────────

        // Место считается до выемки: бросили ниже себя — после выемки индекс на один меньше.
        private static void MoveInList<T>(List<T> list, int from, int targetIndex)
        {
            var item = list[from];

            list.RemoveAt(from);
            list.Insert(Mathf.Clamp(from < targetIndex ? targetIndex - 1 : targetIndex, 0, list.Count), item);
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
