using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.TextureMaterials
{
    /// <summary>
    /// Окно «Материалы из текстур»: выделил в Project папки пака или текстуры — получил по материалу на текстуру.
    /// - сверху откуда брать: запоминает выделение на момент открытия, можно взять заново
    /// - настройки — <see cref="TextureMaterialSettingsPanel"/>; после правки план пересобирается
    /// - итог и список до создания — <see cref="TextureMaterialPlanListView"/>; кнопка «Создать» выполняет план
    /// Решает, когда пересобрать план и справочник дубликатов; само создание — <see cref="TextureMaterialCreator"/>.
    /// </summary>
    public class TextureMaterialsWindow : EditorWindow
    {
        private const string MenuPath = "Assets/Create Materials from Textures…";

        [SerializeField] private List<string> sourcePaths = new List<string>();

        private readonly TextureMaterialPlanListView planListView = new TextureMaterialPlanListView();

        private TextureMaterialIndex index;
        private TextureMaterialPlan plan;
        private Vector2 settingsScroll;


        private static TextureMaterialSettings Settings => TextureMaterialSettings.instance;


        // ── Открытие ─────────────────────────────────────────────────────

        [MenuItem(MenuPath, false, 19)]
        private static void OpenForSelection()
        {
            var window = GetWindow<TextureMaterialsWindow>(true, "Материалы из текстур");
            window.minSize = new Vector2(480f, 520f);
            window.TakeProjectSelection();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanOpenForSelection()
        {
            return SelectedAssetPaths().Any(IsTextureOrFolder);
        }

        [MenuItem("FeatherKit/Texture Materials")]
        private static void OpenFromMainMenu()
        {
            OpenForSelection();
        }


        // ── Откуда брать ─────────────────────────────────────────────────

        private void TakeProjectSelection()
        {
            sourcePaths = SelectedAssetPaths().Where(IsTextureOrFolder).ToList();
            plan = null;
        }

        private static IEnumerable<string> SelectedAssetPaths()
        {
            return Selection.GetFiltered<Object>(SelectionMode.Assets).Select(AssetDatabase.GetAssetPath);
        }

        private static bool IsTextureOrFolder(string assetPath)
        {
            return AssetDatabase.IsValidFolder(assetPath) || AssetDatabase.GetMainAssetTypeAtPath(assetPath) == typeof(Texture2D);
        }


        private void DrawSources()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Откуда", EditorStyles.boldLabel);

                if (GUILayout.Button(new GUIContent("Взять выделение", "Заменить список тем, что сейчас выделено в Project"),
                        GUILayout.Width(130f)))
                    TakeProjectSelection();
            }

            if (sourcePaths.Count == 0)
                EditorGUILayout.HelpBox("Выдели в Project папку пака или текстуры и нажми «Взять выделение»", MessageType.Info);

            foreach (var path in sourcePaths)
            {
                var icon = AssetDatabase.IsValidFolder(path) ? "Folder Icon" : "Texture Icon";

                EditorGUILayout.LabelField(new GUIContent(path, EditorGUIUtility.IconContent(icon).image), EditorStyles.miniLabel);
            }
        }


        // ── План ─────────────────────────────────────────────────────────

        private void RebuildPlanIfNeeded()
        {
            if (plan != null)
                return;

            index ??= new TextureMaterialIndex();
            plan = TextureMaterialPlan.ForAssetPaths(sourcePaths, Settings, index);
        }


        private void DrawSummary()
        {
            EditorGUILayout.LabelField("Итог", EditorStyles.boldLabel);

            var skipped = plan.CountOf(TextureMaterialDecision.NormalMap) + plan.CountOf(TextureMaterialDecision.ServiceMapByName);

            EditorGUILayout.LabelField($"Текстур: {plan.Items.Count}   ·   создать: {plan.CreateCount}   ·   " +
                                       $"уже есть: {plan.CountOf(TextureMaterialDecision.AlreadyHasMaterial)}   ·   " +
                                       $"имя занято: {plan.CountOf(TextureMaterialDecision.NameTaken)}   ·   пропущено: {skipped}");
        }


        // ── Создание ─────────────────────────────────────────────────────

        private void DrawCreateButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Сбросить настройки", "Вернуть настройки как в новом проекте"), GUILayout.Width(140f)))
                {
                    Settings.ResetToDefaults();
                    plan = null;
                }

                GUILayout.FlexibleSpace();

                var canCreate = plan.CreateCount > 0 || (Settings.ChangesTextureImport && plan.Items.Any(item => item.IsSurfaceTexture));

                using (new EditorGUI.DisabledScope(!canCreate))
                {
                    if (GUILayout.Button($"Создать ({plan.CreateCount})", GUILayout.Width(160f), GUILayout.Height(26f)))
                        CreateMaterials();
                }
            }
        }

        private void CreateMaterials()
        {
            var created = TextureMaterialCreator.CreateAll(plan, Settings, index);

            plan = null;

            ShowNotification(new GUIContent($"Создано материалов: {created.Count}"));
            Debug.Log($"Материалы из текстур: создано {created.Count}");

            if (created.Count > 0)
                EditorGUIUtility.PingObject(created[0]);
        }


        // ── Проект поменялся ─────────────────────────────────────────────

        // Справочник мог устареть: материалы удалили или добавили снаружи. Пересоберём при следующей отрисовке.
        private void OnProjectChanged()
        {
            index = null;
            plan = null;
            Repaint();
        }


        // ── Unity-события ────────────────────────────────────────────────

        private void OnEnable()
        {
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        private void OnGUI()
        {
            settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll, GUILayout.ExpandHeight(false));

            DrawSources();
            EditorGUILayout.Space();

            if (TextureMaterialSettingsPanel.Draw(Settings))
                plan = null;

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space();

            RebuildPlanIfNeeded();
            DrawSummary();
            planListView.Draw(plan);
            EditorGUILayout.Space();
            DrawCreateButtons();
        }
    }
}
