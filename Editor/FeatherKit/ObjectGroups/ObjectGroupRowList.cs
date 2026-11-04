using System;
using System.Collections.Generic;
using System.Linq;
using FeatherKit.LevelDesign;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools
{
    /// <summary>Одна строка окна групп: какая группа, на какой глубине и с какой подписью.</summary>
    public readonly struct ObjectGroupRow
    {
        public readonly ObjectGroup Group;
        public readonly int Depth;
        public readonly string Label;
        public readonly bool HasChildGroups;
        public readonly bool IsRecent;

        public ObjectGroupRow(ObjectGroup group, int depth, string label, bool hasChildGroups, bool isRecent)
        {
            Group = group;
            Depth = depth;
            Label = label;
            HasChildGroups = hasChildGroups;
            IsRecent = isRecent;
        }
    }


    /// <summary>
    /// Какие строки показывает окно групп. Без поиска — сверху последние группы с путём («Buildings › Tower A»),
    /// ниже дерево всех групп сцены, свёрнутые ветки скрыты. С поиском — плоский список подходящих, тоже с путём.
    /// Какие ветки свёрнуты, помнит до перезагрузки скриптов.
    /// </summary>
    public static class ObjectGroupRowList
    {
        private const string PathSeparator = " › ";

        private static readonly HashSet<ObjectGroup> CollapsedGroups = new();


        // ── Свёрнутые ветки ──────────────────────────────

        public static bool IsCollapsed(ObjectGroup group) => CollapsedGroups.Contains(group);


        public static void ToggleCollapsed(ObjectGroup group)
        {
            if (!CollapsedGroups.Remove(group))
                CollapsedGroups.Add(group);
        }


        // ── Сборка ───────────────────────────────────────

        // Все группы сцены сверху вниз, как в Hierarchy: родительская группа всегда раньше своих.
        public static List<ObjectGroup> GroupsIn(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return new List<ObjectGroup>();

            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ObjectGroup>(true))
                .ToList();
        }


        public static List<ObjectGroupRow> Build(IReadOnlyList<ObjectGroup> groups, IReadOnlyList<ObjectGroup> recentGroups, string searchText)
        {
            var parentGroups = groups.Select(ParentGroupOf).Where(parent => parent != null).ToHashSet();

            return string.IsNullOrWhiteSpace(searchText)
                ? BuildTree(groups, recentGroups, parentGroups)
                : BuildSearchResults(groups, searchText.Trim(), parentGroups);
        }

        private static List<ObjectGroupRow> BuildTree(IReadOnlyList<ObjectGroup> groups, IReadOnlyList<ObjectGroup> recentGroups,
            HashSet<ObjectGroup> parentGroups)
        {
            var rows = recentGroups
                .Select(group => new ObjectGroupRow(group, 0, PathOf(group), false, true))
                .ToList();

            foreach (var group in groups)
            {
                if (HasCollapsedAncestor(group))
                    continue;

                rows.Add(new ObjectGroupRow(group, DepthOf(group), group.name, parentGroups.Contains(group), false));
            }

            return rows;
        }

        private static bool HasCollapsedAncestor(ObjectGroup group)
        {
            for (var parent = ParentGroupOf(group); parent != null; parent = ParentGroupOf(parent))
            {
                if (CollapsedGroups.Contains(parent))
                    return true;
            }

            return false;
        }

        private static int DepthOf(ObjectGroup group)
        {
            var depth = 0;

            for (var parent = ParentGroupOf(group); parent != null; parent = ParentGroupOf(parent))
                depth++;

            return depth;
        }

        // Ищем по имени без учёта регистра: «tow» находит «Tower A».
        private static List<ObjectGroupRow> BuildSearchResults(IReadOnlyList<ObjectGroup> groups, string searchText,
            HashSet<ObjectGroup> parentGroups)
        {
            return groups
                .Where(group => group.name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(group => new ObjectGroupRow(group, 0, PathOf(group), parentGroups.Contains(group), false))
                .ToList();
        }


        // ── Общие помощники ──────────────────────────────

        // Имена родительских групп и самой группы: «Buildings › Tower A».
        private static string PathOf(ObjectGroup group)
        {
            var names = new List<string>();

            for (var current = group; current != null; current = ParentGroupOf(current))
                names.Insert(0, current.name);

            return string.Join(PathSeparator, names);
        }

        private static ObjectGroup ParentGroupOf(ObjectGroup group)
        {
            var parent = group.transform.parent;
            return parent != null ? parent.GetComponentInParent<ObjectGroup>(true) : null;
        }
    }
}
