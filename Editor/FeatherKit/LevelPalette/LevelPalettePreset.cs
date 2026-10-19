using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.LevelPalette
{
    /// <summary>
    /// Пресет палитры — свой набор вкладок: в одном материалы по категориям, в другом все текстуры одной вкладкой.
    /// - держит вкладки, их порядок и какая открыта
    /// Правки идут через <see cref="LevelPaletteStorage"/> — там отмена и сохранение.
    /// </summary>
    [Serializable]
    public class LevelPalettePreset
    {
        [SerializeField] private string name;
        [SerializeField] private List<LevelPaletteTab> tabs = new List<LevelPaletteTab>();
        [SerializeField] private int activeTabIndex;


        public LevelPalettePreset(string name, IEnumerable<LevelPaletteTab> tabs, int activeTabIndex = 0)
        {
            this.name = name;
            this.tabs.AddRange(tabs);
            this.activeTabIndex = activeTabIndex;
        }


        public string Name
        {
            get => name;
            set => name = value;
        }

        public IReadOnlyList<LevelPaletteTab> Tabs => tabs;

        /// <summary>Открытая вкладка; вкладок нет — null.</summary>
        public LevelPaletteTab ActiveTab => tabs.Count == 0 ? null : tabs[Mathf.Clamp(activeTabIndex, 0, tabs.Count - 1)];


        // ── Вкладки ──────────────────────────────────────────────────────

        public void AddTab(LevelPaletteTab tab)
        {
            tabs.Add(tab);
            activeTabIndex = tabs.Count - 1;
        }


        // Удалили открытую — откроется соседняя, что встала на её место.
        public void RemoveTab(LevelPaletteTab tab)
        {
            var active = ActiveTab;

            tabs.Remove(tab);

            activeTabIndex = tabs.Contains(active)
                ? tabs.IndexOf(active)
                : Mathf.Clamp(activeTabIndex, 0, Mathf.Max(0, tabs.Count - 1));
        }


        /// <summary>false — вкладка и так открыта или не из этого пресета.</summary>
        public bool SelectTab(LevelPaletteTab tab)
        {
            var index = tabs.IndexOf(tab);

            if (index < 0 || index == activeTabIndex)
                return false;

            activeTabIndex = index;
            return true;
        }


        /// <summary>Сдвинуть вкладку влево (-1) или вправо (+1). false — сдвигать некуда.</summary>
        public bool MoveTab(LevelPaletteTab tab, int step)
        {
            var from = tabs.IndexOf(tab);
            var to = from + step;

            if (from < 0 || to < 0 || to >= tabs.Count)
                return false;

            var active = ActiveTab;

            tabs.RemoveAt(from);
            tabs.Insert(to, tab);
            activeTabIndex = tabs.IndexOf(active);

            return true;
        }


        // ── Копия ────────────────────────────────────────────────────────

        /// <summary>Независимая копия: правки вкладок в ней не трогают оригинал.</summary>
        public LevelPalettePreset Duplicate(string copyName)
        {
            var copiedTabs = new List<LevelPaletteTab>();

            foreach (var tab in tabs)
            {
                var copy = new LevelPaletteTab(tab.Name, tab.AssetKind);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(tab), copy);
                copiedTabs.Add(copy);
            }

            return new LevelPalettePreset(copyName, copiedTabs, activeTabIndex);
        }
    }
}
