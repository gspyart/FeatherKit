using System.Collections.Generic;
using System.IO;
using System.Text;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Команда «Upgrade Project Data…»: старые данные Live Builder во всём проекте — сначала список, что изменится, потом подтверждение.
    /// - префабы-ассеты: старые — по содержимому; сцены — те, где есть объекты Live Builder (что в них старое, видно только открыв)
    /// - префабы раньше сцен, вложенные и базовые раньше; обновлённое сохраняется, открытые сцены возвращаются как были
    /// </summary>
    public static class LiveBuilderUpgradeProjectCommand
    {
        private const string MenuPath = "Tools/FeatherKit/Live Builder/Upgrade Project Data…";
        private const int ListedNamesLimit = 15;


        [MenuItem(MenuPath)]
        private static void UpgradeProjectData()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var prefabs = FindOldPrefabs();
            var scenes = FindScenesWithLiveBuilder();

            if (prefabs.Count == 0 && scenes.Count == 0)
            {
                EditorUtility.DisplayDialog("Upgrade Project Data", "No Live Builder data to upgrade in this project.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Upgrade Project Data", DescribePlan(prefabs, scenes), "Upgrade and Save", "Cancel"))
                return;

            var setup = EditorSceneManager.GetSceneManagerSetup();

            using (LiveBuilderUpgrade.SuppressAutomatic())
            {
                foreach (var prefabPath in prefabs)
                    UpgradePrefab(prefabPath);

                foreach (var scenePath in scenes)
                    UpgradeScene(scenePath);
            }

            if (setup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(setup);
        }


        // ── Что изменится ────────────────────────────────────────────────

        private static List<string> FindOldPrefabs()
        {
            var prefabs = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (LiveBuilderUpgrade.NeedsUpgrade(new[] { AssetDatabase.LoadAssetAtPath<GameObject>(path) }, true))
                    prefabs.Add(path);
            }

            LiveBuilderUpgrade.SortDependenciesFirst(prefabs);
            return prefabs;
        }

        // Сцены, в файле которых есть компонент Live Builder, — по идентификатору его скрипта.
        private static List<string> FindScenesWithLiveBuilder()
        {
            var scriptIds = LiveBuilderScriptIds();
            var scenes = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var text = File.ReadAllText(path);

                foreach (var scriptId in scriptIds)
                    if (text.Contains(scriptId))
                    {
                        scenes.Add(path);
                        break;
                    }
            }

            return scenes;
        }

        private static List<string> LiveBuilderScriptIds()
        {
            var types = new[] { typeof(BoxModel), typeof(SplineWall), typeof(SplineBuilding), typeof(GeometryCutter) };
            var ids = new List<string>();

            foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
                if (script != null && System.Array.IndexOf(types, script.GetClass()) >= 0)
                    ids.Add(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script)));

            return ids;
        }

        private static string DescribePlan(List<string> prefabs, List<string> scenes)
        {
            var text = new StringBuilder();
            text.AppendLine("Prefabs with old data — upgraded and saved (" + prefabs.Count + "):");
            AppendNames(text, prefabs);
            text.AppendLine();
            text.AppendLine("Scenes with Live Builder objects — opened, upgraded if old, saved (" + scenes.Count + "):");
            AppendNames(text, scenes);
            text.AppendLine();
            text.Append("What changed and what may look different goes to the Console.");
            return text.ToString();
        }

        private static void AppendNames(StringBuilder text, List<string> paths)
        {
            for (var index = 0; index < paths.Count && index < ListedNamesLimit; index++)
                text.AppendLine("  " + Path.GetFileName(paths[index]));

            if (paths.Count > ListedNamesLimit)
                text.AppendLine("  … and " + (paths.Count - ListedNamesLimit) + " more");
        }


        // ── Обновление ───────────────────────────────────────────────────

        private static void UpgradePrefab(string prefabPath)
        {
            var report = new LiveBuilderUpgradeReport(Path.GetFileName(prefabPath));

            if (LiveBuilderUpgrade.UpgradePrefab(prefabPath, report))
                report.Log();
        }

        private static void UpgradeScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var report = LiveBuilderUpgrade.UpgradeScene(scene);

            if (!report.HasChanges)
                return;

            EditorSceneManager.SaveScene(scene);
            report.Log();
        }
    }
}
