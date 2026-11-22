using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using FeatherKit.EditorTools.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Обновление копий сцен и префабов автора на втором стенде, не в его проекте: итог обновления и сверка вида с эталоном «как было» — файлом.
    /// - сцена: обновить, пересобрать, сверить общий раздел отпечатка (<see cref="LiveBuilderLookDigest.DescribeSceneAsSeen"/>), сохранить и открыть ещё раз
    /// - запуск: -executeMethod LiveBuilderUpgradeCommandLine.UpgradeFromCommandLine -upgradeAssets "путь;путь" -lookBaselines "папка" -upgradeOut "папка"
    /// </summary>
    public static class LiveBuilderUpgradeCommandLine
    {
        private const string LogPrefix = "LIVEBUILDER upgrade ";


        public static void UpgradeFromCommandLine()
        {
            var assetPaths = ArgumentAfter("-upgradeAssets").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var baselineFolder = ArgumentAfter("-lookBaselines");
            var outputFolder = ArgumentAfter("-upgradeOut");
            Directory.CreateDirectory(outputFolder);

            foreach (var assetPath in assetPaths)
            {
                var name = Path.GetFileNameWithoutExtension(assetPath);
                var baseline = LiveBuilderLookDigest.SectionOf(File.ReadAllLines(Path.Combine(baselineFolder, name + ".look.txt")), LiveBuilderLookDigest.WholeSection);
                var text = assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? UpgradePrefab(assetPath, baseline) : UpgradeScene(assetPath, baseline);
                File.WriteAllText(Path.Combine(outputFolder, name + ".migration.txt"), text);
                Debug.Log(LogPrefix + assetPath + "\n" + text);
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static string ArgumentAfter(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, name);

            if (index < 0 || index + 1 >= arguments.Length)
                throw new ArgumentException("Missing command line argument " + name);

            return arguments[index + 1];
        }


        // ── Сцена ────────────────────────────────────────────────────────

        private static string UpgradeScene(string scenePath, List<string> baseline)
        {
            var text = new StringBuilder();
            var stopwatch = Stopwatch.StartNew();
            LiveBuilderUpgradeReport report;

            using (LiveBuilderUpgrade.SuppressAutomatic())
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                report = LiveBuilderUpgrade.UpgradeScene(scene);
                LiveBuilderRebuildQueue.RunFinalPassNow();
                text.Append(report.ToText());
                text.AppendLine("Upgrade took " + stopwatch.ElapsedMilliseconds + " ms.");
                AppendLookCheck(text, baseline, LiveBuilderLookDigest.SectionOf(LiveBuilderLookDigest.DescribeSceneAsSeen(scene), LiveBuilderLookDigest.WholeSection));
                EditorSceneManager.SaveScene(scene);
            }

            var upgradedAgain = 0;
            void CountUpgrade(LiveBuilderUpgradeReport _) => upgradedAgain++;
            LiveBuilderUpgrade.Upgraded += CountUpgrade;
            var reopened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            LiveBuilderUpgrade.Upgraded -= CountUpgrade;
            text.AppendLine("Second open: " + (upgradedAgain == 0 && !reopened.isDirty ? "nothing to upgrade, scene not marked changed." : "upgraded again or marked changed!"));
            return text.ToString();
        }


        // ── Префаб ───────────────────────────────────────────────────────

        private static string UpgradePrefab(string prefabPath, List<string> baseline)
        {
            var text = new StringBuilder();
            var stopwatch = Stopwatch.StartNew();
            var report = new LiveBuilderUpgradeReport(Path.GetFileName(prefabPath));
            var isUpgraded = LiveBuilderUpgrade.UpgradePrefab(prefabPath, report);
            text.Append(isUpgraded ? report.ToText() : "Already up to date.\n");
            text.AppendLine("Upgrade took " + stopwatch.ElapsedMilliseconds + " ms.");

            var contents = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                AppendLookCheck(text, baseline, LiveBuilderLookDigest.SectionOf(LiveBuilderLookDigest.DescribeAsSeen(contents.transform), LiveBuilderLookDigest.WholeSection));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            text.AppendLine("Second upgrade: " + (LiveBuilderUpgrade.UpgradePrefab(prefabPath, new LiveBuilderUpgradeReport("")) ? "upgraded again!" : "nothing to upgrade."));
            return text.ToString();
        }


        private static void AppendLookCheck(StringBuilder text, List<string> baseline, List<string> actual)
        {
            var differences = LiveBuilderLookDigest.Compare(baseline, actual);
            text.AppendLine("Look against the baseline before the upgrade: " + (differences.Count == 0 ? "same." : differences.Count + " differences:"));

            foreach (var difference in differences)
                text.AppendLine("  " + difference);
        }
    }
}
