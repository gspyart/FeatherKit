using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Страница настроек заметок в Project Settings → FeatherKit → Level Notes: поля <see cref="LevelNotesProjectSettings"/>
    /// с подсказками; правка сразу пишется в ProjectSettings.
    /// </summary>
    public static class LevelNotesProjectSettingsPage
    {
        private const string SettingsPath = "Project/FeatherKit/Level Notes";


        // ── Страница настроек ────────────────────────────────────────────

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Level Notes",
                guiHandler = _ => DrawSettings(),
                keywords = new[] { "level notes", "note", "flag", "заметки", "флажок" }
            };
        }

        private static void DrawSettings()
        {
            var settings = LevelNotesProjectSettings.instance;

            // Одиночка настроек создаётся только для чтения — иначе поля не правятся.
            settings.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;

            var serializedSettings = new SerializedObject(settings);

            EditorGUILayout.HelpBox("Общее на проект: хранится в ProjectSettings/FeatherKit и попадает в git.", MessageType.None);

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
        }
    }
}
