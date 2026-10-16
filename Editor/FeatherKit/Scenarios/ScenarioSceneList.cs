using System;
using System.Collections.Generic;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Левая панель окна сценариев: все сценарии открытых сцен или открытого префаба.
    /// - поиск по имени объекта и по событиям; клик выбирает сценарий
    /// - в игре у каждого видно, идёт ли он и срабатывал ли
    /// - создать сценарий: новым объектом или на выделенном
    /// </summary>
    internal class ScenarioSceneList
    {
        private static readonly Color SelectedRowColor = new Color(0.24f, 0.48f, 0.9f, 0.35f);
        private static readonly Color RunningColor = new Color(0.35f, 0.8f, 0.45f);

        private static GUIStyle secondLineStyle;
        private static GUIStyle playStateStyle;

        private readonly Action<Scenario> scenarioClicked;
        private readonly SearchField searchField = new SearchField();
        private readonly List<Scenario> scenarios = new List<Scenario>();

        private string searchText = "";
        private Vector2 scroll;
        private bool isStale = true;


        public ScenarioSceneList(Action<Scenario> scenarioClicked)
        {
            this.scenarioClicked = scenarioClicked;
        }


        private static GUIStyle SecondLineStyle => secondLineStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
            clipping = TextClipping.Clip
        };

        private static GUIStyle PlayStateStyle => playStateStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight
        };


        /// <summary>Сцена или иерархия поменялись — список соберётся заново при следующей отрисовке.</summary>
        public void MarkStale()
        {
            isStale = true;
        }


        // ── Отрисовка ────────────────────────────────────────────────────

        public void Draw(Scenario selected)
        {
            if (isStale)
                Collect();

            searchText = searchField.OnToolbarGUI(searchText);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            var shown = scenarios.Where(scenario => scenario != null && Matches(scenario)).ToList();

            foreach (var scenario in shown)
                DrawRow(scenario, scenario == selected);

            if (shown.Count == 0)
                EditorGUILayout.LabelField(scenarios.Count == 0 ? "Сценариев нет" : "Ничего не найдено",
                    EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndScrollView();
        }

        private bool Matches(Scenario scenario)
        {
            if (string.IsNullOrEmpty(searchText))
                return true;

            return scenario.name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
                   || EventsTextOf(scenario).IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawRow(Scenario scenario, bool isSelected)
        {
            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2f + 4f);

            if (isSelected)
                EditorGUI.DrawRect(rect, SelectedRowColor);

            var titleRect = new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, EditorGUIUtility.singleLineHeight);
            var secondRect = new Rect(titleRect.x, titleRect.yMax, titleRect.width, EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(titleRect, NameOf(scenario), EditorStyles.boldLabel);
            DrawPlayState(titleRect, scenario);
            EditorGUI.LabelField(secondRect, EventsTextOf(scenario), SecondLineStyle);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                scenarioClicked(scenario);
                Event.current.Use();
            }
        }

        private static void DrawPlayState(Rect titleRect, Scenario scenario)
        {
            if (!Application.isPlaying)
                return;

            var text = scenario.IsRunning ? $"▶ шаг {scenario.CurrentActionIndex + 1}" : scenario.HasRun ? "✓ был" : "";

            if (text.Length == 0)
                return;

            var previousColor = GUI.contentColor;
            GUI.contentColor = scenario.IsRunning ? RunningColor : previousColor;
            EditorGUI.LabelField(titleRect, text, PlayStateStyle);
            GUI.contentColor = previousColor;
        }

        // Несколько сценариев на одном объекте различаются номером.
        private static string NameOf(Scenario scenario)
        {
            var siblings = scenario.GetComponents<Scenario>();

            return siblings.Length > 1 ? $"{scenario.name} #{Array.IndexOf(siblings, scenario) + 1}" : scenario.name;
        }

        private static string EventsTextOf(Scenario scenario)
        {
            var titles = scenario.Events.Where(scenarioEvent => scenarioEvent != null)
                .Select(scenarioEvent => string.IsNullOrEmpty(scenarioEvent.Summary)
                    ? ScenarioBlockCatalog.TitleOf(scenarioEvent.GetType())
                    : $"{ScenarioBlockCatalog.TitleOf(scenarioEvent.GetType())} ({scenarioEvent.Summary})");

            var text = string.Join(", ", titles);

            return text.Length > 0 ? text : "без событий";
        }


        // ── Поиск сценариев ──────────────────────────────────────────────

        // Через корни сцен, а не FindObjectsByType: так попадают и сценарии на выключенных объектах.
        private void Collect()
        {
            scenarios.Clear();

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

            if (prefabStage != null)
            {
                scenarios.AddRange(prefabStage.prefabContentsRoot.GetComponentsInChildren<Scenario>(true));
            }
            else
            {
                for (var index = 0; index < SceneManager.sceneCount; index++)
                {
                    var scene = SceneManager.GetSceneAt(index);

                    if (!scene.isLoaded)
                        continue;

                    foreach (var root in scene.GetRootGameObjects())
                        scenarios.AddRange(root.GetComponentsInChildren<Scenario>(true));
                }
            }

            scenarios.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.CurrentCultureIgnoreCase));
            isStale = false;
        }


        // ── Создание ─────────────────────────────────────────────────────

        public void ShowCreateMenu(Rect buttonRect)
        {
            var menu = new GenericMenu();
            var selectedObject = Selection.activeGameObject;
            var canAddToSelected = selectedObject != null && !EditorUtility.IsPersistent(selectedObject);

            menu.AddItem(new GUIContent("Новый объект со сценарием"), false, CreateScenarioObject);

            if (canAddToSelected)
                menu.AddItem(new GUIContent($"На выделенном объекте ({selectedObject.name})"), false,
                    () => Pick(Undo.AddComponent<Scenario>(selectedObject)));
            else
                menu.AddDisabledItem(new GUIContent("На выделенном объекте"));

            menu.DropDown(buttonRect);
        }

        private void CreateScenarioObject()
        {
            var scenarioObject = new GameObject("Scenario");
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

            if (prefabStage != null)
            {
                StageUtility.PlaceGameObjectInCurrentStage(scenarioObject);
                scenarioObject.transform.SetParent(prefabStage.prefabContentsRoot.transform, false);
            }
            else if (SceneView.lastActiveSceneView != null)
                scenarioObject.transform.position = SceneView.lastActiveSceneView.pivot;

            Undo.RegisterCreatedObjectUndo(scenarioObject, "Новый сценарий");
            Pick(Undo.AddComponent<Scenario>(scenarioObject));
        }

        private void Pick(Scenario scenario)
        {
            MarkStale();
            Selection.activeGameObject = scenario.gameObject;
            scenarioClicked(scenario);
        }
    }
}
