using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Окно «Префабы в варианты»: выбираешь шаблон и префабы, заранее видишь, что с чем
    /// совпадёт, и одной кнопкой делаешь их вариантами шаблона.
    ///
    /// Само окно ничего не переделывает: предпросмотр — <see cref="PrefabToVariantConverter"/>,
    /// запуск — <see cref="PrefabToVariantBatch"/>. Префабы помнит по GUID, а не ссылкой:
    /// переделанный префаб Unity перечитывает, и прежняя ссылка на него пустеет.
    /// </summary>
    public class PrefabToVariantWindow : EditorWindow
    {
        private const string WindowTitle = "Префабы в варианты";
        private const string RemoveTemplateExtrasKey = "FeatherKit.PrefabToVariant.RemoveTemplateExtras";
        private const string RunResultKeyPrefix = "run:";
        private const float KindColumnWidth = 190f;
        private const float IconButtonWidth = 22f;
        private const int PreviewProgressThreshold = 8;

        private static readonly Color LostColor = new Color(1f, 0.45f, 0.4f);
        private static readonly Color TemplateExtraColor = new Color(0.6f, 0.6f, 0.6f);

        [SerializeField] private GameObject template;
        [SerializeField] private List<string> prefabGuids = new List<string>();

        private readonly Dictionary<string, PrefabConversionResult> previews = new Dictionary<string, PrefabConversionResult>();
        private readonly HashSet<string> expandedRows = new HashSet<string>();

        private List<PrefabConversionResult> lastRunResults;
        private string lastRunRefusal;
        private bool isLastRunExpanded = true;
        private bool removeTemplateExtras;
        private bool needsFullPreview = true;
        private Vector2 scroll;
        private GUIStyle summaryStyle;


        private bool HasValidTemplate => PrefabToVariantConverter.CanBeTemplate(template, out _);

        private List<string> ReadyPrefabGuids =>
            prefabGuids.Where(guid => previews.TryGetValue(guid, out var preview) && preview.Status == PrefabConversionStatus.Ready).ToList();


        // ── Открытие ─────────────────────────────────

        [MenuItem("FeatherKit/Префабы в варианты")]
        public static void Open() => GetOrCreateWindow();


        // Дублируем в контекстное меню Project: пачку удобнее собрать выделением.
        [MenuItem("Assets/Сделать вариантами шаблона…", false, 31)]
        private static void OpenForSelection() => GetOrCreateWindow().ReplacePrefabs(FindSelectedPrefabGuids());


        [MenuItem("Assets/Сделать вариантами шаблона…", true)]
        private static bool CanOpenForSelection() => FindSelectedPrefabGuids().Count > 0;


        private static PrefabToVariantWindow GetOrCreateWindow()
        {
            var window = GetWindow<PrefabToVariantWindow>();
            window.titleContent = new GUIContent(WindowTitle, EditorGUIUtility.IconContent("PrefabVariant Icon").image);
            window.minSize = new Vector2(380f, 320f);
            return window;
        }


        // ── Список префабов ──────────────────────────

        // Выделенная папка отдаёт все префабы внутри: пачку так собирают одним кликом.
        private static List<string> FindSelectedPrefabGuids() =>
            ToPrefabGuids(Selection.GetFiltered<GameObject>(SelectionMode.Assets | SelectionMode.DeepAssets));


        private static List<string> FindDraggedPrefabGuids()
        {
            var prefabs = DragAndDrop.objectReferences.OfType<GameObject>().ToList();

            foreach (var path in DragAndDrop.paths.Where(AssetDatabase.IsValidFolder))
            {
                prefabs.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { path })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid))));
            }

            return ToPrefabGuids(prefabs);
        }


        private void ReplacePrefabs(IEnumerable<string> guids)
        {
            prefabGuids.Clear();
            AddPrefabs(guids);
        }


        private void AddPrefabs(IEnumerable<string> guids)
        {
            foreach (var guid in guids)
            {
                if (!prefabGuids.Contains(guid))
                    prefabGuids.Add(guid);
            }

            Repaint();
        }


        private void RemovePrefab(string guid)
        {
            prefabGuids.Remove(guid);
            previews.Remove(guid);
            expandedRows.Remove(guid);
        }


        private static List<string> ToPrefabGuids(IEnumerable<GameObject> gameObjects) =>
            gameObjects
                .Where(PrefabToVariantConverter.IsPrefabAssetRoot)
                .Select(gameObject => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(gameObject)))
                .Distinct()
                .ToList();


        // ── Предпросмотр ─────────────────────────────

        // Считаем только то, чего ещё нет: добавленный в список префаб не должен
        // пересчитывать соседей. Сменился шаблон или галка — пересчитываются все.
        private void RefreshPreviews()
        {
            if (needsFullPreview)
                previews.Clear();

            needsFullPreview = false;

            var missing = prefabGuids.Where(guid => !previews.ContainsKey(guid)).ToList();
            if (missing.Count == 0)
                return;

            var showProgress = missing.Count >= PreviewProgressThreshold;

            try
            {
                for (var index = 0; index < missing.Count; index++)
                {
                    if (showProgress)
                        EditorUtility.DisplayProgressBar(WindowTitle, "Предпросмотр: " + NameOf(missing[index]), (float)index / missing.Count);

                    previews[missing[index]] = BuildPreview(missing[index]);
                }
            }
            finally
            {
                if (showProgress)
                    EditorUtility.ClearProgressBar();
            }
        }

        private PrefabConversionResult BuildPreview(string guid)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));

            using (var converter = new PrefabToVariantConverter(prefab, template, removeTemplateExtras))
                return converter.Convert();
        }


        // ── Запуск ───────────────────────────────────

        // Зовётся отложенно, а не из кнопки: пачка перезагружает сцены и показывает диалоги,
        // и посреди отрисовки окна это ломает раскладку.
        private void RunBatch()
        {
            var readyGuids = ReadyPrefabGuids;
            if (readyGuids.Count == 0 || !HasValidTemplate)
                return;

            var message = $"Префабов: {readyGuids.Count}. Каждый станет вариантом «{template.name}», а ссылки на него " +
                          "будут исправлены во всех сценах, префабах и конфигах проекта.\n\n" +
                          "Ctrl+Z это не отменит — убедись, что всё закоммичено.";

            if (!EditorUtility.DisplayDialog(WindowTitle, message, "Переделать", "Отмена"))
                return;

            lastRunResults = PrefabToVariantBatch.Run(readyGuids, template, removeTemplateExtras, out lastRunRefusal);
            isLastRunExpanded = true;
            needsFullPreview = true;
            Repaint();
        }


        // ── Раскладка ────────────────────────────────

        private void DrawTemplateSection()
        {
            EditorGUILayout.HelpBox(
                "Общие части перейдут в шаблон, своё останется поверх. Префабы выглядят и работают как раньше, " +
                "а ссылки на них исправляются по всему проекту.",
                MessageType.None);

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var picked = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Шаблон", "Префаб с общими частями — от него пойдут варианты"),
                    template, typeof(GameObject), false);

                removeTemplateExtras = EditorGUILayout.ToggleLeft(
                    new GUIContent("Убирать то, чего у префаба не было",
                        "Часть шаблона, которой у префаба не было, в варианте помечается удалённой — и префаб остаётся как был. " +
                        "Без галки префаб получит её из шаблона."),
                    removeTemplateExtras);

                if (check.changed)
                {
                    // Из Project можно вытащить и ребёнка внутри префаба — шаблоном всё равно будет корень.
                    template = picked != null ? picked.transform.root.gameObject : null;
                    EditorPrefs.SetBool(RemoveTemplateExtrasKey, removeTemplateExtras);
                    needsFullPreview = true;
                }
            }

            if (!PrefabToVariantConverter.CanBeTemplate(template, out var templateRefusal))
                EditorGUILayout.HelpBox(templateRefusal, template == null ? MessageType.Info : MessageType.Warning);

            if (!PrefabReferenceRemapper.IsProjectTextSerialized)
                EditorGUILayout.HelpBox(
                    "Проект хранит ассеты не текстом (Project Settings → Editor → Asset Serialization → Force Text). " +
                    "Без этого ссылки на переделанные префабы не починить.",
                    MessageType.Error);
        }


        private void DrawPrefabList()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"Префабы · {prefabGuids.Count}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("Добавить выделенные", "Префабы, выделенные в Project, и все префабы внутри выделенных папок"), EditorStyles.toolbarButton))
                    AddPrefabs(FindSelectedPrefabGuids());

                using (new EditorGUI.DisabledScope(prefabGuids.Count == 0))
                {
                    if (GUILayout.Button("Очистить", EditorStyles.toolbarButton))
                        ReplacePrefabs(Enumerable.Empty<string>());
                }

                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Refresh").image, "Пересобрать предпросмотр"), EditorStyles.toolbarButton, GUILayout.Width(26f)))
                    needsFullPreview = true;
            }

            if (prefabGuids.Count == 0)
            {
                EditorGUILayout.HelpBox("Перетащи сюда префабы или папки из Project — или выдели их и нажми «Добавить выделенные».", MessageType.None);
                return;
            }

            // Копией: строку можно убрать прямо из её же кнопки.
            foreach (var guid in prefabGuids.ToList())
                DrawPrefabRow(guid);
        }

        private void DrawPrefabRow(string guid)
        {
            previews.TryGetValue(guid, out var preview);

            var isExpanded = DrawRowHeader(guid, AssetDatabase.GUIDToAssetPath(guid), StatusIcon(preview), SummarizePreview(preview), out var isRemoveClicked);

            if (isRemoveClicked)
            {
                RemovePrefab(guid);
                return;
            }

            if (isExpanded && preview != null)
                DrawResultDetails(preview);
        }


        private void DrawRunSection()
        {
            var readyCount = ReadyPrefabGuids.Count;

            EditorGUILayout.Space(2f);

            if (readyCount > 0)
                EditorGUILayout.HelpBox("Ctrl+Z это не отменит: правятся файлы. Перед запуском закоммить всё в систему контроля версий.", MessageType.Warning);

            var canRun = readyCount > 0 && HasValidTemplate && PrefabReferenceRemapper.IsProjectTextSerialized && !EditorApplication.isPlaying;
            var title = readyCount > 0 ? $"Сделать вариантами «{template.name}» · {readyCount}" : "Сделать вариантами";

            using (new EditorGUI.DisabledScope(!canRun))
            {
                if (GUILayout.Button(title, GUILayout.Height(28f)))
                    EditorApplication.delayCall += RunBatch;
            }

            EditorGUILayout.Space(4f);
        }


        private void DrawLastRun()
        {
            if (lastRunResults == null)
                return;

            var convertedCount = lastRunResults.Count(result => result.Status == PrefabConversionStatus.Converted);
            var failedCount = lastRunResults.Count - convertedCount;

            EditorGUILayout.Space(6f);
            isLastRunExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(isLastRunExpanded, $"Итог запуска: сделано {convertedCount} · не вышло {failedCount}");

            if (isLastRunExpanded)
            {
                if (lastRunRefusal != null)
                    EditorGUILayout.HelpBox(lastRunRefusal, MessageType.Warning);

                foreach (var result in lastRunResults)
                    DrawRunResultRow(result);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawRunResultRow(PrefabConversionResult result)
        {
            var summary = result.Status == PrefabConversionStatus.Converted
                ? $"вариант · ссылки исправлены в файлах: {result.FixedFiles.Count}"
                : result.Reason;

            var isExpanded = DrawRowHeader(RunResultKeyPrefix + result.PrefabPath, result.PrefabPath, StatusIcon(result), summary, out _, canRemove: false);
            if (!isExpanded)
                return;

            DrawResultDetails(result);

            EditorGUI.indentLevel++;
            foreach (var path in result.FixedFiles)
                DrawPingableAssetLine(path);
            EditorGUI.indentLevel--;
        }

        private static void DrawPingableAssetLine(string path)
        {
            var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
            if (GUI.Button(rect, new GUIContent(" " + path, AssetDatabase.GetCachedIcon(path)), EditorStyles.label))
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(path));
        }


        // Бросить префабы можно в любое место окна. Поле шаблона Unity обслуживает
        // раньше, поэтому брошенное на него списка не касается.
        private void HandleDragAndDrop()
        {
            var current = Event.current;
            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)
                return;

            var guids = FindDraggedPrefabGuids();
            if (guids.Count == 0)
                return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                AddPrefabs(guids);
            }

            current.Use();
        }


        // ── Строка и её подробности ──────────────────

        // Шапка строки: раскрывашка с именем слева, сводка справа, кнопки «показать» и «убрать».
        private bool DrawRowHeader(string key, string assetPath, Texture icon, string summary, out bool isRemoveClicked, bool canRemove = true)
        {
            isRemoveClicked = false;
            var isExpanded = expandedRows.Contains(key);

            using (new EditorGUILayout.HorizontalScope())
            {
                var rect = EditorGUILayout.GetControlRect();
                var summaryContent = new GUIContent(summary ?? "", summary);
                var summaryWidth = Mathf.Min(summaryStyle.CalcSize(summaryContent).x, rect.width * 0.6f);
                var foldoutRect = new Rect(rect.x, rect.y, rect.width - summaryWidth - 4f, rect.height);
                var summaryRect = new Rect(rect.xMax - summaryWidth, rect.y, summaryWidth, rect.height);

                var name = string.IsNullOrEmpty(assetPath) ? "(файл удалён)" : Path.GetFileNameWithoutExtension(assetPath);
                var nowExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, new GUIContent(name, icon), true);
                EditorGUI.LabelField(summaryRect, summaryContent, summaryStyle);

                if (nowExpanded != isExpanded)
                {
                    if (nowExpanded)
                        expandedRows.Add(key);
                    else
                        expandedRows.Remove(key);

                    isExpanded = nowExpanded;
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(assetPath)))
                {
                    if (GUILayout.Button(new GUIContent(AssetDatabase.GetCachedIcon(assetPath), "Показать в Project"), EditorStyles.iconButton, GUILayout.Width(IconButtonWidth)))
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(assetPath));
                }

                if (canRemove && GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Toolbar Minus").image, "Убрать из списка"), EditorStyles.iconButton, GUILayout.Width(IconButtonWidth)))
                    isRemoveClicked = true;
            }

            return isExpanded;
        }


        private void DrawResultDetails(PrefabConversionResult result)
        {
            EditorGUI.indentLevel++;

            if (!string.IsNullOrEmpty(result.Reason))
                EditorGUILayout.HelpBox(result.Reason, result.Status == PrefabConversionStatus.Failed ? MessageType.Error : MessageType.Info);

            foreach (var warning in result.Warnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);

            foreach (var part in result.Parts)
                DrawPartLine(part);

            EditorGUI.indentLevel--;
        }

        private static void DrawPartLine(PrefabPartMatch part)
        {
            var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
            var kindWidth = Mathf.Min(KindColumnWidth, rect.width * 0.45f);
            var pathRect = new Rect(rect.x, rect.y, rect.width - kindWidth, rect.height);
            var kindRect = new Rect(rect.xMax - kindWidth, rect.y, kindWidth, rect.height);

            var icon = part.IsGameObject ? EditorGUIUtility.IconContent("GameObject Icon").image : null;
            GUI.Label(pathRect, new GUIContent(part.Path, icon, part.Path), EditorStyles.miniLabel);

            var previousColor = GUI.contentColor;
            GUI.contentColor = KindColor(part.Kind, previousColor);
            GUI.Label(kindRect, DescribeKind(part.Kind), EditorStyles.miniLabel);
            GUI.contentColor = previousColor;
        }


        // ── Подписи ──────────────────────────────────

        private static string SummarizePreview(PrefabConversionResult preview)
        {
            if (preview == null)
                return "";

            if (preview.Status != PrefabConversionStatus.Ready)
                return preview.Reason;

            var pieces = new List<string>
            {
                $"из шаблона {preview.CountParts(PrefabPartMatchKind.FromTemplate)}",
                $"своё {preview.CountParts(PrefabPartMatchKind.Own)}"
            };

            var removed = preview.CountParts(PrefabPartMatchKind.TemplateExtraRemoved);
            if (removed > 0)
                pieces.Add($"убрать {removed}");

            var kept = preview.CountParts(PrefabPartMatchKind.TemplateExtraKept);
            if (kept > 0)
                pieces.Add($"добавится {kept}");

            return string.Join(" · ", pieces);
        }


        private static string DescribeKind(PrefabPartMatchKind kind)
        {
            switch (kind)
            {
                case PrefabPartMatchKind.FromTemplate: return "из шаблона";
                case PrefabPartMatchKind.Own: return "своё, поверх шаблона";
                case PrefabPartMatchKind.TemplateExtraRemoved: return "только в шаблоне — убрано";
                case PrefabPartMatchKind.TemplateExtraKept: return "только в шаблоне — добавится";
                case PrefabPartMatchKind.Lost: return "пропадёт";
                default: return kind.ToString();
            }
        }


        private static Color KindColor(PrefabPartMatchKind kind, Color normal)
        {
            switch (kind)
            {
                case PrefabPartMatchKind.Lost: return LostColor;
                case PrefabPartMatchKind.TemplateExtraRemoved:
                case PrefabPartMatchKind.TemplateExtraKept: return TemplateExtraColor;
                default: return normal;
            }
        }


        private static Texture StatusIcon(PrefabConversionResult result)
        {
            if (result == null)
                return null;

            switch (result.Status)
            {
                case PrefabConversionStatus.Failed:
                    return EditorGUIUtility.IconContent("console.erroricon.sml").image;

                case PrefabConversionStatus.Skipped:
                    return EditorGUIUtility.IconContent("console.infoicon.sml").image;

                default:
                    return result.Warnings.Count > 0
                        ? EditorGUIUtility.IconContent("console.warnicon.sml").image
                        : EditorGUIUtility.IconContent("TestPassed").image;
            }
        }


        private static string NameOf(string guid) => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));


        // ── Unity-события ────────────────────────────

        private void OnEnable()
        {
            removeTemplateExtras = EditorPrefs.GetBool(RemoveTemplateExtrasKey, true);
            needsFullPreview = true;
        }

        private void OnGUI()
        {
            // Стиль сводки — от EditorStyles, а они готовы только внутри отрисовки.
            summaryStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };

            // Считаем только на раскладке: в остальных событиях Unity ждёт тех же строк, что уже разложены.
            if (Event.current.type == EventType.Layout)
                RefreshPreviews();

            EditorGUILayout.Space(4f);
            DrawTemplateSection();
            EditorGUILayout.Space(4f);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawPrefabList();
            DrawLastRun();
            EditorGUILayout.EndScrollView();

            DrawRunSection();
            HandleDragAndDrop();
        }

        // Префабы могли поменять руками или пачкой — предпросмотр устарел.
        private void OnProjectChange()
        {
            needsFullPreview = true;
            Repaint();
        }
    }
}
