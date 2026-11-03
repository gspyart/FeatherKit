using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Разделы сценария «When», «If», «Do» — одни и те же в инспекторе и в окне сценариев.
    /// - в игре: что сейчас идёт, подсветка шага, кнопки «Run» и «Stop»
    /// - предупреждает, если событию триггера не хватает коллайдера на объекте
    /// - в окне над каждым разделом: копировать, вставить, свернуть и развернуть всё
    /// </summary>
    internal class ScenarioSectionsDrawer
    {
        private readonly SerializedObject serializedObject;
        private readonly Scenario scenario;
        private readonly bool showsListTools;
        private readonly ScenarioBlockList eventsList;
        private readonly ScenarioBlockList conditionsList;
        private readonly ScenarioBlockList actionsList;
        private readonly SerializedProperty conditionsMode;
        private readonly SerializedProperty runsOnce;


        public ScenarioSectionsDrawer(SerializedObject serializedObject, bool showsListTools)
        {
            this.serializedObject = serializedObject;
            this.showsListTools = showsListTools;
            scenario = (Scenario)serializedObject.targetObject;

            eventsList = new ScenarioBlockList(serializedObject.FindProperty("events"), typeof(ScenarioEvent),
                "No events — the scenario only runs from another scenario, a button or Timeline");

            conditionsList = new ScenarioBlockList(serializedObject.FindProperty("conditions"), typeof(ScenarioCondition),
                "No conditions — always runs");

            actionsList = new ScenarioBlockList(serializedObject.FindProperty("actions"), typeof(ScenarioAction), "No steps");

            conditionsMode = serializedObject.FindProperty("conditionsMode");
            runsOnce = serializedObject.FindProperty("runsOnce");
        }


        public void Draw()
        {
            serializedObject.Update();

            DrawRunState();

            InspectorSection.Draw("scenario.events", "When", 0, () =>
            {
                DrawListTools(eventsList);
                eventsList.Draw();
                DrawMissingTriggerWarning();
            });

            InspectorSection.Draw("scenario.conditions", "If", 1, () =>
            {
                DrawListTools(conditionsList);
                EditorGUILayout.PropertyField(conditionsMode, new GUIContent("Required", "How many conditions must be met to run"));
                conditionsList.Draw();
            });

            InspectorSection.Draw("scenario.actions", "Do", 2, () =>
            {
                DrawListTools(actionsList);
                actionsList.Draw();
            });

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(runsOnce, new GUIContent("Run Once", runsOnce.tooltip));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawListTools(ScenarioBlockList blockList)
        {
            if (!showsListTools)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (ListToolButton("Copy All"))
                    blockList.CopyAll();

                using (new EditorGUI.DisabledScope(!blockList.CanPaste))
                {
                    if (ListToolButton($"Paste ({ScenarioBlockClipboard.Count})"))
                        blockList.PasteAtEnd();
                }

                if (ListToolButton("Collapse All"))
                    blockList.SetAllExpanded(false);

                if (ListToolButton("Expand All"))
                    blockList.SetAllExpanded(true);
            }
        }

        private static bool ListToolButton(string text)
        {
            return GUILayout.Button(text, EditorStyles.miniButton, GUILayout.ExpandWidth(false));
        }


        // ── Состояние в игре ─────────────────────────────────────────────

        private void DrawRunState()
        {
            if (!Application.isPlaying || scenario == null)
                return;

            var state = !scenario.IsRunning
                ? scenario.HasRun ? "Waiting for an event · already ran" : "Waiting for an event"
                : scenario.CurrentActionIndex >= 0 ? $"Running step {scenario.CurrentActionIndex + 1}" : "Finishing background steps";

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(state, EditorStyles.boldLabel);

                if (GUILayout.Button("Run", GUILayout.Width(80)) && !scenario.TryRun())
                    ShowNotification("Did not start: already running, already ran or conditions not met");

                using (new EditorGUI.DisabledScope(!scenario.IsRunning))
                {
                    if (GUILayout.Button("Stop", GUILayout.Width(80)))
                        scenario.Stop();
                }
            }
        }

        private static void ShowNotification(string text)
        {
            var window = EditorWindow.focusedWindow;

            if (window != null)
                window.ShowNotification(new GUIContent(text));
        }


        // ── Проверки ─────────────────────────────────────────────────────

        private void DrawMissingTriggerWarning()
        {
            if (!scenario.Events.Any(scenarioEvent => scenarioEvent is TriggerEvent)
                || scenario.GetComponents<Collider>().Any(collider => collider.isTrigger))
                return;

            EditorGUILayout.HelpBox("A trigger event needs a collider with Is Trigger checked on this same object.", MessageType.Warning);

            if (GUILayout.Button("Add Trigger Box"))
            {
                var box = Undo.AddComponent<BoxCollider>(scenario.gameObject);
                box.isTrigger = true;
            }
        }
    }
}
