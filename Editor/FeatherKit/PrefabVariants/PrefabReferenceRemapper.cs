using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Чинит ссылки на объекты внутри префаба, когда у них сменились внутренние номера.
    /// Ссылка в файле Unity — это пара «GUID файла + номер объекта в нём». GUID у переделанного
    /// префаба прежний, а номера новые, и без починки ссылки молча пустеют: позиции копий
    /// в сценах, поле «префаб» в конфиге, переопределения в вариантах.
    ///
    /// Номера объектов внутри ЭКЗЕМПЛЯРА префаба Unity не хранит, а вычисляет: номер экземпляра
    /// XOR номер объекта в исходнике. Поэтому починка идёт цепочкой: сменились номера в предмете —
    /// сменились и в ящике, куда он вложен, и в варианте предмета, а за ними и в сценах, где
    /// стоит ящик.
    ///
    /// Работает по тексту файлов, а не через AssetDatabase: старых объектов уже нет, и спросить
    /// про них Unity нечего. Отсюда требование — текстовая сериализация проекта.
    ///
    /// Какой файл на кого ссылается, читает один раз на всю пачку: проект большой, а ссылки
    /// от починки не меняются — меняются только номера.
    /// </summary>
    public sealed class PrefabReferenceRemapper
    {
        // Складывая номера экземпляра и исходника, Unity отбрасывает старший бит.
        private const long FileIdMask = long.MaxValue;
        private const string GuidMarker = "guid: ";
        private const int GuidLength = 32;
        private const string YamlSignature = "%YAML";

        // Ссылаться на объекты префаба могут только эти. Материалы, анимации и текстуры
        // на объекты внутри префабов не ссылаются — читать их незачем.
        private static readonly string[] SearchableExtensions =
        {
            ".unity", ".prefab", ".asset", ".playable", ".controller", ".overrideController", ".preset"
        };

        private static readonly Regex ExternalReferencePattern =
            new Regex(@"\{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: (\d+)\}", RegexOptions.Compiled);

        private static readonly Regex LocalReferencePattern =
            new Regex(@"\{fileID: (-?\d+)\}", RegexOptions.Compiled);

        private static readonly Regex ObjectHeaderPattern =
            new Regex(@"^(--- !u!\d+ &)(-?\d+)", RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex PrefabInstanceHeaderPattern =
            new Regex(@"^--- !u!1001 &(-?\d+)", RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex SourcePrefabPattern =
            new Regex(@"\Gm_SourcePrefab: \{fileID: -?\d+, guid: ([0-9a-f]{32})", RegexOptions.Compiled);

        private static readonly Dictionary<string, bool> EditablePackages = new Dictionary<string, bool>();

        private readonly Dictionary<string, HashSet<string>> filesByGuid = new Dictionary<string, HashSet<string>>();


        private PrefabReferenceRemapper()
        {
        }


        /// <summary>Чинить ссылки по тексту можно, только если проект хранит ассеты текстом.</summary>
        public static bool IsProjectTextSerialized => EditorSettings.serializationMode == SerializationMode.ForceText;


        // ── Чтение проекта ───────────────────────────

        /// <summary>
        /// Читает проект и запоминает, какой файл на какие GUID ссылается. Долго, поэтому
        /// один раз на пачку. Null — если человек отменил.
        /// </summary>
        public static PrefabReferenceRemapper Build()
        {
            var remapper = new PrefabReferenceRemapper();
            var paths = AssetDatabase.GetAllAssetPaths().Where(IsSearchable).ToList();

            try
            {
                for (var index = 0; index < paths.Count; index++)
                {
                    if (index % 50 == 0 && EditorUtility.DisplayCancelableProgressBar(
                            "Ищу ссылки на префабы", paths[index], (float)index / paths.Count))
                        return null;

                    remapper.Reindex(paths[index]);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return remapper;
        }

        private static bool IsSearchable(string path)
        {
            var extension = Path.GetExtension(path);
            return SearchableExtensions.Any(searchable => string.Equals(extension, searchable, StringComparison.OrdinalIgnoreCase))
                   && IsEditableAssetPath(path);
        }


        /// <summary>
        /// Свой ли это файл, который можно переписывать: проект и встроенные или локальные
        /// пакеты. Скачанные пакеты лежат в кеше и при обновлении перезапишутся.
        /// </summary>
        public static bool IsEditableAssetPath(string path)
        {
            if (path.StartsWith("Assets/", StringComparison.Ordinal))
                return true;

            if (!path.StartsWith("Packages/", StringComparison.Ordinal))
                return false;

            // Пакет спрашиваем один раз, а не на каждый его файл: файлов у пакетов Unity тысячи.
            var packageRoot = string.Join("/", path.Split('/').Take(2));
            if (EditablePackages.TryGetValue(packageRoot, out var editable))
                return editable;

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
            editable = package != null && (package.source == PackageSource.Embedded || package.source == PackageSource.Local);
            EditablePackages[packageRoot] = editable;
            return editable;
        }


        /// <summary>
        /// Перечитывает, на кого ссылается файл. Нужно после того, как файл переписала сама
        /// Unity, — у переделанного префаба появилась ссылка на шаблон.
        /// </summary>
        public void Reindex(string path)
        {
            var text = ReadTextIfYaml(path);
            if (text == null)
                return;

            for (var index = text.IndexOf(GuidMarker, StringComparison.Ordinal);
                 index >= 0 && index + GuidMarker.Length + GuidLength <= text.Length;
                 index = text.IndexOf(GuidMarker, index + GuidMarker.Length, StringComparison.Ordinal))
            {
                var guid = text.Substring(index + GuidMarker.Length, GuidLength);

                if (!filesByGuid.TryGetValue(guid, out var files))
                    filesByGuid[guid] = files = new HashSet<string>();

                files.Add(path);
            }
        }


        // ── Что заденет починка ──────────────────────

        /// <summary>
        /// Все файлы, которые может задеть починка ссылок на этот префаб: прямые и по цепочке
        /// вложений и вариантов. С запасом — заранее снять с них защиту от записи.
        /// </summary>
        public HashSet<string> FindAffectedFiles(string prefabGuid)
        {
            var affected = new HashSet<string>();
            var visited = new HashSet<string> { prefabGuid };
            var pending = new Queue<string>();
            pending.Enqueue(prefabGuid);

            while (pending.Count > 0)
            {
                if (!filesByGuid.TryGetValue(pending.Dequeue(), out var files))
                    continue;

                foreach (var path in files)
                {
                    affected.Add(path);

                    if (!IsPrefabFile(path))
                        continue;

                    var guid = AssetDatabase.AssetPathToGUID(path);
                    if (visited.Add(guid))
                        pending.Enqueue(guid);
                }
            }

            return affected;
        }


        // ── Починка ──────────────────────────────────

        /// <summary>
        /// Переписывает ссылки на префаб со старых номеров на новые во всех файлах проекта,
        /// по цепочке вложений и вариантов. Изменённые файлы сразу записываются и перечитываются
        /// Unity. Файлы, которые записать не вышло, — в failedFiles.
        /// </summary>
        public List<string> Remap(string prefabGuid, IReadOnlyDictionary<long, long> fileIdChanges, out List<string> failedFiles)
        {
            var editedTexts = new Dictionary<string, string>();
            var pending = new Queue<(string guid, Dictionary<long, long> changes)>();
            pending.Enqueue((prefabGuid, fileIdChanges.ToDictionary(change => change.Key, change => change.Value)));

            while (pending.Count > 0)
            {
                var (guid, changes) = pending.Dequeue();

                if (changes.Count == 0 || !filesByGuid.TryGetValue(guid, out var files))
                    continue;

                foreach (var path in files)
                {
                    var original = editedTexts.TryGetValue(path, out var edited) ? edited : ReadTextIfYaml(path);
                    if (original == null)
                        continue;

                    var text = ReplaceExternalReferences(original, guid, changes);
                    var derivedChanges = CollectDerivedChanges(text, guid, changes);

                    if (derivedChanges.Count > 0)
                    {
                        text = ReplaceLocalReferences(text, derivedChanges);

                        // На вычисленные объекты префаба ссылаются и снаружи — чиним дальше по цепочке.
                        // Сцены замыкают цепочку: на объекты сцены из других файлов не ссылаются.
                        if (IsPrefabFile(path))
                            pending.Enqueue((AssetDatabase.AssetPathToGUID(path), derivedChanges));
                    }

                    if (text != original)
                        editedTexts[path] = text;
                }
            }

            failedFiles = WriteAndReimport(editedTexts);
            return editedTexts.Keys.Except(failedFiles).ToList();
        }

        private static string ReplaceExternalReferences(string text, string guid, IReadOnlyDictionary<long, long> changes)
        {
            if (text.IndexOf(guid, StringComparison.Ordinal) < 0)
                return text;

            return ExternalReferencePattern.Replace(text, match =>
            {
                if (match.Groups[2].Value != guid
                    || !long.TryParse(match.Groups[1].Value, out var fileId)
                    || !changes.TryGetValue(fileId, out var newFileId))
                    return match.Value;

                return $"{{fileID: {newFileId}, guid: {guid}, type: {match.Groups[3].Value}}}";
            });
        }

        // Объекты экземпляра в файле не хранятся, а вычисляются: номер экземпляра XOR номер
        // в исходнике. Сменился номер в исходнике — сменился и вычисленный, а на него могут
        // ссылаться заглушки в этом же файле и, если это префаб, кто угодно снаружи.
        private static Dictionary<long, long> CollectDerivedChanges(string text, string sourceGuid, IReadOnlyDictionary<long, long> changes)
        {
            var derived = new Dictionary<long, long>();

            foreach (Match header in PrefabInstanceHeaderPattern.Matches(text))
            {
                if (FindSourcePrefabGuid(text, header) != sourceGuid)
                    continue;

                var instanceFileId = long.Parse(header.Groups[1].Value);

                foreach (var change in changes)
                    derived[(instanceFileId ^ change.Key) & FileIdMask] = (instanceFileId ^ change.Value) & FileIdMask;
            }

            return derived;
        }

        // Исходник экземпляра записан в конце его блока, после всех переопределений.
        private static string FindSourcePrefabGuid(string text, Match instanceHeader)
        {
            var blockStart = instanceHeader.Index + instanceHeader.Length;
            var blockEnd = text.IndexOf("\n--- ", blockStart, StringComparison.Ordinal);
            if (blockEnd < 0)
                blockEnd = text.Length;

            var sourceIndex = text.IndexOf("m_SourcePrefab: ", blockStart, blockEnd - blockStart, StringComparison.Ordinal);
            if (sourceIndex < 0)
                return null;

            var match = SourcePrefabPattern.Match(text, sourceIndex);
            return match.Success ? match.Groups[1].Value : null;
        }

        // Меняем и ссылки на заглушку, и номер в её заголовке — иначе Unity не свяжет одно с другим.
        private static string ReplaceLocalReferences(string text, IReadOnlyDictionary<long, long> changes)
        {
            text = LocalReferencePattern.Replace(text, match =>
                long.TryParse(match.Groups[1].Value, out var fileId) && changes.TryGetValue(fileId, out var newFileId)
                    ? $"{{fileID: {newFileId}}}"
                    : match.Value);

            return ObjectHeaderPattern.Replace(text, match =>
                long.TryParse(match.Groups[2].Value, out var fileId) && changes.TryGetValue(fileId, out var newFileId)
                    ? match.Groups[1].Value + newFileId
                    : match.Value);
        }

        // Unity перечитывает файлы пачкой, а не по одному: вложенные префабы иначе
        // пересобирались бы на каждом файле заново.
        private static List<string> WriteAndReimport(Dictionary<string, string> editedTexts)
        {
            var failedFiles = new List<string>();
            if (editedTexts.Count == 0)
                return failedFiles;

            foreach (var pair in editedTexts)
            {
                try
                {
                    WriteTextKeepingEncoding(pair.Key, pair.Value);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    failedFiles.Add(pair.Key);
                }
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in editedTexts.Keys.Except(failedFiles))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            return failedFiles;
        }

        // Unity пишет свои файлы без BOM. Если кто-то сохранил файл с ним — оставляем как было,
        // чтобы в истории версий не появилась правка ради одной невидимой метки.
        private static void WriteTextKeepingEncoding(string path, string text)
        {
            var bytes = File.ReadAllBytes(path);
            var hasByteOrderMark = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            File.WriteAllText(path, text, new UTF8Encoding(hasByteOrderMark));
        }


        // ── Общие помощники ──────────────────────────

        // Заглядываем в начало, прежде чем читать целиком: часть ассетов Unity пишет двоичными
        // при любой настройке (данные освещения), и тянуть их в строку незачем.
        private static string ReadTextIfYaml(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var signature = new byte[YamlSignature.Length];
                    if (stream.Read(signature, 0, signature.Length) != signature.Length
                        || Encoding.ASCII.GetString(signature) != YamlSignature)
                        return null;
                }

                return File.ReadAllText(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return null;
            }
        }


        private static bool IsPrefabFile(string path) =>
            string.Equals(Path.GetExtension(path), ".prefab", StringComparison.OrdinalIgnoreCase);
    }
}
