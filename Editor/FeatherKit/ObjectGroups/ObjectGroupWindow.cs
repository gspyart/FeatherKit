using System.Collections.Generic;
using System.Linq;
using FeatherKit.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Окно групп у курсора по клавише M, как Collection Manager в Blender. Сверху поиск, ниже последние группы и дерево
    /// всех групп сцены с переключателями (<see cref="ObjectGroupRowView"/>), внизу — новая группа (<see cref="ObjectGroupCreationBar"/>).
    /// Клик по имени или Enter — выделенное переезжает в группу; ничего не выделено — выделяется сама группа.
    /// Клик по иконке — цвет группы. ПКМ по строке — выделить группу или её содержимое, переименовать (F2), разгруппировать, цвет.
    /// Стрелки — по списку, печать — в поиск, Esc или клик мимо — закрыть.
    /// Клавиша — M в любом окне редактора, поменять — Edit → Shortcuts → FeatherKit.
    /// </summary>
    public class ObjectGroupWindow : EditorWindow
    {
        private const float WindowWidth = 420f;
        private const float HeaderHeight = 48f;
        private const float MaxListHeight = 440f;
        private const int MinVisibleRows = 4;
        private const float Padding = 6f;
        private const string SearchFieldControl = "ObjectGroupSearch";
        private const string RenameFieldControl = "ObjectGroupRename";
        private const string RenameUndoName = "Rename Group";

        private static readonly Color SeparatorColor = new(0f, 0f, 0f, 0.3f);

        private readonly ObjectGroupCreationBar creationBar = new();

        private List<Transform> objectsToMove = new();
        private int skippedCount;
        private Scene scene;
        private List<ObjectGroup> groups = new();
        private List<ObjectGroupRow> rows = new();
        private int highlightedIndex;
        private string searchText = string.Empty;
        private Vector2 scrollPosition;
        private bool isSearchFocusRequested = true;
        private ObjectGroup renamingGroup;
        private string renameText;
        private bool isRenameFocusRequested;

        // Пока открыто меню строки, окно теряет фокус — закрываться в это время нельзя.
        private bool isRowMenuOpen;

        // Кто был в фокусе до окна и где курсор — после закрытия им возвращается фокус и «мышь над тобой» (<see cref="PopupFocusRestorer"/>).
        private EditorWindow windowFocusedBeforeOpen;
        private Vector2 lastMouseScreenPosition;


        private bool HasObjectsToMove => objectsToMove.Count > 0;

        private string HeaderTitle => HasObjectsToMove
            ? $"Move {objectsToMove.Count} {(objectsToMove.Count == 1 ? "object" : "objects")} to group"
            : "Object Groups";

        // Высота под все строки, но не меньше нескольких и не выше предела — дальше прокрутка.
        private float FittingHeight
        {
            get
            {
                var listHeight = Mathf.Clamp(rows.Count * ObjectGroupRowView.Height, MinVisibleRows * ObjectGroupRowView.Height, MaxListHeight);
                return HeaderHeight + listHeight + ObjectGroupCreationBar.Height;
            }
        }

        private Rect ListRect => new(0f, HeaderHeight, position.width, position.height - HeaderHeight - ObjectGroupCreationBar.Height);


        // ── Открытие ─────────────────────────────────────

        [Shortcut("FeatherKit/Move to Group", KeyCode.M)]
        public static void Open()
        {
            foreach (var openWindow in Resources.FindObjectsOfTypeAll<ObjectGroupWindow>())
                openWindow.Close();

            var window = CreateInstance<ObjectGroupWindow>();
            window.objectsToMove = ObjectGroupMover.CollectMovableSelection(out window.skippedCount);
            window.scene = window.HasObjectsToMove ? window.objectsToMove[0].gameObject.scene : SceneBeingEdited();
            window.Refresh();

            window.windowFocusedBeforeOpen = EditorWindow.focusedWindow;
            window.lastMouseScreenPosition = MouseScreenPoint();
            window.ShowPopup();
            window.position = new Rect(window.lastMouseScreenPosition, new Vector2(WindowWidth, window.FittingHeight));
            window.Focus();
        }

        // Открыт префаб — его сцена, иначе активная.
        private static Scene SceneBeingEdited()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            return prefabStage != null ? prefabStage.scene : SceneManager.GetActiveScene();
        }

        // Окно встаёт углом к курсору; нет курсора — по центру редактора.
        private static Vector2 MouseScreenPoint()
        {
            return Event.current != null
                ? GUIUtility.GUIToScreenPoint(Event.current.mousePosition)
                : EditorGUIUtility.GetMainWindowPosition().center;
        }


        // ── Список ───────────────────────────────────────

        // Перечитать группы и строки — после любой правки из окна, поиска, отмены.
        private void Refresh()
        {
            groups = ObjectGroupRowList.GroupsIn(scene);

            var hasSearch = !string.IsNullOrWhiteSpace(searchText);
            var recentGroups = HasObjectsToMove && !hasSearch ? ObjectGroupRecentList.GroupsIn(scene) : new List<ObjectGroup>();

            rows = ObjectGroupRowList.Build(groups, recentGroups, searchText);
            highlightedIndex = Mathf.Clamp(highlightedIndex, 0, Mathf.Max(0, rows.Count - 1));

            if (renamingGroup == null)
                isRenameFocusRequested = false;

            ResizeToFit();
        }

        private void ResizeToFit()
        {
            if (position.width <= 0f || Mathf.Approximately(position.height, FittingHeight))
                return;

            position = new Rect(position.x, position.y, WindowWidth, FittingHeight);
        }


        // ── Клавиатура ───────────────────────────────────

        // Разбираем до полей: иначе Enter и стрелки съест поле поиска.
        private void HandleKeyboard()
        {
            var currentEvent = Event.current;

            if (currentEvent.type != EventType.KeyDown)
                return;

            if (renamingGroup != null)
            {
                HandleRenameKeys(currentEvent);
                return;
            }

            if (creationBar.IsNaming)
            {
                HandleNamingKeys(currentEvent);
                return;
            }

            switch (currentEvent.keyCode)
            {
                case KeyCode.DownArrow:
                    MoveHighlight(1);
                    currentEvent.Use();
                    break;

                case KeyCode.UpArrow:
                    MoveHighlight(-1);
                    currentEvent.Use();
                    break;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    currentEvent.Use();
                    ActivateRow(highlightedIndex);
                    break;

                case KeyCode.F2:
                    currentEvent.Use();

                    if (highlightedIndex < rows.Count)
                        StartRename(rows[highlightedIndex].Group);

                    break;

                case KeyCode.Escape:
                    currentEvent.Use();
                    Close();
                    break;
            }
        }

        private void HandleRenameKeys(Event currentEvent)
        {
            if (currentEvent.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
            {
                currentEvent.Use();
                ApplyRename();
            }
            else if (currentEvent.keyCode == KeyCode.Escape)
            {
                currentEvent.Use();
                CancelRename();
            }
        }

        private void HandleNamingKeys(Event currentEvent)
        {
            if (currentEvent.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
            {
                currentEvent.Use();
                CreateGroup();
            }
            else if (currentEvent.keyCode == KeyCode.Escape)
            {
                currentEvent.Use();
                creationBar.CancelNaming();
                isSearchFocusRequested = true;
            }
        }

        // Подсветка по кругу не ходит: у краёв списка останавливается. Строку держим в видимой части.
        private void MoveHighlight(int step)
        {
            if (rows.Count == 0)
                return;

            highlightedIndex = Mathf.Clamp(highlightedIndex + step, 0, rows.Count - 1);

            var rowTop = highlightedIndex * ObjectGroupRowView.Height;
            var listHeight = ListRect.height;

            if (rowTop < scrollPosition.y)
                scrollPosition.y = rowTop;
            else if (rowTop + ObjectGroupRowView.Height > scrollPosition.y + listHeight)
                scrollPosition.y = rowTop + ObjectGroupRowView.Height - listHeight;
        }


        // ── Шапка ────────────────────────────────────────

        private void DrawHeader()
        {
            var titleRect = new Rect(Padding, 4f, position.width - Padding * 2f, 18f);
            GUI.Label(titleRect, HeaderTitle, EditorStyles.boldLabel);

            if (skippedCount > 0)
            {
                var skippedContent = new GUIContent($"skipped {skippedCount}", "Parts of a prefab and objects of another scene can't be moved");
                GUI.Label(titleRect, skippedContent, new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight });
            }

            var searchRect = new Rect(Padding, 24f, position.width - Padding * 2f, 18f);
            GUI.SetNextControlName(SearchFieldControl);
            var newSearchText = EditorGUI.TextField(searchRect, searchText, EditorStyles.toolbarSearchField);

            if (isSearchFocusRequested)
            {
                EditorGUI.FocusTextInControl(SearchFieldControl);
                isSearchFocusRequested = false;
            }

            if (newSearchText != searchText)
            {
                searchText = newSearchText;
                highlightedIndex = 0;
                Refresh();
            }
        }

        // ── Строки ───────────────────────────────────────

        private void DrawRows()
        {
            var listRect = ListRect;

            if (rows.Count == 0)
            {
                DrawEmptyListNote(listRect);
                return;
            }

            var contentRect = new Rect(0f, 0f, listRect.width - 14f, rows.Count * ObjectGroupRowView.Height);
            scrollPosition = GUI.BeginScrollView(listRect, scrollPosition, contentRect, false, false);

            for (var index = 0; index < rows.Count; index++)
            {
                var rowRect = new Rect(0f, index * ObjectGroupRowView.Height, contentRect.width, ObjectGroupRowView.Height);

                if (rowRect.yMax < scrollPosition.y || rowRect.y > scrollPosition.y + listRect.height)
                    continue;

                DrawRow(rowRect, index);
            }

            GUI.EndScrollView();
        }

        private void DrawEmptyListNote(Rect listRect)
        {
            var note = string.IsNullOrWhiteSpace(searchText) ? "No groups in this scene yet — create one below" : "Nothing found";
            GUI.Label(listRect, note, new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true });
        }

        private void DrawRow(Rect rowRect, int index)
        {
            var row = rows[index];
            var group = row.Group;

            if (group == null)
                return;

            var canMoveInto = !HasObjectsToMove || ObjectGroupMover.CanMoveInto(group, objectsToMove);
            var isAlreadyIn = HasObjectsToMove && ObjectGroupMover.IsAlreadyIn(group, objectsToMove);

            if (Event.current.type == EventType.MouseMove && rowRect.Contains(Event.current.mousePosition))
                highlightedIndex = index;

            var click = ObjectGroupRowView.Draw(rowRect, row, index == highlightedIndex, canMoveInto, isAlreadyIn);

            if (group == renamingGroup)
                DrawRenameField(ObjectGroupRowView.NameRect(rowRect, row));

            if (IsLastRecentRow(index))
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f), SeparatorColor);

            if (click == ObjectGroupRowClick.Name)
                ActivateRow(index);
            else if (click == ObjectGroupRowClick.Icon)
                ShowColorMenu(group);
            else if (click == ObjectGroupRowClick.ContextMenu)
                ShowRowMenu(group);
        }

        private bool IsLastRecentRow(int index) =>
            rows[index].IsRecent && (index + 1 >= rows.Count || !rows[index + 1].IsRecent);

        // Есть что переносить — переносим и закрываемся; нечего — выделяем саму группу.
        private void ActivateRow(int index)
        {
            if (index < 0 || index >= rows.Count)
                return;

            var group = rows[index].Group;

            if (HasObjectsToMove)
            {
                if (!ObjectGroupMover.CanMoveInto(group, objectsToMove))
                    return;

                ObjectGroupMover.MoveInto(group, objectsToMove);
            }
            else
            {
                SelectGroup(group);
            }

            Close();
        }


        // ── Меню строки ──────────────────────────────────

        private void ShowRowMenu(ObjectGroup group)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Show in Hierarchy"), false, () => ObjectGroupRowView.ShowInHierarchy(group));
            menu.AddItem(new GUIContent("Select Group"), false, () => SelectGroup(group));
            menu.AddItem(new GUIContent("Select Contents"), false, () => SelectContents(group));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Rename  F2"), false, () => StartRename(group));

            if (ObjectGroupMover.CanUngroup(group))
                menu.AddItem(new GUIContent("Ungroup"), false, () => Ungroup(group));
            else
                menu.AddDisabledItem(new GUIContent("Ungroup — part of a prefab"));

            menu.AddSeparator(string.Empty);
            AddColorItems(menu, "Color/", group);

            isRowMenuOpen = true;
            menu.ShowAsContext();
        }

        // Тот же выбор цвета, что в «Color/» меню строки, — по клику на иконку.
        private void ShowColorMenu(ObjectGroup group)
        {
            var menu = new GenericMenu();
            AddColorItems(menu, string.Empty, group);

            isRowMenuOpen = true;
            menu.ShowAsContext();
        }

        private void AddColorItems(GenericMenu menu, string folder, ObjectGroup group)
        {
            var currentColor = ObjectGroupColors.ColorOf(group);

            foreach (var color in ObjectGroupColors.All)
            {
                menu.AddItem(new GUIContent(folder + color), color == currentColor, () =>
                {
                    ObjectGroupColors.SetColor(group, color);
                    FinishRowMenuAction();
                });
            }
        }

        private static void SelectGroup(ObjectGroup group)
        {
            Selection.activeGameObject = group.gameObject;
            EditorGUIUtility.PingObject(group.gameObject);
        }

        // Прямые дети группы — то, что в неё положили.
        private void SelectContents(ObjectGroup group)
        {
            Selection.objects = group.transform.Cast<Transform>().Select(child => (Object)child.gameObject).ToArray();
            Close();
        }

        private void Ungroup(ObjectGroup group)
        {
            ObjectGroupMover.Ungroup(group);
            FinishRowMenuAction();
        }

        private void FinishRowMenuAction()
        {
            isRowMenuOpen = false;
            Focus();
            Refresh();
            Repaint();
        }


        // ── Переименование ───────────────────────────────

        private void StartRename(ObjectGroup group)
        {
            renamingGroup = group;
            renameText = group.name;
            isRenameFocusRequested = true;
            FinishRowMenuAction();
        }

        private void DrawRenameField(Rect nameRect)
        {
            GUI.SetNextControlName(RenameFieldControl);
            renameText = EditorGUI.TextField(nameRect, renameText);

            if (isRenameFocusRequested && Event.current.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl(RenameFieldControl);
                isRenameFocusRequested = false;
            }
        }

        private void ApplyRename()
        {
            var newName = renameText.Trim();

            if (renamingGroup != null && newName.Length > 0 && newName != renamingGroup.name)
            {
                Undo.RecordObject(renamingGroup.gameObject, RenameUndoName);
                renamingGroup.gameObject.name = newName;
            }

            CancelRename();
        }

        private void CancelRename()
        {
            renamingGroup = null;
            isSearchFocusRequested = true;
            Refresh();
        }


        // ── Новая группа ─────────────────────────────────

        private void DrawFooter()
        {
            var footerRect = new Rect(0f, position.height - ObjectGroupCreationBar.Height, position.width, ObjectGroupCreationBar.Height);
            EditorGUI.DrawRect(new Rect(footerRect.x, footerRect.y, footerRect.width, 1f), SeparatorColor);

            if (creationBar.Draw(footerRect, HasObjectsToMove))
                CreateGroup();
        }

        // Группа с выделенным внутри — и окно закрывается: дело сделано. Без выделения — пустая, окно остаётся.
        private void CreateGroup()
        {
            if (!creationBar.TryConfirm(out var placement, out var groupName))
                return;

            var group = ObjectGroupMover.CreateGroupWith(groupName, objectsToMove, scene, placement);
            EditorGUIUtility.PingObject(group.gameObject);

            if (HasObjectsToMove)
            {
                Close();
                return;
            }

            isSearchFocusRequested = true;
            Refresh();
        }


        // ── Unity ────────────────────────────────────────

        private void OnEnable()
        {
            wantsMouseMove = true;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnDestroy()
        {
            PopupFocusRestorer.RestoreAfterClose(windowFocusedBeforeOpen, lastMouseScreenPosition);
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.MouseDown)
                isRowMenuOpen = false;

            if (Event.current.isMouse)
                lastMouseScreenPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);

            HandleKeyboard();
            DrawHeader();
            DrawRows();
            DrawFooter();

            if (Event.current.type == EventType.MouseMove)
                Repaint();
        }

        private void OnFocus()
        {
            isRowMenuOpen = false;
        }

        // Клик мимо — закрыться, как у выпадающего меню. Кроме времени, пока открыто меню строки.
        private void OnLostFocus()
        {
            if (!isRowMenuOpen && this != null)
                Close();
        }

        private void OnUndoRedo()
        {
            Refresh();
            Repaint();
        }
    }
}
