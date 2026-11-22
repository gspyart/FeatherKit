using System.Collections.Generic;
using System.IO;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Сцены-образцы старых данных для миграции этапа 2: старые устройства Live Builder, сохранённые с мешами старой сборкой.
    /// - у каждой — отпечаток «по виду» (<see cref="LiveBuilderLookDigest"/>) рядом, в «&lt;Сцена&gt;.look.txt»: так она выглядела до миграции
    /// - старой сборки больше нет: образцы не переписываются, их только читают
    /// </summary>
    public static class LiveBuilderUpgradeFixtures
    {
        public const string FolderInPackage = "Tests/Editor/LiveBuilder/UpgradeFixtures";
        public const string LookSuffix = ".look.txt";

        private static readonly string[] FixtureNames =
        {
            "AddAfterCut", "RoomKinds", "StoreyLinks", "Repeats", "CopiesUp", "ArrayModifierOnParts", "ArrayModifierOnCutters", "CutBoxes", "LegacyPrefab"
        };


        public static IEnumerable<string> Names => FixtureNames;


        // Путь сцены-образца в пакете — для открытия через AssetDatabase.
        public static string ScenePath(string name)
        {
            return PackageAssetFolder() + "/" + name + ".unity";
        }


        // Файл отпечатка «по виду» образца на диске.
        public static string LookFilePath(string name)
        {
            return Path.Combine(PackageFolderOnDisk(), name + LookSuffix);
        }


        private static string PackageAssetFolder()
        {
            return "Packages/" + Package().name + "/" + FolderInPackage;
        }

        private static string PackageFolderOnDisk()
        {
            return Path.Combine(Package().resolvedPath, FolderInPackage);
        }

        private static UnityEditor.PackageManager.PackageInfo Package()
        {
            return UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(LiveBuilderUpgradeFixtures).Assembly);
        }
    }
}
