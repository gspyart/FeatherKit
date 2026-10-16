using System;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Нижняя панель окна сценариев: журнал запусков, новые сверху.
    /// Можно оставить только выбранный сценарий; клик по строке выбирает её сценарий.
    /// </summary>
    internal class ScenarioRunLogPanel
    {
        private static readonly Color FailedColor = new Color(0.95f, 0.45f, 0.4f);
        private static readonly Color QuietColor = new Color(0.6f, 0.6f, 0.6f);

        private readonly Action<Scenario> scenarioClicked;

        private Vector2 scroll;
        private bool showsOnlySelected;


        public ScenarioRunLogPanel(Action<Scenario> scenarioClicked)
        {
            this.scenarioClicked = scenarioClicked;
        }


        public void Draw(Scenario selected, float height)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Height(height)))
            {
                DrawHeader();

                scroll = EditorGUILayout.BeginScrollView(scroll);
                DrawEntries(selected);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Журнал запусков", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                showsOnlySelected = GUILayout.Toggle(showsOnlySelected, "Только выбранный", EditorStyles.toolbarButton);

                if (GUILayout.Button("Очистить", EditorStyles.toolbarButton))
                    ScenarioRunLog.Clear();
            }
        }

        private void DrawEntries(Scenario selected)
        {
            var entries = ScenarioRunLog.Entries;
            var shownCount = 0;

            for (var index = entries.Count - 1; index >= 0; index--)
            {
                var record = entries[index].Record;

                if (showsOnlySelected && record.Scenario != selected)
                    continue;

                DrawEntry(entries[index]);
                shownCount++;
            }

            if (shownCount == 0)
                EditorGUILayout.LabelField(Application.isPlaying ? "Пока ничего не запускалось" : "Журнал пишется в игре",
                    EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawEntry(ScenarioRunLogEntry entry)
        {
            var record = entry.Record;
            var scenarioName = record.Scenario != null ? record.Scenario.name : "(удалён)";
            var repeat = entry.RepeatCount > 1 ? $"  ×{entry.RepeatCount}" : "";
            var instigator = record.InstigatorName != null ? $"  · вызвал {record.InstigatorName}" : "";
            var text = $"{record.EndTime,7:0.00}   {scenarioName} — {OutcomeTextOf(record)}{repeat}{instigator}";

            var previousColor = GUI.contentColor;
            GUI.contentColor = ColorOf(record.Outcome, previousColor);

            if (GUILayout.Button(text, EditorStyles.label) && record.Scenario != null)
                scenarioClicked(record.Scenario);

            GUI.contentColor = previousColor;
        }

        private static string OutcomeTextOf(ScenarioRunRecord record)
        {
            return record.Outcome switch
            {
                ScenarioRunOutcome.Finished => $"отработал за {record.EndTime - record.StartTime:0.##} с",
                ScenarioRunOutcome.Stopped => "остановлен",
                ScenarioRunOutcome.Failed => $"оборван на шаге {record.FailedActionIndex + 1}: {record.FailureMessage}",
                _ => "условия не сошлись"
            };
        }

        private static Color ColorOf(ScenarioRunOutcome outcome, Color normal)
        {
            return outcome switch
            {
                ScenarioRunOutcome.Failed => FailedColor,
                ScenarioRunOutcome.ConditionsNotMet => QuietColor,
                _ => normal
            };
        }
    }
}
