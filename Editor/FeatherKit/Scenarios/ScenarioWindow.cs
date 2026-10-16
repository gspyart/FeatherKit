using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Окно сценариев: слева все сценарии сцены, справа выбранный на всю ширину, снизу журнал запусков.
    /// - следит за выделением в иерархии; «Закрепить» оставляет текущий сценарий
    /// - разделы рисует тот же ScenarioSectionsDrawer, что и инспектор, — правки видны в обоих
    /// </summary>
    public class ScenarioWindow : EditorWindow
    {
        private const string ListWidthPrefsKey = "FeatherKit.ScenarioWindow.ListWidth";
        private const float DefaultListWidth = 240f;
        private const float MinListWidth = 160f;
        private const float MaxListWidth = 480f;
        private const float SplitterWidth = 4f;
        private const float LogHeight = 170f;
        private const float EditorLabelWidth = 190f;

        private static readonly Color SplitterColor = new Color(0f, 0f, 0f, 0.3f);

        // Сериализуются вместе с окном — переживают перезагрузку домена.
        [SerializeField] private Scenario selected;
        [SerializeField] private bool isLocked;
        [SerializeField] private bool showsLog = true;

        private ScenarioSceneList sceneList;
        private ScenarioRunLogPanel logPanel;
        private ScenarioSectionsDrawer sections;
        private Vector2 editorScroll;
        private float listWidth;
        private bool isDraggingSplitter;


        // ── Открыть и выбрать ────────────────────────────────────────────

        [MenuItem("FeatherKit/Сценарии")]
        public static void OpenFromMenu()
        {
            Open(null);
        }

        public static void Open(Scenario scenario)
        {
            var window = GetWindow<ScenarioWindow>();
            window.titleContent = new GUIContent("Сценарии");

            if (scenario != null)
                window.Select(scenario);

            window.Show();
        }


        private void Select(Scenario scenario)
        {
            selected = scenario;
            sections = scenario != null ? new ScenarioSectionsDrawer(new SerializedObject(scenario), showsListTools: true) : null;
            editorScroll = Vector2.zero;

            Repaint();
        }

        private void SelectFromList(Scenario scenario)
        {
            Select(scenario);
            Selection.activeGameObject = scenario.gameObject;
        }


        // ── Отрисовка ────────────────────────────────────────────────────

        private void OnGUI()
        {
            // Сценарий удалили или окно пережило перезагрузку домена.
            if (selected == null && sections != null)
                Select(null);
            else if (selected != null && sections == null)
                Select(selected);

            DrawToolbar();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(listWidth)))
                    sceneList.Draw(selected);

                DrawSplitter();

                using (new EditorGUILayout.VerticalScope())
                    DrawSelected();
            }

            if (showsLog)
                logPanel.Draw(selected, LogHeight);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var createRect = GUILayoutUtility.GetRect(new GUIContent("+ Сценарий"), EditorStyles.toolbarDropDown);

                if (GUI.Button(createRect, "+ Сценарий", EditorStyles.toolbarDropDown))
                    sceneList.ShowCreateMenu(createRect);

                GUILayout.FlexibleSpace();

                showsLog = GUILayout.Toggle(showsLog, "Журнал", EditorStyles.toolbarButton);
                isLocked = GUILayout.Toggle(isLocked, new GUIContent("Закрепить", "Не переключаться на сценарий, выделенный в иерархии"),
                    EditorStyles.toolbarButton);
            }
        }

        private void DrawSelected()
        {
            if (selected == null)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Выберите сценарий слева\nили объект со сценарием в иерархии",
                    EditorStyles.centeredGreyMiniLabel, GUILayout.Height(40));
                GUILayout.FlexibleSpace();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(selected.name, EditorStyles.largeLabel);

                if (GUILayout.Button("Выделить в сцене", GUILayout.Width(130)))
                {
                    Selection.activeGameObject = selected.gameObject;
                    EditorGUIUtility.PingObject(selected.gameObject);
                }
            }

            editorScroll = EditorGUILayout.BeginScrollView(editorScroll);

            var previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = EditorLabelWidth;
            sections.Draw();
            EditorGUIUtility.labelWidth = previousLabelWidth;

            EditorGUILayout.EndScrollView();
        }

        private void DrawSplitter()
        {
            var rect = GUILayoutUtility.GetRect(SplitterWidth, SplitterWidth, 0f, float.MaxValue,
                GUILayout.ExpandHeight(true));

            EditorGUI.DrawRect(new Rect(rect.center.x - 0.5f, rect.y, 1f, rect.height), SplitterColor);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

            var current = Event.current;

            if (current.type == EventType.MouseDown && rect.Contains(current.mousePosition))
            {
                isDraggingSplitter = true;
                current.Use();
            }
            else if (isDraggingSplitter && current.type == EventType.MouseDrag)
            {
                listWidth = Mathf.Clamp(listWidth + current.delta.x, MinListWidth, MaxListWidth);
                current.Use();
                Repaint();
            }
            else if (isDraggingSplitter && current.type == EventType.MouseUp)
            {
                isDraggingSplitter = false;
                EditorPrefs.SetFloat(ListWidthPrefsKey, listWidth);
                current.Use();
            }
        }


        // ── Что происходит вокруг ────────────────────────────────────────

        private void HandleHierarchyChanged()
        {
            sceneList.MarkStale();
            Repaint();
        }

        private void HandlePrefabStageChanged(PrefabStage _)
        {
            HandleHierarchyChanged();
        }


        // ── Unity ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            sceneList = new ScenarioSceneList(SelectFromList);
            logPanel = new ScenarioRunLogPanel(SelectFromList);
            listWidth = EditorPrefs.GetFloat(ListWidthPrefsKey, DefaultListWidth);

            EditorApplication.hierarchyChanged += HandleHierarchyChanged;
            PrefabStage.prefabStageOpened += HandlePrefabStageChanged;
            PrefabStage.prefabStageClosing += HandlePrefabStageChanged;
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
            PrefabStage.prefabStageOpened -= HandlePrefabStageChanged;
            PrefabStage.prefabStageClosing -= HandlePrefabStageChanged;
            Undo.undoRedoPerformed -= Repaint;
        }

        private void OnSelectionChange()
        {
            var selectedObject = Selection.activeGameObject;

            if (isLocked || selectedObject == null || (selected != null && selected.gameObject == selectedObject))
                return;

            var scenario = selectedObject.GetComponent<Scenario>();

            if (scenario != null)
                Select(scenario);
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }
    }
}
