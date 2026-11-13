using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Шапка окна графа — часть ScenarioGraphWindow: что открыто и есть ли несохранённое.
    /// - крошки с иконками: сцена или префаб ▸ объект ▸ Scenario, или граф-файл ▸ via объект; клик выделяет и пингует
    /// - Lock, Frame All, Select, Save; места под «Make Graph File» и «Embed in Object» (этап 2)
    /// - полосы под шапкой: Play Mode и «граф-файл на N объектах»
    /// </summary>
    internal class ScenarioGraphToolbar
    {
        private const string WindowTitle = "Scenario Graph";
        private const string DirtyMark = " •";
        private const string StageTwoHint = "Stage 2";
        private const string FindUsagesMenuItem = "Assets/Find Usages";

        private readonly ScenarioGraphWindow window;
        private readonly ScenarioGraphView graphView;
        private readonly VisualElement crumbs;
        private readonly Label dirtyMark;
        private readonly Image crumbsLock;
        private readonly Image lockIcon;
        private readonly ToolbarButton selectButton;
        private readonly ToolbarButton saveButton;
        private readonly VisualElement playModeBar;
        private readonly VisualElement sharedFileBar;
        private readonly Label sharedFileText;
        private ScenarioGraphHost shownHost;


        public ScenarioGraphToolbar(ScenarioGraphWindow window, ScenarioGraphView graphView)
        {
            this.window = window;
            this.graphView = graphView;

            Root = new VisualElement();

            var toolbar = new Toolbar();
            toolbar.AddToClassList("scenario-toolbar");

            crumbs = new VisualElement();
            crumbs.AddToClassList("scenario-toolbar__crumbs");
            toolbar.Add(crumbs);

            dirtyMark = new Label("•") { tooltip = "Unsaved changes" };
            dirtyMark.AddToClassList("scenario-toolbar__dirty");
            crumbsLock = new Image { image = ScenarioIcons.Lock(true), tooltip = "Locked" };
            crumbsLock.AddToClassList("scenario-toolbar__icon");

            toolbar.Add(new ToolbarSpacer { flex = true });

            var lockToggle = new ToolbarToggle { value = window.IsLocked, tooltip = "Locked — keeps this graph when selection changes" };
            lockIcon = new Image { image = ScenarioIcons.Lock(window.IsLocked) };
            lockIcon.AddToClassList("scenario-toolbar__icon");
            lockToggle.Add(lockIcon);
            lockToggle.RegisterValueChangedCallback(change => SetLocked(change.newValue));
            toolbar.Add(lockToggle);

            toolbar.Add(IconButton(ScenarioIcons.Frame, "Frame All", "Show the whole graph (A)", graphView.Navigator.FrameWholeGraph));
            selectButton = IconButton(ScenarioIcons.Select, "Select", "", SelectOwner);
            toolbar.Add(selectButton);
            saveButton = IconButton(ScenarioIcons.Save, "Save", "Save the scene, prefab or graph file that holds the graph", () => graphView.Host?.Save());
            toolbar.Add(saveButton);
            toolbar.Add(StageTwoButton("Make Graph File"));
            toolbar.Add(StageTwoButton("Embed in Object"));

            Root.Add(toolbar);

            playModeBar = Bar("scenario-bar--play", "Play Mode: changes to the graph will be lost when you stop the game");
            Root.Add(playModeBar);

            sharedFileBar = Bar("scenario-bar--shared", "");
            sharedFileText = sharedFileBar.Q<Label>();
            sharedFileBar.Add(new Button(FindAllUsers) { text = "Find All", tooltip = "Find every scene and prefab using this file" });
            Root.Add(sharedFileBar);
        }


        /// <summary>Шапка и полосы под ней — окно ставит это над холстом.</summary>
        public VisualElement Root { get; }


        // ── Обновить ─────────────────────────────────────────────────────

        /// <summary>Крошки — когда сменился граф; отметка правок, кнопки и полосы — каждый раз.</summary>
        public void Refresh()
        {
            var host = graphView.Host;

            if (host != shownHost)
            {
                shownHost = host;
                RebuildCrumbs(host);
            }

            var isDirty = host is { IsAlive: true } && host.IsDirty;

            dirtyMark.style.display = isDirty ? DisplayStyle.Flex : DisplayStyle.None;
            crumbsLock.style.display = window.IsLocked ? DisplayStyle.Flex : DisplayStyle.None;
            window.titleContent = new GUIContent(isDirty ? WindowTitle + DirtyMark : WindowTitle, ScenarioIcons.Flow);

            saveButton.SetEnabled(isDirty);
            selectButton.SetEnabled(host is { IsAlive: true });
            selectButton.tooltip = host is { IsAlive: true } ? $"Select {SelectedObjectOf(host).name}" : "";

            playModeBar.style.display = EditorApplication.isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshSharedFileBar(host);
        }

        private void RefreshSharedFileBar(ScenarioGraphHost host)
        {
            var userCount = host is { IsAlive: true, IsGraphFile: true } ? CountUsers((ScenarioGraphAsset)host.Owner) : 0;

            sharedFileText.text = $"This graph file is used by {userCount} objects in open scenes and prefabs — changes affect all of them";
            sharedFileBar.style.display = userCount > 1 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Только загруженное: открытые сцены и префабы. Весь проект — по кнопке «Find All».
        private static int CountUsers(ScenarioGraphAsset graphFile)
        {
            return Resources.FindObjectsOfTypeAll<Scenario>()
                .Count(scenario => scenario.GraphSource == ScenarioGraphSource.Asset && scenario.GraphAsset == graphFile
                                   && (scenario.gameObject.scene.IsValid() || EditorUtility.IsPersistent(scenario)));
        }

        private void FindAllUsers()
        {
            if (graphView.Host is not { IsAlive: true, IsGraphFile: true } host)
                return;

            // Поиск использований FeatherKit открыт только пунктом меню — зовём его, выделив файл.
            Selection.activeObject = host.Owner;
            EditorApplication.ExecuteMenuItem(FindUsagesMenuItem);
        }


        // ── Крошки ───────────────────────────────────────────────────────

        private void RebuildCrumbs(ScenarioGraphHost host)
        {
            crumbs.Clear();

            if (host is not { IsAlive: true })
            {
                crumbs.Add(new Label("No graph — select a Scenario or a Scenario Graph file"));
                return;
            }

            if (host.IsGraphFile)
                AddGraphFileCrumbs(host);
            else
                AddEmbeddedCrumbs(host.Scenario);

            crumbs.Add(dirtyMark);
            crumbs.Add(crumbsLock);
        }

        private void AddGraphFileCrumbs(ScenarioGraphHost host)
        {
            AddCrumb(ScenarioIcons.GraphFile, AssetDatabase.GetAssetPath(host.Owner), "Show the graph file in the Project",
                () => Ping(host.Owner));

            if (host.Scenario != null)
                AddCrumb(ScenarioIcons.TypeIcon(typeof(GameObject)), $"via {host.Scenario.name}", "Select the object this file is opened through",
                    () => SelectAndPing(host.Scenario.gameObject));
        }

        private void AddEmbeddedCrumbs(Scenario scenario)
        {
            var gameObject = scenario.gameObject;
            var prefabStage = PrefabStageUtility.GetPrefabStage(gameObject);

            if (prefabStage != null)
                AddCrumb(ScenarioIcons.Prefab, $"Prefab: {Path.GetFileNameWithoutExtension(prefabStage.assetPath)}", "Show the prefab in the Project",
                    () => Ping(AssetDatabase.LoadAssetAtPath<GameObject>(prefabStage.assetPath)));
            else if (EditorUtility.IsPersistent(gameObject))
                AddCrumb(ScenarioIcons.Prefab, AssetDatabase.GetAssetPath(gameObject), "Show the prefab in the Project", () => Ping(gameObject));
            else
                AddCrumb(ScenarioIcons.Scene, gameObject.scene.name, "Show the scene in the Project",
                    () => Ping(AssetDatabase.LoadAssetAtPath<SceneAsset>(gameObject.scene.path)));

            AddCrumb(ScenarioIcons.TypeIcon(typeof(GameObject)), HierarchyPathOf(gameObject.transform), "Select the object",
                () => SelectAndPing(gameObject));
            AddCrumb(ScenarioIcons.TypeIcon(typeof(Scenario)), "Scenario", "Select the object", () => SelectAndPing(gameObject));
        }

        private void AddCrumb(Texture icon, string text, string tooltip, Action clicked)
        {
            if (crumbs.childCount > 0)
                crumbs.Add(new Label("▸") { pickingMode = PickingMode.Ignore });

            crumbs.Add(IconButton(icon, text, tooltip, clicked));
        }

        private static string HierarchyPathOf(Transform transform)
        {
            var names = new List<string>();

            for (var current = transform; current != null; current = current.parent)
                names.Insert(0, current.name);

            return string.Join("/", names);
        }


        // ── Действия ─────────────────────────────────────────────────────

        private void SetLocked(bool isLocked)
        {
            window.IsLocked = isLocked;
            lockIcon.image = ScenarioIcons.Lock(isLocked);
            Refresh();
        }

        private void SelectOwner()
        {
            if (graphView.Host is { IsAlive: true } host)
                SelectAndPing(SelectedObjectOf(host));
        }

        private static Object SelectedObjectOf(ScenarioGraphHost host)
        {
            return host.Scenario != null ? host.Scenario.gameObject : host.Owner;
        }

        private static void SelectAndPing(Object target)
        {
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private static void Ping(Object target)
        {
            if (target != null)
                EditorGUIUtility.PingObject(target);
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private static ToolbarButton IconButton(Texture icon, string text, string tooltip, Action clicked)
        {
            var button = new ToolbarButton(clicked) { tooltip = tooltip };
            button.AddToClassList("scenario-toolbar__button");

            var image = new Image { image = icon };
            image.AddToClassList("scenario-toolbar__icon");

            button.Add(image);
            button.Add(new Label(text));

            return button;
        }

        private static ToolbarButton StageTwoButton(string text)
        {
            var button = IconButton(null, text, $"{text} — {StageTwoHint}", () => { });
            button.SetEnabled(false);

            return button;
        }

        private static VisualElement Bar(string modifierClass, string text)
        {
            var bar = new VisualElement();
            bar.AddToClassList("scenario-bar");
            bar.AddToClassList(modifierClass);
            bar.Add(new Label(text));

            return bar;
        }
    }
}
