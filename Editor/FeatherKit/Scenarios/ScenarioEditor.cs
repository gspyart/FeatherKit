using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Инспектор сценария: описание, кнопка «Открыть в окне» и разделы из ScenarioSectionsDrawer.
    /// Тесно — то же самое шире и со списком всех сценариев в окне FeatherKit → Сценарии.
    /// </summary>
    [CustomEditor(typeof(Scenario))]
    public class ScenarioEditor : Editor
    {
        private ScenarioSectionsDrawer sections;


        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }


        public override void OnInspectorGUI()
        {
            DrawDescription();

            if (GUILayout.Button("Открыть в окне сценариев"))
                ScenarioWindow.Open((Scenario)target);

            sections.Draw();
        }

        private void DrawDescription()
        {
            var description = FDescriptionHeader.DescriptionOf(target);

            if (!string.IsNullOrEmpty(description))
                EditorGUILayout.HelpBox(description, MessageType.None);
        }


        // ── Unity ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            sections = new ScenarioSectionsDrawer(serializedObject, showsListTools: false);
        }
    }
}
