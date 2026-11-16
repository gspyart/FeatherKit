using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Окно графа сценария: шапка (ScenarioGraphToolbar), холст с панелью переменных, инспектор выделенной ноды.
    /// - открывается из меню, кнопкой «Open Graph» в инспекторе и двойным кликом по граф-файлу
    /// - идёт за выделением, пока не нажат «Lock»; следит, не поменяли ли граф в обход холста
    /// </summary>
    public class ScenarioGraphWindow : EditorWindow
    {
        private const long DataCheckIntervalMilliseconds = 500;
        private const float InspectorWidth = 330f;

        // Сериализуется вместе с окном — граф переоткрывается после перезагрузки домена.
        [SerializeField] private Object openedObject;
        [SerializeField] private bool isLocked;

        private ScenarioGraphView graphView;
        private ScenarioNodeInspectorPanel inspectorPanel;
        private ScenarioGraphToolbar toolbar;
        private VisualElement emptyHint;


        /// <summary>Окно держит открытый граф, когда меняется выделение.</summary>
        public bool IsLocked
        {
            get => isLocked;
            set => isLocked = value;
        }


        // ── Открыть ──────────────────────────────────────────────────────

        [MenuItem("FeatherKit/Scenarios/Graph")]
        public static void OpenFromMenu()
        {
            Open(Selection.activeObject);
        }

        /// <summary>Открыть граф сценария или граф-файла. Объект без графа — пустое окно с подсказкой.</summary>
        public static void Open(Object scenarioOrGraphFile)
        {
            var window = GetWindow<ScenarioGraphWindow>();
            window.titleContent = new GUIContent("Scenario Graph");

            if (ScenarioGraphHost.GraphOwnerOf(scenarioOrGraphFile) != null)
                window.ShowGraphOf(scenarioOrGraphFile);

            window.Show();
        }


#if UNITY_6000_2_OR_NEWER
        [OnOpenAsset]
        private static bool OpenGraphFile(EntityId entityId, int line) => TryOpenGraphFile(EditorUtility.EntityIdToObject(entityId));
#else
        [OnOpenAsset]
        private static bool OpenGraphFile(int instanceId, int line) => TryOpenGraphFile(EditorUtility.InstanceIDToObject(instanceId));
#endif

        private static bool TryOpenGraphFile(Object asset)
        {
            if (asset is not ScenarioGraphAsset)
                return false;

            Open(asset);

            return true;
        }


        private void ShowGraphOf(Object scenarioOrGraphFile)
        {
            openedObject = scenarioOrGraphFile is GameObject gameObject ? (Object)gameObject.GetComponent<Scenario>() : scenarioOrGraphFile;

            if (graphView != null)
                ShowHost(ScenarioGraphHost.For(openedObject));
        }

        /// <summary>Граф объекта переехал (в файл или обратно) — показать, где он теперь, не дожидаясь проверки.</summary>
        internal void ShowCurrentGraph()
        {
            ShowHost(ScenarioGraphHost.For(openedObject));
        }

        private void ShowHost(ScenarioGraphHost host)
        {
            graphView.Show(host);
            toolbar.Refresh();
            emptyHint.style.display = host != null ? DisplayStyle.None : DisplayStyle.Flex;
        }


        // ── Окно ─────────────────────────────────────────────────────────

        private void CreateGUI()
        {
            ScenarioGraphView.ApplyStyle(rootVisualElement);

            var split = new TwoPaneSplitView(1, InspectorWidth, TwoPaneSplitViewOrientation.Horizontal) { style = { flexGrow = 1f } };

            var canvasArea = new VisualElement { style = { flexGrow = 1f } };
            graphView = new ScenarioGraphView(this);
            canvasArea.Add(graphView);

            emptyHint = new Label("Select a Scenario or a Scenario Graph file and press “Open Graph”.") { pickingMode = PickingMode.Ignore };
            emptyHint.AddToClassList("scenario-window__empty-hint");
            canvasArea.Add(emptyHint);

            inspectorPanel = new ScenarioNodeInspectorPanel();
            graphView.SelectedNodeChanged += nodeView => inspectorPanel.Show(graphView, nodeView);

            toolbar = new ScenarioGraphToolbar(this, graphView);
            rootVisualElement.Add(toolbar.Root);

            split.Add(canvasArea);
            split.Add(inspectorPanel);
            rootVisualElement.Add(split);

            ShowHost(ScenarioGraphHost.For(openedObject));
            rootVisualElement.schedule.Execute(CheckGraphStillShown).Every(DataCheckIntervalMilliseconds);
        }


        // ── Слежение ─────────────────────────────────────────────────────

        // Сценарий сменил источник графа, объект удалили, граф поменяли в обход холста.
        private void CheckGraphStillShown()
        {
            var currentOwner = ScenarioGraphHost.GraphOwnerOf(openedObject);
            var shownOwner = graphView.Host?.Owner;

            if (currentOwner != shownOwner)
                ShowHost(ScenarioGraphHost.For(openedObject));
            else
                graphView.RebuildIfDataChanged();

            toolbar.Refresh();
        }

        private void HandleUndoRedo()
        {
            graphView?.Rebuild();
        }


        // ── Unity ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            graphView?.Dispose();
        }

        private void OnSelectionChange()
        {
            var selected = Selection.activeObject;

            if (isLocked || ScenarioGraphHost.GraphOwnerOf(selected) == null)
                return;

            var selectedScenarioOrFile = selected is GameObject gameObject ? (Object)gameObject.GetComponent<Scenario>() : selected;

            if (selectedScenarioOrFile != openedObject)
                ShowGraphOf(selectedScenarioOrFile);
        }
    }
}
