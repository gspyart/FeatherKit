using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Колонка вкладок слева в окне закладок — часть окна.
    /// - список вкладок: клик открывает, двойной клик переименовывает
    /// - создать, переименовать и удалить вкладку
    /// - край колонки тянут мышью, чтобы поменять ширину
    /// </summary>
    public class FBookmarkTabsColumn
    {
        private const string NewTabName = "Новая вкладка";
        private const float MinWidth = 70f;
        private const float MinListWidth = 120f;
        private const float ResizeEdgeWidth = 4f;
        private const float LabelPadding = 6f;

        private static readonly Color ActiveTabColor = new Color(0.24f, 0.48f, 0.9f, 0.35f);
        private static readonly Color EdgeColor = new Color(0f, 0f, 0f, 0.3f);

        private readonly EditorWindow owner;
        private readonly FBookmarksStorage storage;
        private readonly FBookmarksDragAndDrop dragAndDrop;
        private readonly FBookmarksRenameField renameField;

        private Vector2 scroll;


        public FBookmarkTabsColumn(EditorWindow owner, FBookmarksStorage storage,
            FBookmarksDragAndDrop dragAndDrop, FBookmarksRenameField renameField)
        {
            this.owner = owner;
            this.storage = storage;
            this.dragAndDrop = dragAndDrop;
            this.renameField = renameField;
        }


        // ── Колонка ──────────────────────────────────────────────────────

        /// <summary>Нарисовать колонку шириной width. Возвращает новую ширину, если край потянули.</summary>
        public float Draw(float width)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(width)))
            {
                DrawToolbar();
                DrawTabs();
            }

            var edge = GUILayoutUtility.GetRect(ResizeEdgeWidth, ResizeEdgeWidth,
                GUILayout.Width(ResizeEdgeWidth), GUILayout.ExpandHeight(true));

            return ResizeByEdge(edge, width);
        }


        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Вкладки", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                var addTab = new GUIContent(EditorGUIUtility.IconContent("Toolbar Plus").image, "Новая вкладка");

                if (GUILayout.Button(addTab, EditorStyles.toolbarButton, GUILayout.Width(24f)))
                    AddTab();
            }
        }


        private void DrawTabs()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            for (var i = 0; i < storage.Tabs.Count; i++)
                DrawTab(storage.Tabs[i], i);

            var emptySpace = GUILayoutUtility.GetRect(0f, FBookmarkRowView.Height, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            HandleEmptySpaceClicks(emptySpace);
            dragAndDrop.HandleTabsEmptySpace(emptySpace);

            dragAndDrop.DrawIndicator();
            EditorGUILayout.EndScrollView();
        }

        private void DrawTab(FBookmarkTab tab, int tabIndex)
        {
            var rect = GUILayoutUtility.GetRect(0f, FBookmarkRowView.Height, GUILayout.ExpandWidth(true));
            var labelRect = new Rect(rect.x + LabelPadding, rect.y, rect.width - LabelPadding, rect.height);

            if (Event.current.type == EventType.Repaint && tab == storage.ActiveTab)
                EditorGUI.DrawRect(rect, ActiveTabColor);

            if (renameField.IsRenaming(tab))
                renameField.Draw(labelRect);
            else
                GUI.Label(labelRect, new GUIContent(tab.Name, tab.Name));

            HandleTabClicks(tab, rect);
            dragAndDrop.HandleTabRow(tab, tabIndex, rect);
        }


        // ── Клики ────────────────────────────────────────────────────────

        // Клик — открыть, двойной — переименовать.
        private void HandleTabClicks(FBookmarkTab tab, Rect rect)
        {
            var current = Event.current;

            if (!rect.Contains(current.mousePosition) || renameField.IsRenaming(tab))
                return;

            if (current.type == EventType.ContextClick)
            {
                ShowTabMenu(tab);
                current.Use();
                return;
            }

            if (current.type != EventType.MouseDown || current.button != 0)
                return;

            renameField.Commit();
            current.Use();

            if (current.clickCount == 2)
            {
                StartRename(tab);
                return;
            }

            dragAndDrop.BeginPendingDrag(tab);

            if (tab == storage.ActiveTab)
                return;

            storage.SetActiveTab(tab);
            GUIUtility.ExitGUI(); // справа уже другой список — дорисовывать этот кадр нельзя
        }


        private void ShowTabMenu(FBookmarkTab tab)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Переименовать"), false, () => StartRename(tab));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Новая вкладка"), false, AddTab);
            menu.AddItem(new GUIContent("Удалить вкладку"), false, () => RemoveTabWithConfirmation(tab));
            menu.ShowAsContext();
        }


        private void HandleEmptySpaceClicks(Rect rect)
        {
            var current = Event.current;

            if (current.type != EventType.ContextClick || !rect.Contains(current.mousePosition))
                return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Новая вкладка"), false, AddTab);
            menu.ShowAsContext();
            current.Use();
        }


        // ── Действия ─────────────────────────────────────────────────────

        private void AddTab()
        {
            StartRename(storage.AddTab(NewTabName));
        }


        private void StartRename(FBookmarkTab tab)
        {
            renameField.Start(tab, tab.Name, name => storage.RenameTab(tab, name));
        }


        private void RemoveTabWithConfirmation(FBookmarkTab tab)
        {
            var bookmarkCount = 0;

            foreach (var folder in tab.Folders)
                bookmarkCount += folder.Bookmarks.Count;

            var isConfirmed = bookmarkCount == 0 || EditorUtility.DisplayDialog("Удалить вкладку",
                $"Вкладка «{tab.Name}»: папок {tab.Folders.Count}, закладок {bookmarkCount}. Вернуть можно через Undo.",
                "Удалить", "Отмена");

            if (isConfirmed)
                storage.RemoveTab(tab);
        }


        // ── Ширина ───────────────────────────────────────────────────────

        // Колонка начинается от левого края окна, так что новая ширина — просто где сейчас мышь.
        private float ResizeByEdge(Rect edge, float width)
        {
            var current = Event.current;
            var controlId = GUIUtility.GetControlID(FocusType.Passive);

            EditorGUIUtility.AddCursorRect(edge, MouseCursor.ResizeHorizontal);

            if (current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(edge.center.x - 0.5f, edge.y, 1f, edge.height), EdgeColor);

            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown when current.button == 0 && edge.Contains(current.mousePosition):
                    GUIUtility.hotControl = controlId;
                    current.Use();
                    break;

                case EventType.MouseDrag when GUIUtility.hotControl == controlId:
                    width = ClampWidth(current.mousePosition.x - ResizeEdgeWidth * 0.5f);
                    current.Use();
                    owner.Repaint();
                    break;

                case EventType.MouseUp when GUIUtility.hotControl == controlId:
                    GUIUtility.hotControl = 0;
                    current.Use();
                    break;
            }

            return width;
        }

        // Колонка не схлопывается и оставляет место списку.
        private float ClampWidth(float width)
        {
            return Mathf.Clamp(width, MinWidth, Mathf.Max(MinWidth, owner.position.width - MinListWidth));
        }
    }
}
