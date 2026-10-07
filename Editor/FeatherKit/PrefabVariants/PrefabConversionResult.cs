using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Итог переделки одного префаба в вариант шаблона — или её предпросмотра: получилось ли,
    /// что стало с какими частями, какие внутренние номера объектов сменились и в каких файлах
    /// исправлены ссылки. Окно по нему рисует строку, пачка дописывает в него починку ссылок.
    /// </summary>
    public sealed class PrefabConversionResult
    {
        private readonly List<PrefabPartMatch> parts = new List<PrefabPartMatch>();
        private readonly List<string> warnings = new List<string>();
        private readonly List<string> fixedFiles = new List<string>();
        private readonly Dictionary<long, long> fileIdChanges = new Dictionary<long, long>();


        public PrefabConversionResult(string prefabPath)
        {
            PrefabPath = prefabPath;
            Status = PrefabConversionStatus.Ready;
        }


        public string PrefabPath { get; }

        public string PrefabName => Path.GetFileNameWithoutExtension(PrefabPath);

        public PrefabConversionStatus Status { get; private set; }

        /// <summary>Почему пропущен или почему не вышло. У удачных — пусто.</summary>
        public string Reason { get; private set; }

        public IReadOnlyList<PrefabPartMatch> Parts => parts;

        public IReadOnlyList<string> Warnings => warnings;

        /// <summary>Файлы, где переписаны ссылки на этот префаб.</summary>
        public IReadOnlyList<string> FixedFiles => fixedFiles;

        /// <summary>Старый номер объекта в файле префаба → новый. По ним чинятся ссылки.</summary>
        public IReadOnlyDictionary<long, long> FileIdChanges => fileIdChanges;


        // ── Статус ───────────────────────────────────

        public void Skip(string reason)
        {
            Status = PrefabConversionStatus.Skipped;
            Reason = reason;
        }


        public void Fail(string reason)
        {
            Status = PrefabConversionStatus.Failed;
            Reason = reason;
        }


        public void MarkConverted() => Status = PrefabConversionStatus.Converted;


        // ── Наполнение ───────────────────────────────

        public void AddPart(PrefabPartMatch part) => parts.Add(part);


        public void AddWarning(string warning) => warnings.Add(warning);


        public void AddFileIdChange(long oldFileId, long newFileId) => fileIdChanges[oldFileId] = newFileId;


        public void AddFixedFiles(IEnumerable<string> paths) => fixedFiles.AddRange(paths);


        // ── Сводка ───────────────────────────────────

        public int CountParts(PrefabPartMatchKind kind) => parts.Count(part => part.Kind == kind);
    }
}
