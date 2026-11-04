using System;
using FeatherKit.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>Что сделали со строкой окна групп мышью — дальше решает окно.</summary>
    public enum ObjectGroupRowClick
    {
        None,
        Name,
        Icon,
        ContextMenu
    }


    /// <summary>
    /// Рисует одну строку окна групп: стрелка ветки, галка вкл/выкл, иконка, имя, справа — «показать в Hierarchy»,
    /// глаз, рука, Static и Default Parent.
    /// Переключатели срабатывают сразу, через <see cref="ObjectGroupSwitches"/>; клик по имени, по иконке и ПКМ отдаёт окну.
    /// Выключенный переключатель — бледный, включённый — яркий: состояние видно, не наводя мышь.
    /// </summary>
    public static class ObjectGroupRowView
    {
        public const float Height = 22f;

        private const float IndentWidth = 14f;
        private const float FoldoutWidth = 14f;
        private const float ToggleWidth = 18f;
        private const float IconSize = 16f;
        private const float SwitchWidth = 22f;
        private const float SidePadding = 6f;
        private const int RightButtonCount = 5;
        private const float OffSwitchAlpha = 0.3f;
        private const float ActionButtonAlpha = 0.6f;
        private const float DimmedNameAlpha = 0.45f;

        private static readonly Color HoverColor = new(1f, 1f, 1f, 0.06f);
        private static readonly Color HighlightColor = new(0.24f, 0.49f, 0.9f, 0.45f);
        private static readonly Color RecentColor = new(1f, 0.75f, 0.2f, 0.07f);
        private static readonly Color StaticOnColor = new(0.35f, 0.75f, 1f, 1f);

        private static GUIStyle staticLetterStyle;
        private static GUIStyle nameStyle;
        private static GUIStyle boldNameStyle;


        private static GUIStyle StaticLetterStyle => staticLetterStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };

        private static GUIStyle NameStyle => nameStyle ??= new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft
        };

        private static GUIStyle BoldNameStyle => boldNameStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleLeft
        };


        // ── Строка ───────────────────────────────────────

        // canMoveInto — можно ли положить выделенное сюда; isAlreadyIn — выделенное уже здесь, имя жирное.
        public static ObjectGroupRowClick Draw(Rect rect, ObjectGroupRow row, bool isHighlighted, bool canMoveInto, bool isAlreadyIn)
        {
            DrawBackground(rect, row, isHighlighted);

            var group = row.Group;
            var switchesLeft = DrawSwitches(rect, group);
            var left = rect.x + SidePadding + row.Depth * IndentWidth;

            DrawFoldout(new Rect(left, rect.y, FoldoutWidth, rect.height), row);
            left += FoldoutWidth;

            DrawActiveToggle(new Rect(left, rect.y + 2f, ToggleWidth, rect.height - 4f), group);
            left += ToggleWidth;

            var iconRect = new Rect(left, rect.y + (rect.height - IconSize) / 2f, IconSize, IconSize);
            GUI.DrawTexture(iconRect, ObjectGroupColors.IconFor(group));
            left += IconSize + 4f;

            var nameRect = new Rect(left, rect.y, Mathf.Max(0f, switchesLeft - left), rect.height);
            DrawName(nameRect, row, canMoveInto && group.gameObject.activeInHierarchy, isAlreadyIn);

            return ClickOn(rect, iconRect, nameRect);
        }

        // Где стоит имя — туда окно кладёт поле переименования.
        public static Rect NameRect(Rect rect, ObjectGroupRow row)
        {
            var left = rect.x + SidePadding + row.Depth * IndentWidth + FoldoutWidth + ToggleWidth + IconSize + 4f;
            var right = rect.xMax - SidePadding - SwitchWidth * RightButtonCount;
            return new Rect(left, rect.y + 2f, Mathf.Max(0f, right - left), rect.height - 4f);
        }

        private static void DrawBackground(Rect rect, ObjectGroupRow row, bool isHighlighted)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (row.IsRecent)
                EditorGUI.DrawRect(rect, RecentColor);

            if (isHighlighted)
                EditorGUI.DrawRect(rect, HighlightColor);
            else if (rect.Contains(Event.current.mousePosition))
                EditorGUI.DrawRect(rect, HoverColor);
        }

        // Стрелка только у веток дерева; в последних и в поиске дерево не раскрывают.
        private static void DrawFoldout(Rect rect, ObjectGroupRow row)
        {
            if (!row.HasChildGroups || row.IsRecent)
                return;

            var isExpanded = !ObjectGroupRowList.IsCollapsed(row.Group);

            if (EditorGUI.Foldout(rect, isExpanded, GUIContent.none, true) != isExpanded)
                ObjectGroupRowList.ToggleCollapsed(row.Group);
        }

        private static void DrawActiveToggle(Rect rect, ObjectGroup group)
        {
            var isActive = ObjectGroupSwitches.IsActive(group);
            var content = new GUIContent(string.Empty, "On / off: the group and everything inside, in the game too");

            if (EditorGUI.Toggle(rect, content, isActive) != isActive)
                ObjectGroupSwitches.SetActive(group, !isActive);
        }

        private static void DrawName(Rect rect, ObjectGroupRow row, bool isEnabled, bool isAlreadyIn)
        {
            var style = isAlreadyIn ? BoldNameStyle : NameStyle;
            var tooltip = isAlreadyIn ? "The selection is already in this group" : string.Empty;

            using (new ColorAlphaScope(isEnabled ? 1f : DimmedNameAlpha))
                GUI.Label(rect, new GUIContent(row.Label, tooltip), style);
        }

        // Клик по иконке — выбор цвета, по имени — перенос, ПКМ по строке — её меню.
        private static ObjectGroupRowClick ClickOn(Rect rowRect, Rect iconRect, Rect nameRect)
        {
            var currentEvent = Event.current;

            if (currentEvent.type != EventType.MouseDown)
                return ObjectGroupRowClick.None;

            if (currentEvent.button == 1 && rowRect.Contains(currentEvent.mousePosition))
            {
                currentEvent.Use();
                return ObjectGroupRowClick.ContextMenu;
            }

            if (currentEvent.button == 0 && iconRect.Contains(currentEvent.mousePosition))
            {
                currentEvent.Use();
                return ObjectGroupRowClick.Icon;
            }

            if (currentEvent.button == 0 && nameRect.Contains(currentEvent.mousePosition))
            {
                currentEvent.Use();
                return ObjectGroupRowClick.Name;
            }

            return ObjectGroupRowClick.None;
        }


        // ── Переключатели справа ─────────────────────────

        // Справа налево: Default Parent, Static, рука, глаз, «показать в Hierarchy». Отдаёт левый край кнопок — до него тянется имя.
        private static float DrawSwitches(Rect rect, ObjectGroup group)
        {
            var x = rect.xMax - SidePadding - SwitchWidth;

            var isDefaultParent = ObjectGroupSwitches.IsDefaultParent(group);
            if (DrawIconSwitch(SwitchRect(rect, ref x), "Favorite", isDefaultParent,
                    "Default Parent: new objects of the scene are created inside this group"))
                ObjectGroupSwitches.SetDefaultParent(group, !isDefaultParent);

            var isStatic = ObjectGroupSwitches.IsStatic(group);
            if (DrawStaticSwitch(SwitchRect(rect, ref x), isStatic))
                ObjectGroupSwitches.SetStatic(group, !isStatic);

            var isPickingDisabled = ObjectGroupSwitches.IsPickingDisabled(group);
            if (DrawIconSwitch(SwitchRect(rect, ref x), isPickingDisabled ? "scenepicking_notpickable" : "scenepicking_pickable",
                    isPickingDisabled, "Picking: off — clicks in the Scene view go through the group. Editor only"))
                ObjectGroupSwitches.SetPickingDisabled(group, !isPickingDisabled);

            var isHidden = ObjectGroupSwitches.IsHiddenInScene(group);
            if (DrawIconSwitch(SwitchRect(rect, ref x), isHidden ? "scenevis_hidden" : "scenevis_visible",
                    isHidden, "Visibility: hidden in the Scene view only, the game still shows it. Editor only"))
                ObjectGroupSwitches.SetHiddenInScene(group, !isHidden);

            if (DrawActionButton(SwitchRect(rect, ref x), "UnityEditor.SceneHierarchyWindow", "Show in Hierarchy: unfolds, scrolls to and flashes the group"))
                ShowInHierarchy(group);

            return x + SwitchWidth;
        }


        // Hierarchy раскрывает родителей, прокручивает к группе и подсвечивает её.
        public static void ShowInHierarchy(ObjectGroup group) => EditorGUIUtility.PingObject(group.gameObject);

        private static Rect SwitchRect(Rect rowRect, ref float x)
        {
            var switchRect = new Rect(x, rowRect.y, SwitchWidth, rowRect.height);
            x -= SwitchWidth;
            return switchRect;
        }

        // Иконка-кнопка: «включено» — ярко, «выключено» — бледно. Отдаёт true, если по ней кликнули.
        private static bool DrawIconSwitch(Rect rect, string iconName, bool isOn, string tooltip)
        {
            var icon = EditorGUIUtility.IconContent(iconName).image;
            var iconRect = new Rect(rect.x + (rect.width - IconSize) / 2f, rect.y + (rect.height - IconSize) / 2f, IconSize, IconSize);

            using (new ColorAlphaScope(isOn ? 1f : OffSwitchAlpha))
                GUI.DrawTexture(iconRect, icon);

            return GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
        }

        // Кнопка-действие, а не переключатель: полупрозрачная, под мышью — яркая.
        private static bool DrawActionButton(Rect rect, string iconName, string tooltip)
        {
            var icon = EditorGUIUtility.IconContent(iconName).image;
            var iconRect = new Rect(rect.x + (rect.width - IconSize) / 2f, rect.y + (rect.height - IconSize) / 2f, IconSize, IconSize);
            var isHovered = rect.Contains(Event.current.mousePosition);

            using (new ColorAlphaScope(isHovered ? 1f : ActionButtonAlpha))
                GUI.DrawTexture(iconRect, icon);

            return GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
        }

        // У Static нет штатной иконки — буква «S»: включён — голубая, выключен — бледная.
        private static bool DrawStaticSwitch(Rect rect, bool isOn)
        {
            var previousColor = GUI.contentColor;
            GUI.contentColor = isOn ? StaticOnColor : new Color(1f, 1f, 1f, OffSwitchAlpha);
            GUI.Label(rect, "S", StaticLetterStyle);
            GUI.contentColor = previousColor;

            var tooltip = "Static: the group and everything inside get all Static flags, including objects added later. " +
                          "Switching it off removes the flags";
            return GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
        }


        // ── Общие помощники ──────────────────────────────

        // Прозрачность всего, что рисуется внутри, — потом как было.
        private readonly struct ColorAlphaScope : IDisposable
        {
            private readonly Color previousColor;

            public ColorAlphaScope(float alpha)
            {
                previousColor = GUI.color;
                GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * alpha);
            }

            public void Dispose() => GUI.color = previousColor;
        }
    }
}
