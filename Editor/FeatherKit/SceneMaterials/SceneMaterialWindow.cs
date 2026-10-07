using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Окно материалов сцены. Сверху две вкладки: «Выделение» — материалы выделенных объектов
    /// по группам и что с ними сделать, «Сцена» — все сценовые копии открытых сцен. Снизу —
    /// настройка выбранного материала, высота делится перетаскиваемой чертой.
    ///
    /// Сверху вниз, а не двумя колонками: окна инструментов живут пристыкованными к краю
    /// редактора, и узкая высокая колонка там удобнее двух половин.
    ///
    /// Окно только раскладывает части и решает, когда пересобрать данные: вкладки —
    /// <see cref="SelectionMaterialsPanel"/> и <see cref="SceneMaterialsPanel"/>, настройка —
    /// <see cref="InspectedMaterialPanel"/>, операции — <see cref="SceneMaterialTools"/>.
    /// </summary>
    public class SceneMaterialWindow : EditorWindow
    {
        private const string TabKey = "FeatherKit.SceneMaterials.Tab";
        private const string InspectorHeightKey = "FeatherKit.SceneMaterials.InspectorHeight";
        private const float SplitterHeight = 6f;
        private const float ToolbarHeight = 21f;
        private const float MinListHeight = 140f;
        private const float MinInspectorHeight = 80f;
        private const float DefaultInspectorHeight = 320f;
        private const int SelectionTab = 0;

        private static readonly Color SplitterColor = new Color(0f, 0f, 0f, 0.3f);

        private readonly SceneMaterialUsage usage = new SceneMaterialUsage();
        private readonly MaterialPreviewCache previews = new MaterialPreviewCache();

        private InspectedMaterialPanel inspected;
        private SelectionMaterialsPanel selection;
        private SceneMaterialsPanel scene;

        private int tab;
        private float inspectorHeight;
        private bool needsRefresh = true;


        [MenuItem("FeatherKit/Материалы сцены")]
        public static void Open()
        {
            var window = GetWindow<SceneMaterialWindow>();
            window.titleContent = new GUIContent("Материалы сцены", EditorGUIUtility.IconContent("Material Icon").image);
            window.minSize = new Vector2(300f, MinListHeight + MinInspectorHeight + ToolbarHeight + SplitterHeight);
        }


        // ── Раскладка ────────────────────────────────────

        // Части окна — явными областями, а не общей раскладкой: инспектор материала просит
        // столько высоты, сколько у него свойств, и в общей раскладке выдавливал бы список.
        private void DrawParts()
        {
            var width = position.width;
            var inspectorTop = position.height - ClampInspectorHeight(inspectorHeight);
            var splitterTop = inspectorTop - SplitterHeight;

            GUILayout.BeginArea(new Rect(0f, 0f, width, ToolbarHeight));
            DrawToolbar();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(0f, ToolbarHeight, width, splitterTop - ToolbarHeight));
            DrawTab();
            GUILayout.EndArea();

            DrawSplitter(new Rect(0f, splitterTop, width, SplitterHeight));

            inspected.Draw(new Rect(0f, inspectorTop, width, position.height - inspectorTop));
        }


        private void DrawTab()
        {
            if (EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Идёт игра: всё, что сделаешь здесь, пропадёт при выходе из неё.", MessageType.Warning);

            if (tab == SelectionTab)
                selection.Draw();
            else
                scene.Draw();
        }


        // ── Полоса вкладок ───────────────────────────────

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var titles = new[]
            {
                new GUIContent($"Выделение · {selection.GroupCount}", "Материалы выделенных объектов, собранные по группам"),
                new GUIContent($"Сцена · {usage.SceneMaterials.Count}", "Все сценовые копии открытых сцен")
            };

            var chosen = GUILayout.Toolbar(tab, titles, EditorStyles.toolbarButton, GUI.ToolbarButtonSize.FitToContents);

            if (chosen != tab)
            {
                tab = chosen;
                EditorPrefs.SetInt(TabKey, tab);
            }

            GUILayout.FlexibleSpace();

            if (tab == SelectionTab)
                selection.DrawToolbarControls();
            else
                scene.DrawToolbarControls();

            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Refresh").image, "Пересобрать списки"), EditorStyles.toolbarButton, GUILayout.Width(26f)))
                RequestRefresh();

            EditorGUILayout.EndHorizontal();
        }


        // ── Черта между списком и настройкой ─────────────

        private void DrawSplitter(Rect rect)
        {
            var id = GUIUtility.GetControlID(FocusType.Passive, rect);
            var current = Event.current;

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);

            switch (current.GetTypeForControl(id))
            {
                case EventType.Repaint:
                    EditorGUI.DrawRect(new Rect(rect.x, rect.center.y - 0.5f, rect.width, 1f), SplitterColor);
                    break;

                case EventType.MouseDown when current.button == 0 && rect.Contains(current.mousePosition):
                    GUIUtility.hotControl = id;
                    current.Use();
                    break;

                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    inspectorHeight = ClampInspectorHeight(inspectorHeight - current.delta.y);
                    current.Use();
                    Repaint();
                    break;

                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    EditorPrefs.SetFloat(InspectorHeightKey, inspectorHeight);
                    current.Use();
                    break;
            }
        }

        // Список не должен схлопываться, как бы низко ни опустили черту, и настройка — тоже.
        private float ClampInspectorHeight(float height)
        {
            var max = position.height - ToolbarHeight - SplitterHeight - MinListHeight;
            return Mathf.Clamp(height, MinInspectorHeight, Mathf.Max(MinInspectorHeight, max));
        }


        // ── Данные ───────────────────────────────────────

        private void RequestRefresh()
        {
            needsRefresh = true;
            Repaint();
        }


        private void Refresh()
        {
            needsRefresh = false;

            usage.Refresh();
            selection.Refresh();
            previews.ForgetDestroyed();
        }


        // Правка свойств самого ассета не меняет, кто что носит, — её пропускаем: иначе каждый
        // шаг ползунка в настройке материала пересобирал бы всю сцену.
        private void OnObjectChangesPublished(ref ObjectChangeEventStream stream)
        {
            for (var index = 0; index < stream.length; index++)
            {
                if (stream.GetEventType(index) == ObjectChangeKind.ChangeAssetObjectProperties)
                    continue;

                RequestRefresh();
                return;
            }
        }


        // ── Unity-события ────────────────────────────────

        private void OnEnable()
        {
            wantsMouseMove = true;

            inspected = new InspectedMaterialPanel(usage, previews, RequestRefresh, Repaint);
            selection = new SelectionMaterialsPanel(usage, previews, inspected, RequestRefresh);
            scene = new SceneMaterialsPanel(usage, previews, inspected, selection, RequestRefresh);

            tab = EditorPrefs.GetInt(TabKey, SelectionTab);
            inspectorHeight = EditorPrefs.GetFloat(InspectorHeightKey, DefaultInspectorHeight);
            needsRefresh = true;

            Undo.undoRedoPerformed += RequestRefresh;
            ObjectChangeEvents.changesPublished += OnObjectChangesPublished;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RequestRefresh;
            ObjectChangeEvents.changesPublished -= OnObjectChangesPublished;

            inspected.Clear();
            previews.Clear();
        }

        private void OnGUI()
        {
            var current = Event.current;

            // Пересобираем только на раскладке: в остальных событиях Unity ждёт тех же строк, что уже разложены.
            if (needsRefresh && current.type == EventType.Layout)
                Refresh();

            if (current.type == EventType.Repaint)
                previews.BeginRepaint();

            DrawParts();

            if (current.type == EventType.Repaint && previews.HasPendingRenders)
                Repaint();
        }

        private void OnSelectionChange()
        {
            selection.ResetCopyName();
            RequestRefresh();
        }

        private void OnHierarchyChange() => RequestRefresh();
    }
}
