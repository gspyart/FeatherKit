using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Инспектор сценария: откуда граф (этот объект или граф-файл), значения открытых переменных, кнопка «Open Graph».
    /// Пока встроенный граф пуст — ниже старые разделы «When / If / Do» из ScenarioSectionsDrawer и кнопка «Convert to Graph».
    /// Блоки со старыми полями — предупреждение с кнопкой «Upgrade Fields»; сам инспектор ничего не переносит.
    /// </summary>
    [CustomEditor(typeof(Scenario))]
    public class ScenarioEditor : Editor
    {
        private ScenarioSectionsDrawer sections;
        private ScenarioBindingsSection bindings;
        private SerializedProperty graphSource;
        private SerializedProperty graphAsset;
        private int oldFieldsCount;
        private int oldFieldsCheckedVersion = -1;


        private Scenario Scenario => (Scenario)target;

        /// <summary>Старые списки живут, пока у сценария нет своего графа: у 3 сценариев игры они ещё в работе.</summary>
        private bool ShowsListMode => Scenario.GraphSource == ScenarioGraphSource.Embedded
                                      && (Scenario.Graph == null || Scenario.Graph.Nodes.Count == 0);


        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }


        public override void OnInspectorGUI()
        {
            DrawDescription();

            serializedObject.Update();
            DrawGraphSource();
            bindings.Draw();
            serializedObject.ApplyModifiedProperties();

            DrawOldFields();

            if (GUILayout.Button("Open Graph"))
                ScenarioGraphWindow.Open(Scenario);

            if (!ShowsListMode)
                return;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("List Mode", EditorStyles.boldLabel);
            DrawConvertButton();

            if (GUILayout.Button("Open in Scenarios Window"))
                ScenarioWindow.Open(Scenario);

            sections.Draw();
        }

        // Старые списки → нода When и нода Do; после — сразу граф, чтобы было видно, что получилось.
        private void DrawConvertButton()
        {
            if (!ScenarioGraphConverter.CanConvert(Scenario))
                return;

            EditorGUILayout.HelpBox("Old lists: When → If → Do. Convert them to a graph — a When node and a Do node, one undo step.", MessageType.Info);

            if (!GUILayout.Button("Convert to Graph"))
                return;

            ScenarioGraphConverter.Convert(Scenario);
            ScenarioGraphWindow.Open(Scenario);
            GUIUtility.ExitGUI();
        }

        // Перебор всех полей недешёв — пересчитываем, только когда сценарий или его файл правили.
        private void DrawOldFields()
        {
            var graphFile = Scenario.GraphSource == ScenarioGraphSource.Asset ? Scenario.GraphAsset : null;
            var version = EditorUtility.GetDirtyCount(Scenario) * 397 + (graphFile != null ? EditorUtility.GetDirtyCount(graphFile) + 1 : 0);

            if (version != oldFieldsCheckedVersion)
            {
                oldFieldsCheckedVersion = version;
                oldFieldsCount = ScenarioLegacyFieldsUpgrade.CountOutdatedBlocks(Scenario) + ScenarioLegacyFieldsUpgrade.CountOutdatedBlocks(graphFile);
            }

            if (oldFieldsCount == 0)
                return;

            EditorGUILayout.HelpBox($"Old fields in {oldFieldsCount} blocks: they still work, but should be moved to the new ones.", MessageType.Warning);

            if (!GUILayout.Button(ScenarioLegacyFieldsUpgrade.ButtonText))
                return;

            ScenarioLegacyFieldsUpgrade.Upgrade(Scenario);

            if (graphFile != null)
                ScenarioLegacyFieldsUpgrade.Upgrade(graphFile);

            GUIUtility.ExitGUI();
        }

        private void DrawDescription()
        {
            var description = FDescriptionHeader.DescriptionOf(target);

            if (!string.IsNullOrEmpty(description))
                EditorGUILayout.HelpBox(description, MessageType.None);
        }

        private void DrawGraphSource()
        {
            EditorGUILayout.PropertyField(graphSource, new GUIContent("Graph", "Where the graph lives: in this object or in a graph file"));

            if (graphSource.enumValueIndex != (int)ScenarioGraphSource.Asset)
                return;

            EditorGUILayout.PropertyField(graphAsset, new GUIContent("Graph File"));

            if (graphAsset.objectReferenceValue == null)
                EditorGUILayout.HelpBox("Pick a Scenario Graph file: Create → FeatherKit → Scenarios → Scenario Graph.", MessageType.Info);
        }


        // ── Unity ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            sections = new ScenarioSectionsDrawer(serializedObject, showsListTools: false);
            bindings = new ScenarioBindingsSection(serializedObject);
            graphSource = serializedObject.FindProperty("graphSource");
            graphAsset = serializedObject.FindProperty("graphAsset");
        }
    }
}
