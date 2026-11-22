using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Страница настроек Live Builder в Project Settings → FeatherKit → Live Builder: все поля <see cref="LiveBuilderProjectSettings"/>
    /// по группам, с подсказками; правка сразу пишется в ProjectSettings и перерисовывает окно сцены.
    /// </summary>
    public static class LiveBuilderProjectSettingsPage
    {
        private const string SettingsPath = "Project/FeatherKit/Live Builder";


        // ── Страница настроек ────────────────────────────────────────────

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Live Builder",
                guiHandler = _ => DrawSettings(),
                keywords = new[] { "live builder", "box model", "wall", "building", "opening", "cut", "section", "коробки", "стены", "здания", "проём", "разрез" }
            };
        }

        private static void DrawSettings()
        {
            var settings = LiveBuilderProjectSettings.instance;

            // Одиночка настроек создаётся только для чтения — иначе поля не правятся.
            settings.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;

            var serializedSettings = new SerializedObject(settings);

            EditorGUILayout.HelpBox("What each game sets its own way. Shared by the project: stored in ProjectSettings/FeatherKit and goes to git. " +
                                    "Personal Live Builder settings — grid, storey, tool — are per user.", MessageType.None);

            EditorGUI.BeginChangeCheck();

            var property = serializedSettings.GetIterator();
            var isFirst = true;

            while (property.NextVisible(isFirst))
            {
                isFirst = false;

                if (property.name != "m_Script")
                    EditorGUILayout.PropertyField(property, true);
            }

            if (!EditorGUI.EndChangeCheck())
                return;

            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            settings.SaveChanges();
            SceneView.RepaintAll();
        }
    }
}
