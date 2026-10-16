using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Окно закладок: ассеты проекта и объекты сцен, разложенные по вкладкам и папкам.
    /// - раскладывает папки и закладки открытой вкладки, ищет по имени
    /// - клики, контекстные меню и клавиши
    /// Хранит FBookmarksStorage; колонка вкладок, перетаскивание, переименование и вид строки — части окна.
    /// </summary>
    public class FBookmarksWindow : EditorWindow
    {
        private const string NewFolderName = "Новая папка";
        private const float FoldoutWidth = 16f;
        private const float MinEmptySpaceHeight = 40f;
        private const float DefaultTabsColumnWidth = 110f;

        private static readonly Color FolderRowColor = new Color(0f, 0f, 0f, 0.12f);
        private static readonly Color ActiveFolderMarkColor = new Color(0.25f, 0.6f, 1f);

        [SerializeField] private float tabsColumnWidth = DefaultTabsColumnWidth;

        private FBookmarksDragAndDrop dragAndDrop;
        private FBookmarksRenameField renameField;
        private FBookmarkTabsColumn tabsColumn;
        private Vector2 scroll;
        private string search = string.Empty;
        private FBookmark selectedBookmark;


        private static FBookmarksStorage Storage => FBookmarksStorage.instance;

        private bool IsSearching => !string.IsNullOrEmpty(search);


        // ── Открытие ─────────────────────────────────────────────────────

        public static FBookmarksWindow Open()
        {
            var window = GetWindow<FBookmarksWindow>();
            window.titleContent = new GUIContent("Bookmarks", EditorGUIUtility.IconContent("Favorite Icon").image);
            window.minSize = new Vector2(320f, 160f);

            return window;
        }

        [MenuItem("FeatherKit/Bookmarks")]
        private static void OpenFromMenu()
        {
            Open();
        }


        public static string DescribeRejected(int count)
        {
            return $"Не добавлено: {count}. Объекты из игры и несохранённых сцен закладками не бывают";
        }


        public static string DescribeCopy(bool isCopied, FBookmarkTab tab)
        {
            return isCopied ? $"Скопировано во вкладку «{tab.Name}»" : $"Во вкладке «{tab.Name}» такая уже есть";
        }


        // ── Панель сверху ────────────────────────────────────────────────

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var addFolder = new GUIContent(" Папка", EditorGUIUtility.IconContent("Toolbar Plus").image, "Новая папка");

                if (GUILayout.Button(addFolder, EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    AddFolder();

                GUILayout.Space(FBookmarkRowView.Gap);
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            }
        }


        // ── Список ───────────────────────────────────────────────────────

        private void DrawList()
        {
            var folders = Storage.ActiveTab.Folders;

            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (folders.Count == 0)
                EditorGUILayout.HelpBox("Перетащи сюда ассеты из Project или объекты из Hierarchy.\n" +
                                        "Или ПКМ по ним → «Добавить в закладки».", MessageType.Info);

            for (var i = 0; i < folders.Count; i++)
                DrawFolder(folders[i], i);

            var emptySpace = GUILayoutUtility.GetRect(0f, MinEmptySpaceHeight, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            HandleEmptySpaceClicks(emptySpace);
            dragAndDrop.HandleEmptySpace(emptySpace);

            dragAndDrop.DrawIndicator();
            EditorGUILayout.EndScrollView();
        }

        // На поиске папки раскрыты: иначе найденное пряталось бы в свёрнутых.
        private void DrawFolder(FBookmarkFolder folder, int folderIndex)
        {
            if (IsSearching && !folder.Bookmarks.Exists(IsMatch))
                return;

            var folderRect = GUILayoutUtility.GetRect(0f, FBookmarkRowView.Height, GUILayout.ExpandWidth(true));
            DrawFolderRow(folder, folderRect);
            HandleFolderClicks(folder, folderRect);
            dragAndDrop.HandleFolderRow(folder, folderIndex, folderRect);

            if (!folder.IsExpanded && !IsSearching)
                return;

            for (var i = 0; i < folder.Bookmarks.Count; i++)
            {
                var bookmark = folder.Bookmarks[i];

                if (!IsMatch(bookmark))
                    continue;

                var rowRect = GUILayoutUtility.GetRect(0f, FBookmarkRowView.Height, GUILayout.ExpandWidth(true));
                FBookmarkRowView.Draw(rowRect, bookmark, bookmark == selectedBookmark);
                HandleBookmarkClicks(folder, bookmark, rowRect);
                dragAndDrop.HandleBookmarkRow(folder, i, rowRect);
            }
        }

        private bool IsMatch(FBookmark bookmark)
        {
            return !IsSearching || bookmark.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }


        // ── Папка ────────────────────────────────────────────────────────

        private void DrawFolderRow(FBookmarkFolder folder, Rect rect)
        {
            var isExpanded = folder.IsExpanded || IsSearching;
            var iconSize = FBookmarkRowView.IconSize;
            var gap = FBookmarkRowView.Gap;
            var iconRect = new Rect(rect.x + FoldoutWidth + 2f, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize);
            var labelRect = new Rect(iconRect.xMax + gap, rect.y, rect.xMax - iconRect.xMax - gap, rect.height);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, FolderRowColor);

                if (folder == Storage.ActiveFolder)
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), ActiveFolderMarkColor);

                EditorStyles.foldout.Draw(FoldoutRect(rect), false, false, isExpanded, false);
                GUI.DrawTexture(iconRect, EditorGUIUtility.IconContent(isExpanded ? "FolderOpened Icon" : "Folder Icon").image);
            }

            if (renameField.IsRenaming(folder))
                renameField.Draw(labelRect);
            else
                FBookmarkRowView.DrawNameWithNote(labelRect, new GUIContent(folder.Name), EditorStyles.boldLabel,
                    folder.Bookmarks.Count.ToString());
        }


        private static Rect FoldoutRect(Rect row)
        {
            return new Rect(row.x + 2f, row.y, FoldoutWidth, row.height);
        }


        // Стрелка или двойной клик — свернуть; клик по имени — сделать папку активной.
        private void HandleFolderClicks(FBookmarkFolder folder, Rect rect)
        {
            var current = Event.current;

            if (!rect.Contains(current.mousePosition) || renameField.IsRenaming(folder))
                return;

            if (current.type == EventType.ContextClick)
            {
                ShowFolderMenu(folder);
                current.Use();
                return;
            }

            if (current.type != EventType.MouseDown || current.button != 0)
                return;

            renameField.Commit();
            Storage.SetActiveFolder(folder);
            current.Use();

            if (FoldoutRect(rect).Contains(current.mousePosition) || current.clickCount == 2)
            {
                Storage.SetExpanded(folder, !folder.IsExpanded);
                GUIUtility.ExitGUI(); // строк стало другое число — дорисовывать этот кадр нельзя
            }

            dragAndDrop.BeginPendingDrag(folder, null);
        }


        private void ShowFolderMenu(FBookmarkFolder folder)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Переименовать  F2"), false, () => StartRename(folder));
            menu.AddItem(new GUIContent("Добавить выделенное"), false, () => AddSelection(folder));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Новая папка"), false, AddFolder);
            menu.AddItem(new GUIContent("Удалить папку"), false, () => RemoveFolderWithConfirmation(folder));
            menu.ShowAsContext();
        }


        private void AddFolder()
        {
            StartRename(Storage.AddFolder(NewFolderName));
        }


        private void StartRename(FBookmarkFolder folder)
        {
            renameField.Start(folder, folder.Name, name => Storage.RenameFolder(folder, name));
        }


        private void AddSelection(FBookmarkFolder folder)
        {
            var rejected = Storage.AddBookmarks(folder, Selection.objects);

            if (rejected > 0)
                ShowNotification(new GUIContent(DescribeRejected(rejected)));
        }


        private static void RemoveFolderWithConfirmation(FBookmarkFolder folder)
        {
            var isConfirmed = folder.Bookmarks.Count == 0 || EditorUtility.DisplayDialog("Удалить папку",
                $"Папка «{folder.Name}» и закладок в ней: {folder.Bookmarks.Count}. Вернуть можно через Undo.",
                "Удалить", "Отмена");

            if (isConfirmed)
                Storage.RemoveFolder(folder);
        }


        // ── Закладка ─────────────────────────────────────────────────────

        // Клик — выделить, двойной — открыть; заодно папка становится активной.
        private void HandleBookmarkClicks(FBookmarkFolder folder, FBookmark bookmark, Rect rect)
        {
            var current = Event.current;

            if (!rect.Contains(current.mousePosition))
                return;

            if (current.type == EventType.ContextClick)
            {
                selectedBookmark = bookmark;
                ShowBookmarkMenu(folder, bookmark);
                current.Use();
                return;
            }

            if (current.type != EventType.MouseDown || current.button != 0)
                return;

            renameField.Commit();
            selectedBookmark = bookmark;
            Storage.SetActiveFolder(folder);
            current.Use();

            if (current.clickCount == 2)
            {
                FBookmarkOpener.Open(bookmark);
                GUIUtility.ExitGUI(); // открытие сцены показывает окно-вопрос, после него кадр не дорисовать
            }

            FBookmarkOpener.Select(bookmark);
            dragAndDrop.BeginPendingDrag(folder, bookmark);
        }


        private void ShowBookmarkMenu(FBookmarkFolder folder, FBookmark bookmark)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Показать"), false, () => FBookmarkOpener.Select(bookmark));
            menu.AddItem(new GUIContent("Открыть"), false, () => FBookmarkOpener.Open(bookmark));
            menu.AddSeparator(string.Empty);

            foreach (var other in Storage.ActiveTab.Folders)
            {
                if (other == folder)
                    continue;

                // Косая черта в имени разрезала бы пункт меню на подменю.
                var target = other;
                var label = "Переместить в/" + other.Name.Replace('/', '∕');

                menu.AddItem(new GUIContent(label), false,
                    () => Storage.MoveBookmark(bookmark, folder, target, target.Bookmarks.Count));
            }

            foreach (var tab in Storage.Tabs)
            {
                if (tab == Storage.ActiveTab)
                    continue;

                var target = tab;
                var label = "Копировать во вкладку/" + tab.Name.Replace('/', '∕');

                menu.AddItem(new GUIContent(label), false, () => CopyToTab(bookmark, target));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Удалить из закладок  Del"), false, () => Storage.RemoveBookmark(folder, bookmark));
            menu.ShowAsContext();
        }


        private void CopyToTab(FBookmark bookmark, FBookmarkTab tab)
        {
            var isCopied = Storage.CopyBookmarkToTab(bookmark, tab);

            ShowNotification(new GUIContent(DescribeCopy(isCopied, tab)));
        }


        // ── Пустое место и клавиши ───────────────────────────────────────

        private void HandleEmptySpaceClicks(Rect rect)
        {
            var current = Event.current;

            if (!rect.Contains(current.mousePosition))
                return;

            if (current.type == EventType.MouseDown && current.button == 0)
            {
                renameField.Commit();
                selectedBookmark = null;
                Repaint();
            }
            else if (current.type == EventType.ContextClick)
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Новая папка"), false, AddFolder);
                menu.ShowAsContext();
                current.Use();
            }
        }


        // Пока печатают в поиске или в имени папки, клавиши — их: Backspace не должен удалять закладку.
        private void HandleKeyboard()
        {
            var current = Event.current;

            if (current.type != EventType.KeyDown || renameField.IsRenamingAny || EditorGUIUtility.editingTextField)
                return;

            if (current.keyCode == KeyCode.F2 && Storage.ActiveTab.Folders.Count > 0)
            {
                StartRename(Storage.ActiveFolder);
                current.Use();
            }
            else if ((current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace) && selectedBookmark != null)
            {
                RemoveSelectedBookmark();
                current.Use();
            }
        }

        private void RemoveSelectedBookmark()
        {
            foreach (var folder in Storage.ActiveTab.Folders)
            {
                if (!folder.Bookmarks.Contains(selectedBookmark))
                    continue;

                Storage.RemoveBookmark(folder, selectedBookmark);
                selectedBookmark = null;
                return;
            }
        }


        // ── Слежение за сценами и проектом ───────────────────────────────

        private static void RetryNotFound()
        {
            Storage.RetryNotFound();
        }

        private static void RetryNotFound(Scene scene, OpenSceneMode mode)
        {
            Storage.RetryNotFound();
        }


        // ── Unity ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            dragAndDrop = new FBookmarksDragAndDrop(this, Storage);
            renameField = new FBookmarksRenameField(this);
            tabsColumn = new FBookmarkTabsColumn(this, Storage, dragAndDrop, renameField);

            Storage.Changed += Repaint;
            EditorApplication.hierarchyChanged += RetryNotFound;
            EditorApplication.projectChanged += RetryNotFound;
            EditorSceneManager.sceneOpened += RetryNotFound;
        }

        private void OnDisable()
        {
            Storage.Changed -= Repaint;
            EditorApplication.hierarchyChanged -= RetryNotFound;
            EditorApplication.projectChanged -= RetryNotFound;
            EditorSceneManager.sceneOpened -= RetryNotFound;
        }

        // Вкладка нужна до раскладки: появись она посреди кадра — строк станет больше, чем Unity разложил.
        private void OnGUI()
        {
            _ = Storage.ActiveTab;
            dragAndDrop.BeginFrame();

            using (new EditorGUILayout.HorizontalScope())
            {
                tabsColumnWidth = tabsColumn.Draw(tabsColumnWidth);

                using (new EditorGUILayout.VerticalScope())
                {
                    DrawToolbar();
                    DrawList();
                }
            }

            dragAndDrop.EndFrame();
            HandleKeyboard();
            renameField.CommitOnFocusLoss();
        }
    }
}
