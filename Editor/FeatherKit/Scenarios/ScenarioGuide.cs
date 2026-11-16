using System.IO;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Руководство по графу Scenario — Documentation~/Scenarios.md пакета: открывается в приложении по умолчанию
    /// кнопкой «?» в шапке окна графа и пунктом меню. Папка Documentation~ в окне Project Unity не видна — поэтому так.
    /// </summary>
    internal static class ScenarioGuide
    {
        private const string MenuPath = "FeatherKit/Scenarios/Guide";
        private const string GuidePath = "Documentation~/Scenarios.md";


        [MenuItem(MenuPath)]
        public static void Open()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ScenarioGuide).Assembly);
            var path = package != null ? Path.Combine(package.resolvedPath, GuidePath) : null;

            if (path == null || !File.Exists(path))
            {
                EditorUtility.DisplayDialog("Scenarios guide", $"The guide wasn't found: {path ?? GuidePath}. It lives in the FeatherKit package, folder Documentation~.", "OK");
                return;
            }

            EditorUtility.OpenWithDefaultApp(path);
        }
    }
}
