using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Вкладка «Сцена»: все сценовые копии открытых сцен — сколько у каждой носителей,
    /// кто они и как уехать в проект.
    /// Нужна потому, что у сценового материала нет файла, и списком он больше нигде не лежит:
    /// в окне проекта его нет, а в инспекторе виден только текущий объект.
    /// </summary>
    public class SceneMaterialsPanel
    {
        private const string SortKey = "FeatherKit.SceneMaterials.SortByCarriers";
        private const float SelectButtonWidth = 72f;
        private const float SaveButtonWidth = 78f;

        private static readonly string[] SortTitles = { "По имени", "По носителям" };

        private readonly List<Material> visibleMaterials = new List<Material>();
        private readonly SceneMaterialUsage usage;
        private readonly MaterialPreviewCache previews;
        private readonly InspectedMaterialPanel inspected;
        private readonly SelectionMaterialsPanel selection;
        private readonly Action requestRefresh;

        private string search = string.Empty;
        private bool isSortedByCarriers;
        private Vector2 scroll;


        public SceneMaterialsPanel(SceneMaterialUsage usage, MaterialPreviewCache previews, InspectedMaterialPanel inspected, SelectionMaterialsPanel selection, Action requestRefresh)
        {
            this.usage = usage;
            this.previews = previews;
            this.inspected = inspected;
            this.selection = selection;
            this.requestRefresh = requestRefresh;

            isSortedByCarriers = EditorPrefs.GetBool(SortKey, false);
        }


        // ── Шапка вкладки ────────────────────────────────

        /// <summary>Своё место в общей полосе окна: поиск по имени и порядок.</summary>
        public void DrawToolbarControls()
        {
            search = GUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(60f), GUILayout.MaxWidth(220f));

            var sort = EditorGUILayout.Popup(isSortedByCarriers ? 1 : 0, SortTitles, EditorStyles.toolbarPopup, GUILayout.Width(96f));

            if ((sort == 1) == isSortedByCarriers)
                return;

            isSortedByCarriers = sort == 1;
            EditorPrefs.SetBool(SortKey, isSortedByCarriers);
        }


        // ── Отрисовка ────────────────────────────────────

        public void Draw()
        {
            // Список пересобирается только на раскладке: Unity требует одних и тех же строк на весь кадр.
            if (Event.current.type == EventType.Layout)
                RebuildVisible();

            if (usage.SceneMaterials.Count == 0)
            {
                EditorGUILayout.HelpBox("В открытых сценах сценовых материалов нет. Завести их можно на вкладке «Выделение».", MessageType.Info);
                return;
            }

            if (visibleMaterials.Count == 0)
            {
                EditorGUILayout.HelpBox($"По «{search}» ничего не нашлось.", MessageType.None);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));

            foreach (var material in visibleMaterials)
                DrawMaterial(material);

            EditorGUILayout.EndScrollView();
        }

        private void RebuildVisible()
        {
            visibleMaterials.Clear();

            foreach (var material in usage.SceneMaterials)
                if (material != null && (search.Length == 0 || material.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                    visibleMaterials.Add(material);

            if (isSortedByCarriers)
                visibleMaterials.Sort((a, b) => usage.CountSlots(b).CompareTo(usage.CountSlots(a)));
            else
                visibleMaterials.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        }

        private void DrawMaterial(Material material)
        {
            var row = MaterialRowGUI.ReserveRow();
            MaterialRowGUI.DrawBackground(row, inspected.Material == material);

            var content = row;
            content.xMin += MaterialRowGUI.Gap;
            content.xMax -= MaterialRowGUI.Gap;

            MaterialRowGUI.DrawPreview(ref content, material, previews);

            var saveRect = MaterialRowGUI.CenterVertically(MaterialRowGUI.CutRight(ref content, SaveButtonWidth), 18f);
            var selectRect = MaterialRowGUI.CenterVertically(MaterialRowGUI.CutRight(ref content, SelectButtonWidth), 18f);

            var slots = usage.CountSlots(material);

            // Копию делят несколько слотов — правка уедет сразу всем. Обычно это след дублирования объекта.
            MaterialRowGUI.DrawTitle(content, material.name, Subtitle(material, slots), slots > 1 ? MaterialRowGUI.SharedColor : (Color?)null);

            if (GUI.Button(selectRect, "Выделить", EditorStyles.miniButton))
                usage.SelectOwners(material);

            if (GUI.Button(saveRect, "В проект…", EditorStyles.miniButton))
            {
                SaveToProject(material);
                GUIUtility.ExitGUI();
            }

            switch (MaterialRowGUI.HandleMouse(row, material))
            {
                case MaterialRowClick.Single:
                    inspected.Show(material);
                    break;

                case MaterialRowClick.Context:
                    ShowMaterialMenu(material);
                    break;
            }
        }

        // Носитель один — пишем его имя: так строку узнаёшь, не выделяя объект.
        private string Subtitle(Material material, int slots)
        {
            if (slots == 1)
                return $"1 слот · {usage.FindFirstOwner(material).name}";

            return $"делят {MaterialRowGUI.CountSlots(slots)} · {MaterialRowGUI.CountObjects(usage.CountOwners(material))}";
        }

        private void ShowMaterialMenu(Material material)
        {
            var menu = new GenericMenu();
            var checkedSlots = selection.CheckedSlotCount;

            var giveTitle = new GUIContent($"Отдать отмеченным во «Выделении» ({MaterialRowGUI.CountSlots(checkedSlots)})");

            if (checkedSlots > 0)
                menu.AddItem(giveTitle, false, () => selection.AssignToChecked(material));
            else
                menu.AddDisabledItem(giveTitle);

            menu.AddItem(new GUIContent("Выделить носителей"), false, () => usage.SelectOwners(material));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Сохранить в проект…"), false, () => SaveToProject(material));

            menu.ShowAsContext();
        }


        private void SaveToProject(Material material)
        {
            var asset = SceneMaterialTools.SaveToProject(material);

            if (asset != null)
                inspected.Show(asset);

            requestRefresh();
        }
    }
}
