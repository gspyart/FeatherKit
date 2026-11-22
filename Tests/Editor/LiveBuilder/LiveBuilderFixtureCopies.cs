using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Копия сцен-образцов (<see cref="LiveBuilderUpgradeFixtures"/>) в папку проекта: обновление старых данных правит и пишет на диск сцены,
    /// префабы и меши, а образцы в пакете должны остаться «как было».
    /// - файлы копируются со своими .meta, но с новыми идентификаторами — и ссылки между скопированными файлами переписаны на них
    /// - удаляется вместе с папкой
    /// </summary>
    public static class LiveBuilderFixtureCopies
    {
        public const string Folder = "Assets/Live Builder Upgrade Test Copies";

        private static readonly Regex GuidLine = new("guid: ([0-9a-f]{32})");
        private static readonly string[] TextExtensions = { ".unity", ".prefab", ".mat", ".asset", ".meta" };


        // Все образцы — в папку проекта; прежняя копия заменяется.
        public static void CopyIntoProject()
        {
            Delete();

            var source = Path.GetDirectoryName(LiveBuilderUpgradeFixtures.LookFilePath("x"));
            var target = Path.GetFullPath(Folder);
            var newGuids = new Dictionary<string, string>();

            foreach (var meta in Directory.GetFiles(source, "*.meta", SearchOption.AllDirectories))
            {
                var match = GuidLine.Match(File.ReadAllText(meta));

                if (match.Success)
                    newGuids[match.Groups[1].Value] = Guid.NewGuid().ToString("N");
            }

            CopyDirectory(source, target, newGuids);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void CopyDirectory(string source, string target, Dictionary<string, string> newGuids)
        {
            Directory.CreateDirectory(target);

            foreach (var file in Directory.GetFiles(source))
            {
                var targetFile = Path.Combine(target, Path.GetFileName(file));

                if (Array.IndexOf(TextExtensions, Path.GetExtension(file)) >= 0)
                    File.WriteAllText(targetFile, GuidLine.Replace(File.ReadAllText(file), match =>
                        newGuids.TryGetValue(match.Groups[1].Value, out var newGuid) ? "guid: " + newGuid : match.Value));
                else
                    File.Copy(file, targetFile);
            }

            foreach (var directory in Directory.GetDirectories(source))
                CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)), newGuids);
        }


        public static string ScenePath(string name)
        {
            return Folder + "/" + name + ".unity";
        }


        public static void Delete()
        {
            if (AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.DeleteAsset(Folder);
        }
    }
}
