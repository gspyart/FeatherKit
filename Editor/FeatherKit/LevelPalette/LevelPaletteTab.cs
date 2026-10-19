using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LevelPalette
{
    /// <summary>
    /// Вкладка палитры: имя, вид ассетов и откуда они берутся.
    /// - папки-источники: всё подходящее в них появляется само, новое — тоже
    /// - ручные ассеты: перетащены в окно и живут во вкладке, пока их не уберут
    /// Ассеты хранятся по GUID: переименование и перенос файла ссылку не ломают.
    /// Правки идут через <see cref="LevelPaletteStorage"/> — там отмена и сохранение.
    /// </summary>
    [Serializable]
    public class LevelPaletteTab
    {
        [SerializeField] private string name;
        [SerializeField] private PaletteAssetKind assetKind;
        [SerializeField] private bool includeSubfolders = true;
        [SerializeField] private List<string> sourceFolderGuids = new List<string>();
        [SerializeField] private List<string> manualAssetGuids = new List<string>();


        public LevelPaletteTab(string name, PaletteAssetKind assetKind)
        {
            this.name = name;
            this.assetKind = assetKind;
        }


        public string Name
        {
            get => name;
            set => name = value;
        }

        public PaletteAssetKind AssetKind
        {
            get => assetKind;
            set => assetKind = value;
        }

        /// <summary>Брать ассеты и из подпапок источников.</summary>
        public bool IncludeSubfolders
        {
            get => includeSubfolders;
            set => includeSubfolders = value;
        }

        public List<string> SourceFolderGuids => sourceFolderGuids;

        public List<string> ManualAssetGuids => manualAssetGuids;


        // ── Источники ────────────────────────────────────────────────────

        /// <summary>Пути живых папок-источников; удалённые пропускаются.</summary>
        public IEnumerable<string> SourceFolderPaths => SourceFolderGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(AssetDatabase.IsValidFolder);


        /// <summary>
        /// Папка-источник, в которой лежит ассет. Для текстур это пак: от него повторяются подпапки материалов.
        /// Ассет не из папок вкладки — null.
        /// </summary>
        public string FindSourceFolderOf(string assetPath)
        {
            return SourceFolderPaths
                .Where(folder => assetPath.StartsWith(folder + "/", StringComparison.Ordinal))
                .OrderByDescending(folder => folder.Length)
                .FirstOrDefault();
        }


        // ── Содержимое ───────────────────────────────────────────────────

        /// <summary>GUID всех ассетов вкладки по имени файла: сначала из папок, затем ручные, без повторов.</summary>
        public List<string> CollectAssetGuids()
        {
            var guids = new List<string>();
            var seen = new HashSet<string>();

            foreach (var guid in FindInSourceFolders().Concat(ManualAssetGuids))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.IsNullOrEmpty(path) && seen.Add(guid))
                    guids.Add(guid);
            }

            return guids.OrderBy(guid => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)),
                StringComparer.OrdinalIgnoreCase).ToList();
        }

        // FindAssets всегда ищет вглубь — без подпапок отбрасываем всё, что лежит не прямо в источнике.
        private IEnumerable<string> FindInSourceFolders()
        {
            var folders = SourceFolderPaths.ToArray();

            if (folders.Length == 0)
                return Enumerable.Empty<string>();

            var guids = AssetDatabase.FindAssets(AssetKind.ToSearchFilter(), folders)
                .Where(guid => AssetKind.IsMainAssetOfKind(AssetDatabase.GUIDToAssetPath(guid)));

            if (IncludeSubfolders)
                return guids;

            return guids.Where(guid => folders.Contains(Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guid))?.Replace('\\', '/')));
        }


        public bool IsManual(string guid)
        {
            return ManualAssetGuids.Contains(guid);
        }
    }
}
