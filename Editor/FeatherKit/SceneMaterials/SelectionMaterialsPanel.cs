using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Вкладка «Выделение»: материалы выделенных объектов, разложенные по группам, и что
    /// сделать с отмеченными — завести новую копию в сцене или в проекте, подставить готовый.
    ///
    /// Группами, а не объектами: «дать этим пяти камням свой материал» — вопрос про материал,
    /// а не про каждый камень. Одна копия на группу и есть ответ на него.
    /// </summary>
    public class SelectionMaterialsPanel
    {
        private const string IncludeChildrenKey = "FeatherKit.SceneMaterials.IncludeChildren";
        private const string SharedCopyKey = "FeatherKit.SceneMaterials.SharedCopy";
        private const float ToggleWidth = 16f;
        private const int TooltipSlotLimit = 12;

        private static readonly string[] CopyModeTitles = { "Каждому свой", "Один на всех" };

        private readonly List<MaterialSlotGroup> groups = new List<MaterialSlotGroup>();
        private readonly HashSet<Material> uncheckedMaterials = new HashSet<Material>();
        private readonly List<MaterialSlotGroup> checkedSources = new List<MaterialSlotGroup>();
        private readonly List<MaterialSlot> checkedSlots = new List<MaterialSlot>();
        private readonly SceneMaterialUsage usage;
        private readonly MaterialPreviewCache previews;
        private readonly InspectedMaterialPanel inspected;
        private readonly Action requestRefresh;

        private GUIStyle placeholderStyle;
        private int selectedObjectCount;
        private int slotCount;
        private bool includeChildren;
        private bool isSharedCopy;
        private bool isSingleCopy;
        private string copyName = string.Empty;
        private Material replacement;
        private Vector2 scroll;


        public SelectionMaterialsPanel(SceneMaterialUsage usage, MaterialPreviewCache previews, InspectedMaterialPanel inspected, Action requestRefresh)
        {
            this.usage = usage;
            this.previews = previews;
            this.inspected = inspected;
            this.requestRefresh = requestRefresh;

            includeChildren = EditorPrefs.GetBool(IncludeChildrenKey, true);
            isSharedCopy = EditorPrefs.GetBool(SharedCopyKey, false);
        }


        public int GroupCount => groups.Count;

        public int CheckedSlotCount
        {
            get
            {
                var count = 0;

                foreach (var group in groups)
                    if (IsChecked(group))
                        count += group.Slots.Count;

                return count;
            }
        }

        private GUIStyle PlaceholderStyle => placeholderStyle ??= new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 0.8f) }
        };


        // ── Данные ───────────────────────────────────────

        public void Refresh()
        {
            groups.Clear();

            var slots = new List<MaterialSlot>();
            var owners = Selection.gameObjects;

            SceneMaterialTools.CollectSlots(owners, includeChildren, slots);
            MaterialSlotGroup.Build(slots, groups);

            selectedObjectCount = 0;
            slotCount = slots.Count;

            foreach (var owner in owners)
                if (!EditorUtility.IsPersistent(owner))
                    selectedObjectCount++;
        }


        /// <summary>Новое выделение — новая копия: имя, набранное для прошлого, этому не подходит.</summary>
        public void ResetCopyName() => copyName = string.Empty;


        /// <summary>Подставляет материал всем отмеченным слотам. Зовёт и вкладка «Сцена» — «отдать выделению».</summary>
        public void AssignToChecked(Material material)
        {
            if (material == null)
                return;

            if (!EditorUtility.IsPersistent(material) && !SceneMaterialTools.IsSceneEditable())
                return;

            var slots = CollectCheckedSlots();

            if (slots.Count == 0)
                return;

            SceneMaterialTools.RunAsOneUndo(SceneMaterialTools.AssignLabel,
                () => SceneMaterialTools.Assign(slots, material, SceneMaterialTools.AssignLabel));

            ShowResult(material);
        }


        // ── Шапка вкладки ────────────────────────────────

        /// <summary>Своё место в общей полосе окна: галка «с детьми».</summary>
        public void DrawToolbarControls()
        {
            var value = GUILayout.Toggle(includeChildren, new GUIContent("С детьми", "Брать и вложенные объекты — нужно для составных, вроде дома из частей."), EditorStyles.toolbarButton);

            if (value == includeChildren)
                return;

            includeChildren = value;
            EditorPrefs.SetBool(IncludeChildrenKey, value);
            requestRefresh();
        }


        // ── Отрисовка ────────────────────────────────────

        public void Draw()
        {
            // Отметки меняются посреди нажатия, а раскладка Unity требует одних и тех же
            // полей на весь кадр: состав отмеченного запоминаем раз, на раскладке.
            if (Event.current.type == EventType.Layout)
                SnapshotChecked();

            if (selectedObjectCount == 0)
            {
                EditorGUILayout.HelpBox("Выдели объекты в сцене — покажу их материалы, собранные по группам.", MessageType.Info);
                return;
            }

            if (groups.Count == 0)
            {
                EditorGUILayout.HelpBox(includeChildren
                    ? "У выделенного нет материалов."
                    : "У самих выделенных объектов материалов нет. Включи «С детьми» — возьму и вложенные.", MessageType.Info);
                return;
            }

            DrawSummary();

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));

            foreach (var group in groups)
                DrawGroup(group);

            EditorGUILayout.EndScrollView();

            DrawActions();
        }

        private void DrawSummary()
        {
            EditorGUILayout.BeginHorizontal();

            var checkedCount = CountCheckedGroups();

            EditorGUI.showMixedValue = checkedCount > 0 && checkedCount < groups.Count;
            var all = EditorGUILayout.Toggle(checkedCount == groups.Count, GUILayout.Width(ToggleWidth));
            EditorGUI.showMixedValue = false;

            if (all != (checkedCount == groups.Count))
                SetAllChecked(all);

            EditorGUILayout.LabelField($"{MaterialRowGUI.CountObjects(selectedObjectCount)} · {MaterialRowGUI.CountSlots(slotCount)}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGroup(MaterialSlotGroup group)
        {
            var row = MaterialRowGUI.ReserveRow();
            var isChecked = IsChecked(group);

            MaterialRowGUI.DrawBackground(row, group.Material != null && inspected.Material == group.Material);

            var content = row;
            content.xMin += MaterialRowGUI.Gap;

            var toggleRect = MaterialRowGUI.CenterVertically(MaterialRowGUI.CutLeft(ref content, ToggleWidth), ToggleWidth);

            if (GUI.Toggle(toggleRect, isChecked, GUIContent.none) != isChecked)
                SetChecked(group, !isChecked);

            // Неотмеченная группа приглушена: сразу видно, чего операция не коснётся.
            var color = GUI.color;

            if (!isChecked)
                GUI.color = new Color(color.r, color.g, color.b, color.a * 0.5f);

            MaterialRowGUI.DrawPreview(ref content, group.Material, previews);
            MaterialRowGUI.DrawBadge(ref content, group.Material);
            MaterialRowGUI.DrawTitle(content, Title(group), Subtitle(group));

            GUI.color = color;

            if (row.Contains(Event.current.mousePosition))
                GUI.Label(row, new GUIContent(string.Empty, Tooltip(group)));

            switch (MaterialRowGUI.HandleMouse(row, group.Material))
            {
                case MaterialRowClick.Single:
                    inspected.Show(group.Material);
                    break;

                case MaterialRowClick.Context:
                    ShowGroupMenu(group);
                    break;
            }
        }

        private static string Title(MaterialSlotGroup group) =>
            group.Material != null ? group.Material.name : "Пустой слот";

        private string Subtitle(MaterialSlotGroup group)
        {
            var carriers = $"{MaterialRowGUI.CountSlots(group.Slots.Count)} · {MaterialRowGUI.CountObjects(group.OwnerCount)}";

            if (group.Material == null)
                return carriers + " · подставь материал";

            var total = usage.CountSlots(group.Material);

            return total > group.Slots.Count ? $"{carriers} · всего в сцене {total}" : carriers;
        }

        private static string Tooltip(MaterialSlotGroup group)
        {
            var text = new StringBuilder();
            var shown = Mathf.Min(group.Slots.Count, TooltipSlotLimit);

            for (var index = 0; index < shown; index++)
                text.AppendLine(group.Slots[index].Title);

            if (group.Slots.Count > shown)
                text.Append($"…и ещё {group.Slots.Count - shown}");

            return text.ToString().TrimEnd();
        }

        private void ShowGroupMenu(MaterialSlotGroup group)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Выделить только эти объекты"), false, () => SceneMaterialTools.SelectOwners(group.Slots));

            if (group.Material != null)
                menu.AddItem(new GUIContent("Выделить всех носителей в сцене"), false, () => usage.SelectOwners(group.Material));

            menu.AddItem(new GUIContent("Отметить только эту группу"), false, () =>
            {
                CheckOnly(group);
                requestRefresh();
            });
            menu.AddSeparator(string.Empty);

            switch (SceneMaterialTools.GetOrigin(group.Material))
            {
                case MaterialOrigin.Scene:
                    menu.AddItem(new GUIContent("Сохранить в проект…"), false, () => ShowResult(SceneMaterialTools.SaveToProject(group.Material)));
                    break;

                case MaterialOrigin.Project:
                    menu.AddItem(new GUIContent("Показать в проекте"), false, () => EditorGUIUtility.PingObject(group.Material));
                    break;
            }

            menu.ShowAsContext();
        }


        // ── Действия с отмеченными ───────────────────────

        private void DrawActions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Отмеченным: {MaterialRowGUI.CountSlots(checkedSlots.Count)}", EditorStyles.miniBoldLabel);

            if (checkedSources.Count > 1)
                DrawCopyMode(checkedSources[0]);

            using (new EditorGUI.DisabledScope(checkedSources.Count == 0))
            {
                if (isSingleCopy)
                    DrawCopyName(checkedSources.Count > 0 ? SceneMaterialTools.SuggestCopyName(checkedSources[0].Material, checkedSlots) : string.Empty);

                DrawCopyButtons();
            }

            DrawReplacement();
            EditorGUILayout.EndVertical();
        }

        private void DrawCopyMode(MaterialSlotGroup basis)
        {
            var mode = GUILayout.Toolbar(isSharedCopy ? 1 : 0, CopyModeTitles, EditorStyles.miniButton);

            if ((mode == 1) != isSharedCopy)
            {
                isSharedCopy = mode == 1;
                EditorPrefs.SetBool(SharedCopyKey, isSharedCopy);
            }

            EditorGUILayout.LabelField(isSharedCopy
                ? $"Одна копия на всех. Настройки — от верхнего: {basis.Material.name}."
                : "Каждому материалу своя копия, общая для его слотов.", EditorStyles.wordWrappedMiniLabel);
        }

        // Пустое поле — имя по умолчанию: оно видно серым, и набирать его руками не нужно.
        private void DrawCopyName(string suggested)
        {
            var rect = EditorGUILayout.GetControlRect();
            copyName = EditorGUI.TextField(rect, "Имя копии", copyName);

            if (!string.IsNullOrEmpty(copyName) || Event.current.type != EventType.Repaint)
                return;

            var hint = new Rect(rect.x + EditorGUIUtility.labelWidth + 4f, rect.y, rect.width - EditorGUIUtility.labelWidth - 4f, rect.height);
            GUI.Label(hint, suggested, PlaceholderStyle);
        }

        private void DrawCopyButtons()
        {
            EditorGUILayout.BeginHorizontal();

            var isPrefabMode = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null;

            using (new EditorGUI.DisabledScope(isPrefabMode))
            {
                var tooltip = isPrefabMode ? "В режиме префаба сценовому материалу негде храниться." : "Копия живёт только в этой сцене, файла в проекте нет.";

                if (GUILayout.Button(new GUIContent("Новый в сцене", tooltip)))
                {
                    ShowResult(CreateSceneCopies());
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button(new GUIContent("Новый в проекте…", "Копия ляжет файлом: её можно будет дать и другим сценам.")))
            {
                ShowResult(CreateProjectCopies());
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
        }

        private Material CreateSceneCopies()
        {
            if (!SceneMaterialTools.IsSceneEditable())
                return null;

            Material created = null;

            SceneMaterialTools.RunAsOneUndo(SceneMaterialTools.MakeUniqueLabel, () =>
            {
                if (isSingleCopy)
                {
                    created = SceneMaterialTools.AssignSceneCopy(checkedSlots, checkedSources[0].Material, CopyNameOr(checkedSources[0]));
                    return;
                }

                foreach (var group in checkedSources)
                    created = SceneMaterialTools.AssignSceneCopy(group.Slots, group.Material, SceneMaterialTools.SuggestCopyName(group.Material, group.Slots));
            });

            return created;
        }

        private Material CreateProjectCopies()
        {
            if (isSingleCopy)
            {
                var path = SceneMaterialTools.AskMaterialPath(CopyNameOr(checkedSources[0]));

                if (string.IsNullOrEmpty(path))
                    return null;

                Material single = null;
                SceneMaterialTools.RunAsOneUndo(SceneMaterialTools.ProjectCopyLabel,
                    () => single = SceneMaterialTools.AssignProjectCopy(checkedSlots, checkedSources[0].Material, path));

                AssetDatabase.SaveAssets();
                return single;
            }

            // Несколько копий — одна папка на все: по окну сохранения на каждую было бы мучением.
            var folder = SceneMaterialTools.AskMaterialsFolder();

            if (folder == null)
                return null;

            Material created = null;

            SceneMaterialTools.RunAsOneUndo(SceneMaterialTools.ProjectCopyLabel, () =>
            {
                foreach (var group in checkedSources)
                {
                    var path = SceneMaterialTools.MakeMaterialPath(folder, SceneMaterialTools.SuggestCopyName(group.Material, group.Slots));
                    created = SceneMaterialTools.AssignProjectCopy(group.Slots, group.Material, path);
                }
            });

            AssetDatabase.SaveAssets();
            return created;
        }

        private string CopyNameOr(MaterialSlotGroup basis) =>
            string.IsNullOrWhiteSpace(copyName) ? SceneMaterialTools.SuggestCopyName(basis.Material, checkedSlots) : copyName.Trim();

        private void DrawReplacement()
        {
            EditorGUILayout.BeginHorizontal();

            replacement = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Подставить", "Готовый материал — из проекта или строкой из списка, перетащив её сюда."),
                replacement, typeof(Material), true);

            using (new EditorGUI.DisabledScope(replacement == null || checkedSlots.Count == 0))
            {
                if (GUILayout.Button("Ок", GUILayout.Width(36f)))
                {
                    AssignToChecked(replacement);
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndHorizontal();
        }


        // ── Отметки ──────────────────────────────────────

        private bool IsChecked(MaterialSlotGroup group) => !uncheckedMaterials.Contains(group.Material);


        // Отметку помнит материал, а не строка: список собирается заново на каждое изменение сцены.
        private void SetChecked(MaterialSlotGroup group, bool isChecked)
        {
            if (isChecked)
                uncheckedMaterials.Remove(group.Material);
            else
                uncheckedMaterials.Add(group.Material);
        }


        private void SetAllChecked(bool isChecked)
        {
            foreach (var group in groups)
                SetChecked(group, isChecked);
        }


        private void CheckOnly(MaterialSlotGroup only)
        {
            foreach (var group in groups)
                SetChecked(group, group == only);
        }


        private int CountCheckedGroups()
        {
            var count = 0;

            foreach (var group in groups)
                if (IsChecked(group))
                    count++;

            return count;
        }


        /// <summary>
        /// Запоминает отмеченное на кадр: с каких материалов снимать копии (пустые слоты
        /// не в счёт), какие слоты их получат и выйдет ли копия одна.
        /// </summary>
        private void SnapshotChecked()
        {
            checkedSources.Clear();

            foreach (var group in groups)
                if (group.Material != null && IsChecked(group))
                    checkedSources.Add(group);

            checkedSlots.Clear();
            checkedSlots.AddRange(CollectCheckedSlots());

            isSingleCopy = checkedSources.Count == 1 || isSharedCopy;
        }


        private List<MaterialSlot> CollectCheckedSlots()
        {
            var slots = new List<MaterialSlot>();

            foreach (var group in groups)
                if (IsChecked(group))
                    slots.AddRange(group.Slots);

            return slots;
        }


        // ── Общее ────────────────────────────────────────

        private void ShowResult(Material material)
        {
            if (material != null)
                inspected.Show(material);

            requestRefresh();
        }
    }
}
