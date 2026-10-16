using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Разделы сценария «Когда», «Если», «Что сделать» — одни и те же в инспекторе и в окне сценариев.
    /// - в игре: что сейчас идёт, подсветка шага, кнопки «Запустить» и «Остановить»
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
                "Нет событий — сценарий запустится только из другого сценария, кнопки или Timeline");

            conditionsList = new ScenarioBlockList(serializedObject.FindProperty("conditions"), typeof(ScenarioCondition),
                "Без условий — запускается всегда");

            actionsList = new ScenarioBlockList(serializedObject.FindProperty("actions"), typeof(ScenarioAction),
                "Нет шагов", () => Application.isPlaying && scenario != null ? scenario.CurrentActionIndex : -1);

            conditionsMode = serializedObject.FindProperty("conditionsMode");
            runsOnce = serializedObject.FindProperty("runsOnce");
        }


        public void Draw()
        {
            serializedObject.Update();

            DrawRunState();

            InspectorSection.Draw("scenario.events", "Когда", 0, () =>
            {
                DrawListTools(eventsList);
                eventsList.Draw();
                DrawMissingTriggerWarning();
            });

            InspectorSection.Draw("scenario.conditions", "Если", 1, () =>
            {
                DrawListTools(conditionsList);
                EditorGUILayout.PropertyField(conditionsMode, new GUIContent("Нужно", "Сколько условий должно сойтись для запуска"));
                conditionsList.Draw();
            });

            InspectorSection.Draw("scenario.actions", "Что сделать", 2, () =>
            {
                DrawListTools(actionsList);
                actionsList.Draw();
            });

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(runsOnce, new GUIContent("Только один раз", runsOnce.tooltip));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawListTools(ScenarioBlockList blockList)
        {
            if (!showsListTools)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (ListToolButton("Копировать всё"))
                    blockList.CopyAll();

                using (new EditorGUI.DisabledScope(!blockList.CanPaste))
                {
                    if (ListToolButton($"Вставить ({ScenarioBlockClipboard.Count})"))
                        blockList.PasteAtEnd();
                }

                if (ListToolButton("Свернуть всё"))
                    blockList.SetAllExpanded(false);

                if (ListToolButton("Развернуть всё"))
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

            var state = scenario.IsRunning
                ? $"Идёт шаг {scenario.CurrentActionIndex + 1}"
                : scenario.HasRun ? "Ждёт события · уже срабатывал" : "Ждёт события";

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(state, EditorStyles.boldLabel);

                if (GUILayout.Button("Запустить", GUILayout.Width(80)) && !scenario.TryRun())
                    ShowNotification("Не запустился: уже идёт, уже был или условия не сошлись");

                using (new EditorGUI.DisabledScope(!scenario.IsRunning))
                {
                    if (GUILayout.Button("Остановить", GUILayout.Width(80)))
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

            EditorGUILayout.HelpBox("Событию триггера нужен коллайдер с галкой Is Trigger на этом же объекте.", MessageType.Warning);

            if (GUILayout.Button("Добавить триггер-коробку"))
            {
                var box = Undo.AddComponent<BoxCollider>(scenario.gameObject);
                box.isTrigger = true;
            }
        }
    }
}
