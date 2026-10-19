using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace FeatherKit.EditorTools.LevelPalette
{
    /// <summary>
    /// Хранилище палитры уровня: общее, в ProjectSettings/FeatherKit проекта — попадает в git.
    /// - пресеты — наборы вкладок, и какой из них открыт
    /// - вид сетки: размер клеток и подписи
    /// Правки пресетов и вкладок — с отменой через Undo и сразу на диск; вид сетки — без отмены, это не работа, а удобство.
    /// </summary>
    [FilePath("ProjectSettings/FeatherKit/LevelPalette.asset", FilePathAttribute.Location.ProjectFolder)]
    public class LevelPaletteStorage : ScriptableSingleton<LevelPaletteStorage>
    {
        public const float MinCellSize = 40f;
        public const float MaxCellSize = 200f;

        private const string FirstPresetName = "Основной";
        private const string DefaultTabName = "Материалы";
        private const string DefaultTabFolder = "Assets/Materials";

        [SerializeField] private List<LevelPalettePreset> presets = new List<LevelPalettePreset>();
        [SerializeField] private int activePresetIndex;
        [SerializeField] private float cellSize = 72f;
        [SerializeField] private bool showNames = true;

        // Вкладки из времени до пресетов: при первом чтении переезжают в пресет «Основной».
        [SerializeField, FormerlySerializedAs("tabs")] private List<LevelPaletteTab> tabsBeforePresets = new List<LevelPaletteTab>();
        [SerializeField, FormerlySerializedAs("activeTabIndex")] private int activeTabIndexBeforePresets;


        /// <summary>Что-то поменялось — окну пора пересобрать список и перерисоваться.</summary>
        public event Action Changed;


        public IReadOnlyList<LevelPalettePreset> Presets
        {
            get
            {
                if (presets.Count == 0)
                    presets.Add(CreateFirstPreset());

                return presets;
            }
        }

        public LevelPalettePreset ActivePreset => Presets[Mathf.Clamp(activePresetIndex, 0, presets.Count - 1)];

        public IReadOnlyList<LevelPaletteTab> Tabs => ActivePreset.Tabs;

        /// <summary>Открытая вкладка пресета. Вкладок нет — заводится «Материалы» с папкой материалов проекта.</summary>
        public LevelPaletteTab ActiveTab
        {
            get
            {
                var preset = ActivePreset;

                if (preset.ActiveTab == null)
                    preset.AddTab(CreateDefaultTab());

                return preset.ActiveTab;
            }
        }

        public float CellSize
        {
            get => Mathf.Clamp(cellSize, MinCellSize, MaxCellSize);
            set
            {
                cellSize = Mathf.Clamp(value, MinCellSize, MaxCellSize);
                Save(true);
            }
        }

        public bool ShowNames
        {
            get => showNames;
            set
            {
                showNames = value;
                Save(true);
            }
        }


        // ── Пресеты ──────────────────────────────────────────────────────

        /// <summary>Добавить пресет и сразу открыть его.</summary>
        public void AddPreset(LevelPalettePreset preset)
        {
            RecordUndo("Новый пресет палитры");

            presets.Add(preset);
            activePresetIndex = presets.Count - 1;

            SaveAndNotify();
        }


        /// <summary>Последний пресет не удаляется: палитре нужен хоть один.</summary>
        public void RemovePreset(LevelPalettePreset preset)
        {
            if (Presets.Count <= 1)
                return;

            var active = ActivePreset;

            RecordUndo("Удалить пресет палитры");
            presets.Remove(preset);

            activePresetIndex = presets.Contains(active)
                ? presets.IndexOf(active)
                : Mathf.Clamp(activePresetIndex, 0, presets.Count - 1);

            SaveAndNotify();
        }


        public void SelectPreset(LevelPalettePreset preset)
        {
            var index = presets.IndexOf(preset);

            if (index < 0 || index == activePresetIndex)
                return;

            activePresetIndex = index;
            SaveAndNotify();
        }


        public void RenamePreset(LevelPalettePreset preset, string name)
        {
            RecordUndo("Переименовать пресет палитры");
            preset.Name = name;
            SaveAndNotify();
        }


        // ── Вкладки открытого пресета ────────────────────────────────────

        public LevelPaletteTab AddTab(string name, PaletteAssetKind assetKind)
        {
            RecordUndo("Новая вкладка палитры");

            var tab = new LevelPaletteTab(name, assetKind);
            ActivePreset.AddTab(tab);

            SaveAndNotify();

            return tab;
        }


        public void RemoveTab(LevelPaletteTab tab)
        {
            RecordUndo("Удалить вкладку палитры");
            ActivePreset.RemoveTab(tab);
            SaveAndNotify();
        }


        public void SelectTab(LevelPaletteTab tab)
        {
            if (ActivePreset.SelectTab(tab))
                SaveAndNotify();
        }


        /// <summary>Сдвинуть вкладку влево (-1) или вправо (+1).</summary>
        public void MoveTab(LevelPaletteTab tab, int step)
        {
            RecordUndo("Сдвинуть вкладку палитры");

            if (ActivePreset.MoveTab(tab, step))
                SaveAndNotify();
        }


        /// <summary>Любая правка настроек вкладки: имя, вид ассетов, подпапки.</summary>
        public void EditTab(LevelPaletteTab tab, string undoName, Action<LevelPaletteTab> change)
        {
            RecordUndo(undoName);
            change(tab);
            SaveAndNotify();
        }


        // ── Источники и ручные ассеты ────────────────────────────────────

        public void AddSourceFolder(LevelPaletteTab tab, string folderGuid)
        {
            if (tab.SourceFolderGuids.Contains(folderGuid))
                return;

            EditTab(tab, "Добавить папку в палитру", changed => changed.SourceFolderGuids.Add(folderGuid));
        }


        public void RemoveSourceFolder(LevelPaletteTab tab, string folderGuid)
        {
            EditTab(tab, "Убрать папку из палитры", changed => changed.SourceFolderGuids.Remove(folderGuid));
        }


        /// <summary>Добавить ассеты во вкладку вручную. Возвращает, сколько добавлено: уже лежащие не повторяются.</summary>
        public int AddManualAssets(LevelPaletteTab tab, IEnumerable<string> assetGuids)
        {
            RecordUndo("Добавить в палитру");

            var added = 0;

            foreach (var guid in assetGuids)
            {
                if (tab.ManualAssetGuids.Contains(guid))
                    continue;

                tab.ManualAssetGuids.Add(guid);
                added++;
            }

            SaveAndNotify();

            return added;
        }


        public void RemoveManualAsset(LevelPaletteTab tab, string assetGuid)
        {
            EditTab(tab, "Убрать из палитры", changed => changed.ManualAssetGuids.Remove(assetGuid));
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private void RecordUndo(string undoName)
        {
            Undo.RecordObject(this, undoName);
        }

        private void SaveAndNotify()
        {
            Save(true);
            Changed?.Invoke();
        }

        private LevelPalettePreset CreateFirstPreset()
        {
            var preset = new LevelPalettePreset(FirstPresetName, tabsBeforePresets, activeTabIndexBeforePresets);
            tabsBeforePresets.Clear();

            return preset;
        }

        private static LevelPaletteTab CreateDefaultTab()
        {
            var tab = new LevelPaletteTab(DefaultTabName, PaletteAssetKind.Materials);
            var folderGuid = AssetDatabase.AssetPathToGUID(DefaultTabFolder);

            if (!string.IsNullOrEmpty(folderGuid))
                tab.SourceFolderGuids.Add(folderGuid);

            return tab;
        }


        // ── Отмена ───────────────────────────────────────────────────────

        // Отмена вернула данные в памяти — на диск и в окно их надо отдать заново.
        private void OnUndoRedo()
        {
            SaveAndNotify();
        }


        // ── Unity-события ────────────────────────────────────────────────

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
