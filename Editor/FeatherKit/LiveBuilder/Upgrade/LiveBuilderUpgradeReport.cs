using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Итог обновления старых данных одной сцены или префаба: что переложено по-новому и что может выглядеть иначе.
    /// - строки по объектам со ссылкой на объект — клик в консоли выделяет его; одна строка-итог сверху
    /// - тексты по-английски, как все сообщения в консоль; тот же текст — файлом для отчёта
    /// </summary>
    public sealed class LiveBuilderUpgradeReport
    {
        private readonly List<LiveBuilderUpgradeReportLine> restructured = new();
        private readonly List<LiveBuilderUpgradeReportLine> mayLookDifferent = new();
        private readonly List<string> upgradedPrefabs = new();
        private readonly HashSet<Object> restructuredObjects = new();
        private readonly HashSet<Object> differentObjects = new();


        public LiveBuilderUpgradeReport(string assetName)
        {
            AssetName = assetName;
        }


        // Сцена или префаб, которые обновлены.
        public string AssetName { get; }

        // Сколько объектов сверено после обновления: тела объектов из коробок, меши стен и частей зданий.
        public int CheckedCount { get; set; }

        public bool HasChanges => restructured.Count > 0 || mayLookDifferent.Count > 0 || upgradedPrefabs.Count > 0 || IsDataVersionRaised;

        // Объекты получили новую версию данных — даже если устройство не поменялось.
        public bool IsDataVersionRaised { get; set; }

        public IReadOnlyList<LiveBuilderUpgradeReportLine> Restructured => restructured;

        public IReadOnlyList<LiveBuilderUpgradeReportLine> MayLookDifferent => mayLookDifferent;

        public IReadOnlyList<string> UpgradedPrefabs => upgradedPrefabs;


        // ── Запись ───────────────────────────────────────────────────────

        public void AddRestructured(Object target, string what)
        {
            restructured.Add(new LiveBuilderUpgradeReportLine(target, PathOf(target) + " — " + what));
            restructuredObjects.Add(target);
        }


        public void AddMayLookDifferent(Object target, string why)
        {
            mayLookDifferent.Add(new LiveBuilderUpgradeReportLine(target, PathOf(target) + " — " + why));
            differentObjects.Add(target);
        }


        public void AddUpgradedPrefab(string prefabPath)
        {
            if (!upgradedPrefabs.Contains(prefabPath))
                upgradedPrefabs.Add(prefabPath);
        }


        // Итог префаба, обновлённого по пути, — в итог сцены: его строки тоже нужны тому, кто открыл сцену.
        public void Include(LiveBuilderUpgradeReport prefabReport)
        {
            restructured.AddRange(prefabReport.restructured);
            mayLookDifferent.AddRange(prefabReport.mayLookDifferent);
            restructuredObjects.UnionWith(prefabReport.restructuredObjects);
            differentObjects.UnionWith(prefabReport.differentObjects);
            CheckedCount += prefabReport.CheckedCount;
        }


        // ── Текст ────────────────────────────────────────────────────────

        public string Summary
        {
            get
            {
                var summary = new StringBuilder("Live Builder: upgraded '" + AssetName + "' — " + restructuredObjects.Count + " objects restructured, " +
                                                differentObjects.Count + " may look different");

                if (upgradedPrefabs.Count > 0)
                    summary.Append(", " + upgradedPrefabs.Count + " prefabs upgraded (" + string.Join(", ", FileNamesOf(upgradedPrefabs)) + ")");

                summary.Append(". Look checks after upgrading: " + CheckedCount + ".");
                return summary.ToString();
            }
        }


        public string ToText()
        {
            var text = new StringBuilder(Summary).AppendLine();

            foreach (var line in restructured)
                text.AppendLine("  Restructured: " + line.Text);

            foreach (var line in mayLookDifferent)
                text.AppendLine("  May look different: " + line.Text);

            return text.ToString();
        }


        // Строка-итог и по строке на объект: у строки — ссылка на объект, клик выделяет его.
        public void Log()
        {
            Debug.Log(Summary);

            foreach (var line in restructured)
                Debug.Log("  Restructured: " + line.Text, line.Target);

            foreach (var line in mayLookDifferent)
                Debug.LogWarning("  May look different: " + line.Text, line.Target);
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Путь в иерархии в кавычках: 'Tower A/Opening'.
        public static string PathOf(Object target)
        {
            var transform = target switch
            {
                Component component => component.transform,
                GameObject gameObject => gameObject.transform,
                _ => null
            };

            if (transform == null)
                return target != null ? "'" + target.name + "'" : "'(deleted)'";

            var path = transform.name;

            for (var current = transform.parent; current != null; current = current.parent)
                path = current.name + "/" + path;

            return "'" + path + "'";
        }

        private static IEnumerable<string> FileNamesOf(IEnumerable<string> paths)
        {
            foreach (var path in paths)
                yield return System.IO.Path.GetFileName(path);
        }
    }


    /// <summary>Строка итога обновления: текст и объект, который выделит клик в консоли.</summary>
    public readonly struct LiveBuilderUpgradeReportLine
    {
        public LiveBuilderUpgradeReportLine(Object target, string text)
        {
            Target = target;
            Text = text;
        }


        public Object Target { get; }

        public string Text { get; }
    }
}
