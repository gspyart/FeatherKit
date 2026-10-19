using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LevelPalette
{
    /// <summary>
    /// Ряды вкладок над палитрой — часть окна <see cref="LevelPaletteWindow"/>.
    /// - в начале первой строки — выбор пресета, <see cref="LevelPalettePresetSelector"/>
    /// - не влезающие вкладки переносит на новую строку
    /// - порядок меняется перетаскиванием, по правой кнопке — переименовать, настроить, удалить
    /// - кнопки «новая вкладка» и «настройки вкладки»
    /// </summary>
    [Serializable]
    public class LevelPaletteTabsBar
    {
        private const float AddTabButtonWidth = 24f;
        private const float SettingsButtonWidth = 28f;
        private const float DragStartDistance = 6f;
        private static readonly Vector2 TabIconSize = new Vector2(14f, 14f);

        [SerializeField] private bool isSettingsShown;

        private LevelPaletteTab pressedTab;
        private Vector2 pressPosition;
        private bool isDragging;
        private int dragUndoGroup;


        /// <summary>Пользователь сам переключил вкладку или пресет.</summary>
        public event Action TabSelected;


        public bool IsSettingsShown => isSettingsShown;

        private static LevelPaletteStorage Storage => LevelPaletteStorage.instance;


        // ── Отрисовка ────────────────────────────────────────────────────

        public void Draw(float windowWidth)
        {
            if (Event.current.rawType == EventType.MouseUp)
                FinishDrag();

            var activeTab = Storage.ActiveTab;
            var rowWidth = windowWidth - EditorStyles.toolbar.padding.horizontal;
            // Размер значка задаём и на замер ширины, и на отрисовку — иначе ряды разойдутся с кнопками.
            using var iconSize = new EditorGUIUtility.IconSizeScope(TabIconSize);
            var firstRowWidth = rowWidth - LevelPalettePresetSelector.Width - AddTabButtonWidth - SettingsButtonWidth;
            var rows = SplitIntoRows(Storage.Tabs.ToList(), firstRowWidth, rowWidth);

            for (var row = 0; row < rows.Count; row++)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if (row == 0)
                        LevelPalettePresetSelector.Draw(NotifyTabSelected);

                    foreach (var tab in rows[row])
                        DrawTabButton(tab, tab == activeTab);

                    GUILayout.FlexibleSpace();

                    if (row == 0)
                        DrawButtons();
                }
            }
        }

        private static List<List<LevelPaletteTab>> SplitIntoRows(List<LevelPaletteTab> tabs, float firstRowWidth, float rowWidth)
        {
            var rows = new List<List<LevelPaletteTab>> { new List<LevelPaletteTab>() };
            var freeWidth = firstRowWidth;

            foreach (var tab in tabs)
            {
                var tabWidth = EditorStyles.toolbarButton.CalcSize(TabLabelOf(tab)).x;

                if (tabWidth > freeWidth && rows[rows.Count - 1].Count > 0)
                {
                    rows.Add(new List<LevelPaletteTab>());
                    freeWidth = rowWidth;
                }

                rows[rows.Count - 1].Add(tab);
                freeWidth -= tabWidth;
            }

            return rows;
        }

        private void DrawTabButton(LevelPaletteTab tab, bool isActive)
        {
            var label = TabLabelOf(tab);
            var rect = GUILayoutUtility.GetRect(label, EditorStyles.toolbarButton);
            var current = Event.current;

            // Нажатие, а не ContextClick: кнопка вкладки съедает правый клик, и ContextClick не приходит.
            if (current.type == EventType.MouseDown && current.button == 1 && rect.Contains(current.mousePosition))
            {
                ShowTabMenu(tab, rect);
                current.Use();
            }

            HandleDrag(tab, rect);

            if (GUI.Toggle(rect, isActive, label, EditorStyles.toolbarButton) && !isActive)
                SelectTab(tab);
        }

        private void DrawButtons()
        {
            if (GUILayout.Button(new GUIContent("+", "Новая вкладка"), EditorStyles.toolbarButton, GUILayout.Width(AddTabButtonWidth)))
                ShowAddTabMenu();

            isSettingsShown = GUILayout.Toggle(isSettingsShown, new GUIContent(EditorGUIUtility.IconContent("_Popup").image,
                "Настройки вкладки: папки, вид ассетов, имя"), EditorStyles.toolbarButton, GUILayout.Width(SettingsButtonWidth));
        }


        // ── Перетаскивание ───────────────────────────────────────────────

        private void HandleDrag(LevelPaletteTab tab, Rect rect)
        {
            var current = Event.current;

            if (current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
            {
                pressedTab = tab;
                pressPosition = current.mousePosition;
                return;
            }

            if (current.type != EventType.MouseDrag || pressedTab == null)
                return;

            if (!isDragging && tab == pressedTab && Vector2.Distance(pressPosition, current.mousePosition) > DragStartDistance)
                StartDrag();

            if (isDragging && tab != pressedTab && IsDraggedPastMiddle(tab, rect))
                MoveDraggedTabTo(tab);
        }

        // Все сдвиги за одно перетаскивание отменяются одним Undo.
        private void StartDrag()
        {
            isDragging = true;
            Undo.IncrementCurrentGroup();
            dragUndoGroup = Undo.GetCurrentGroup();
            SelectTab(pressedTab);
        }

        // Середина, а не край: иначе широкая и узкая вкладки менялись бы местами туда-обратно.
        private bool IsDraggedPastMiddle(LevelPaletteTab tab, Rect rect)
        {
            var mouse = Event.current.mousePosition;

            if (!rect.Contains(mouse))
                return false;

            var isTargetAfter = PositionOf(tab) > PositionOf(pressedTab);

            return isTargetAfter ? mouse.x > rect.center.x : mouse.x < rect.center.x;
        }

        // Порядок поменялся посреди кадра — обрываем его, как и панель настроек.
        private void MoveDraggedTabTo(LevelPaletteTab tab)
        {
            Storage.MoveTab(pressedTab, PositionOf(tab) - PositionOf(pressedTab));
            Event.current.Use();
            GUIUtility.ExitGUI();
        }

        private void FinishDrag()
        {
            if (isDragging)
                Undo.CollapseUndoOperations(dragUndoGroup);

            pressedTab = null;
            isDragging = false;
        }


        // ── Меню ─────────────────────────────────────────────────────────

        private void ShowAddTabMenu()
        {
            var menu = new GenericMenu();

            foreach (PaletteAssetKind kind in Enum.GetValues(typeof(PaletteAssetKind)))
            {
                var name = PaletteAssetKindRules.DisplayNames[(int)kind];

                menu.AddItem(new GUIContent(name), false, () =>
                {
                    Storage.AddTab(name, kind);
                    isSettingsShown = true;
                });
            }

            menu.ShowAsContext();
        }


        private void ShowTabMenu(LevelPaletteTab tab, Rect tabRect)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Переименовать"), false, () => PopupWindow.Show(tabRect, new PaletteNameEditPopup(tab.Name,
                name => Storage.EditTab(tab, "Переименовать вкладку палитры", changed => changed.Name = name))));
            menu.AddItem(new GUIContent("Настроить"), false, () =>
            {
                SelectTab(tab);
                isSettingsShown = true;
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Удалить"), false, () => RemoveTabAfterConfirm(tab));

            menu.ShowAsContext();
        }

        private static void RemoveTabAfterConfirm(LevelPaletteTab tab)
        {
            if (EditorUtility.DisplayDialog("Палитра", $"Удалить вкладку «{tab.Name}»? Ассеты в проекте не пострадают.",
                    "Удалить", "Отмена"))
                Storage.RemoveTab(tab);
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private void SelectTab(LevelPaletteTab tab)
        {
            Storage.SelectTab(tab);
            NotifyTabSelected();
        }

        private void NotifyTabSelected()
        {
            TabSelected?.Invoke();
        }

        private static GUIContent TabLabelOf(LevelPaletteTab tab)
        {
            return new GUIContent(tab.Name, tab.AssetKind.ToIcon(), PaletteAssetKindRules.DisplayNames[(int)tab.AssetKind]);
        }

        private static int PositionOf(LevelPaletteTab tab)
        {
            return Storage.Tabs.ToList().IndexOf(tab);
        }
    }
}
