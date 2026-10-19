using System;
using System.Collections.Generic;
using System.Linq;
using FeatherKit.EditorTools.TextureMaterials;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools.LevelPalette
{
    /// <summary>
    /// Палитра уровня: вкладки с квадратами материалов, текстур и префабов под рукой, как браузер текстур в Hammer.
    /// - клик по материалу или текстуре красит выделенные грани или объекты; у текстуры без материала он создаётся
    /// - перетаскивание из палитры: префаб — в сцену, материал — на грани или объект, ассет — в поле инспектора
    /// - пипетка находит в палитре материал выделенной грани
    /// - перетаскивание в палитру: ассет — во вкладку вручную, папка — в источники вкладки
    /// Окно раскладывает части и решает, что значит клик и когда пересобрать список.
    /// Хранит всё <see cref="LevelPaletteStorage"/>; вкладки — <see cref="LevelPaletteTabsBar"/>, сетка — <see cref="LevelPaletteGrid"/>,
    /// настройки вкладки — <see cref="LevelPaletteTabSettingsPanel"/>, покраска — <see cref="FaceMaterialPainter"/>.
    /// </summary>
    public class LevelPaletteWindow : EditorWindow
    {
        private const float SizeSliderWidth = 90f;

        [SerializeField] private LevelPaletteTabsBar tabsBar = new LevelPaletteTabsBar();
        [SerializeField] private string search = string.Empty;

        private readonly PaletteMaterialResolver materialResolver = new PaletteMaterialResolver();
        private readonly List<string> visibleGuids = new List<string>();

        private LevelPaletteGrid grid;
        private List<string> tabGuids = new List<string>();
        private LevelPaletteTab listedTab;
        private string listedSearch;
        private bool isListOutdated = true;
        private string highlightedGuid;


        private static LevelPaletteStorage Storage => LevelPaletteStorage.instance;


        // ── Открытие ─────────────────────────────────────────────────────

        [MenuItem("FeatherKit/Level Palette")]
        private static void Open()
        {
            var window = GetWindow<LevelPaletteWindow>();
            window.titleContent = new GUIContent("Палитра", EditorGUIUtility.IconContent("Material Icon").image);
            window.minSize = new Vector2(220f, 200f);
        }


        // ── Список клеток ────────────────────────────────────────────────

        // Папки сканируются только когда что-то поменялось, поиск — фильтр по готовому списку.
        private void RefreshListIfNeeded()
        {
            var tab = Storage.ActiveTab;

            if (isListOutdated || tab != listedTab)
            {
                tabGuids = tab.CollectAssetGuids();
                listedTab = tab;
                listedSearch = null;
                isListOutdated = false;
            }

            if (listedSearch == search)
                return;

            listedSearch = search;
            visibleGuids.Clear();

            var words = search.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var guid in tabGuids)
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));

                if (words.All(word => name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0))
                    visibleGuids.Add(guid);
            }
        }

        private void MarkListOutdated()
        {
            isListOutdated = true;
            Repaint();
        }


        // ── Поиск, пипетка, вид ──────────────────────────────────────────

        private void DrawSearchToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);

                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_eyeDropper.Large").image,
                        "Пипетка: найти в палитре материал выделенной грани"), EditorStyles.toolbarButton, GUILayout.Width(28f)))
                    PickMaterialFromSelection();

                Storage.ShowNames = GUILayout.Toggle(Storage.ShowNames, new GUIContent("Aa", "Подписи под клетками"),
                    EditorStyles.toolbarButton, GUILayout.Width(28f));

                var cellSize = GUILayout.HorizontalSlider(Storage.CellSize, LevelPaletteStorage.MinCellSize,
                    LevelPaletteStorage.MaxCellSize, GUILayout.Width(SizeSliderWidth));

                if (!Mathf.Approximately(cellSize, Storage.CellSize))
                    Storage.CellSize = cellSize;
            }
        }


        // Ищем сам материал, а во вкладке текстур — его текстуру; нет в открытой — смотрим другие вкладки.
        private void PickMaterialFromSelection()
        {
            var material = FaceMaterialPicker.PickFromSelection();

            if (material == null)
            {
                ShowNotification(new GUIContent("Выдели грань или объект в сцене"));
                return;
            }

            var materialGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material));
            var textureGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MaterialMainTexture.Find(material)));
            var tabs = new[] { Storage.ActiveTab }.Concat(Storage.Tabs.Where(tab => tab != Storage.ActiveTab));

            foreach (var tab in tabs)
            {
                var guids = tab == listedTab ? tabGuids : tab.CollectAssetGuids();
                var foundGuid = guids.Contains(materialGuid) ? materialGuid
                    : !string.IsNullOrEmpty(textureGuid) && guids.Contains(textureGuid) ? textureGuid
                    : null;

                if (foundGuid != null)
                {
                    HighlightCell(tab, foundGuid);
                    return;
                }
            }

            ShowNotification(new GUIContent($"«{material.name}» нет в палитре"));
            EditorGUIUtility.PingObject(material);
        }

        private void HighlightCell(LevelPaletteTab tab, string guid)
        {
            Storage.SelectTab(tab);
            search = string.Empty;
            highlightedGuid = guid;
            grid.ScrollTo(guid);
            Repaint();
        }


        // ── Вкладку переключили ──────────────────────────────────────────

        private void OnTabSelected()
        {
            highlightedGuid = null;
        }


        // ── Клик по клетке ───────────────────────────────────────────────

        private void OnCellClicked(string guid)
        {
            var asset = LoadAsset(guid);
            highlightedGuid = guid;

            if (!(asset is Material) && !(asset is Texture2D))
            {
                EditorGUIUtility.PingObject(asset);
                return;
            }

            // Сначала цель, потом материал: без цели у текстуры не создаём материал зря.
            if (FaceMaterialPainter.DescribeTarget() == null)
            {
                ShowNotification(new GUIContent("Выдели грани или объект в сцене"));
                return;
            }

            var material = materialResolver.Resolve(asset, Storage.ActiveTab);

            if (material == null)
            {
                ShowNotification(new GUIContent("Это служебная карта — материал из неё не делается"));
                return;
            }

            FaceMaterialPainter.Paint(material);
        }


        private void OnCellDoubleClicked(string guid)
        {
            Selection.activeObject = LoadAsset(guid);
        }


        private void OnCellContextClicked(string guid)
        {
            var tab = Storage.ActiveTab;
            var asset = LoadAsset(guid);
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Показать в Project"), false, () => EditorGUIUtility.PingObject(asset));
            menu.AddItem(new GUIContent("Выделить"), false, () => Selection.activeObject = asset);

            if (asset is Texture2D)
                menu.AddItem(new GUIContent("Создать материал"), false, () => CreateMaterialForTexture(asset));

            menu.AddSeparator(string.Empty);

            foreach (var otherTab in Storage.Tabs.Where(other => other != tab))
                menu.AddItem(new GUIContent($"Добавить во вкладку/{otherTab.Name}"), false,
                    () => Storage.AddManualAssets(otherTab, new[] { guid }));

            if (tab.IsManual(guid))
                menu.AddItem(new GUIContent("Убрать из вкладки"), false, () => Storage.RemoveManualAsset(tab, guid));
            else
                menu.AddDisabledItem(new GUIContent("Убрать из вкладки (лежит в папке-источнике)"));

            menu.ShowAsContext();
        }

        private void CreateMaterialForTexture(Object texture)
        {
            var material = materialResolver.Resolve(texture, Storage.ActiveTab);

            if (material != null)
                EditorGUIUtility.PingObject(material);
            else
                ShowNotification(new GUIContent("Это служебная карта — материал из неё не делается"));
        }


        // ── Перетаскивание ───────────────────────────────────────────────

        // Текстура тащит ещё и свой материал: на грань ляжет он, а в поле текстуры — сама текстура.
        private Object[] CollectDraggedObjects(Object asset)
        {
            if (!(asset is Texture2D))
                return new[] { asset };

            var material = materialResolver.Resolve(asset, Storage.ActiveTab);

            return material != null ? new[] { material, asset } : new[] { asset };
        }


        private void OnObjectsDropped(Object[] objects)
        {
            var tab = Storage.ActiveTab;
            var folders = objects.Where(IsFolder).ToList();
            var accepted = objects.Where(asset => tab.AssetKind.Accepts(asset)).ToList();
            var rejectedCount = objects.Length - folders.Count - accepted.Count;

            foreach (var folder in folders)
                Storage.AddSourceFolder(tab, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(folder)));

            if (accepted.Count > 0)
                Storage.AddManualAssets(tab, accepted.Select(asset => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset))));

            if (rejectedCount > 0)
                ShowNotification(new GUIContent($"Не подходит вкладке «{tab.Name}»: {rejectedCount}"));
        }

        private static bool IsFolder(Object asset)
        {
            return AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(asset));
        }


        // ── Строка состояния ─────────────────────────────────────────────

        private void DrawStatusBar()
        {
            var target = FaceMaterialPainter.DescribeTarget();
            var hint = target != null ? $"Клик покрасит — {target}" : "Выдели грани или объект, чтобы красить кликом";

            EditorGUILayout.LabelField($"{visibleGuids.Count} шт.   ·   {hint}", EditorStyles.miniLabel);
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private static Object LoadAsset(string guid)
        {
            return AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
        }


        // ── Проект и выделение поменялись ────────────────────────────────

        private void OnProjectChanged()
        {
            grid.ClearCache();
            materialResolver.Forget();
            MarkListOutdated();
        }


        private void OnSceneSelectionChanged()
        {
            Repaint();
        }

        private void OnProBuilderSelectionUpdated(IEnumerable<ProBuilderMesh> meshes)
        {
            Repaint();
        }


        // ── Unity-события ────────────────────────────────────────────────

        private void OnEnable()
        {
            grid = new LevelPaletteGrid(CollectDraggedObjects);
            grid.CellClicked += OnCellClicked;
            grid.CellDoubleClicked += OnCellDoubleClicked;
            grid.CellContextClicked += OnCellContextClicked;
            grid.ObjectsDropped += OnObjectsDropped;
            tabsBar.TabSelected += OnTabSelected;

            Storage.Changed += MarkListOutdated;
            EditorApplication.projectChanged += OnProjectChanged;
            Selection.selectionChanged += OnSceneSelectionChanged;
            ToolManager.activeContextChanged += OnSceneSelectionChanged;
            ProBuilderEditor.selectionUpdated += OnProBuilderSelectionUpdated;
        }

        private void OnDisable()
        {
            tabsBar.TabSelected -= OnTabSelected;
            Storage.Changed -= MarkListOutdated;
            EditorApplication.projectChanged -= OnProjectChanged;
            Selection.selectionChanged -= OnSceneSelectionChanged;
            ToolManager.activeContextChanged -= OnSceneSelectionChanged;
            ProBuilderEditor.selectionUpdated -= OnProBuilderSelectionUpdated;
        }

        private void OnGUI()
        {
            tabsBar.Draw(position.width);
            DrawSearchToolbar();

            if (tabsBar.IsSettingsShown)
                LevelPaletteTabSettingsPanel.Draw(Storage.ActiveTab);

            RefreshListIfNeeded();

            var gridRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (grid.Draw(gridRect, visibleGuids, highlightedGuid, Storage.CellSize, Storage.ShowNames))
                Repaint();

            DrawStatusBar();
        }
    }
}
