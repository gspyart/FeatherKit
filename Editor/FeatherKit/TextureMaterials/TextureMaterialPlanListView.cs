using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.TextureMaterials
{
    /// <summary>
    /// Список плана в окне: строка на текстуру — что решено и куда ляжет материал.
    /// - фильтр по решению, чтобы в тысяче строк найти пропущенные
    /// - рисует только видимые строки: прокрутка тысячи не тормозит
    /// - клик по строке показывает текстуру в Project, по уже готовому материалу — материал
    /// </summary>
    public class TextureMaterialPlanListView
    {
        private const float RowHeight = 18f;
        private const float DecisionColumnWidth = 110f;

        private static readonly string[] FilterLabels = { "Все", "Будут созданы", "Уже есть", "Имя занято", "Пропущены" };

        private readonly List<TextureMaterialPlanItem> filteredItems = new List<TextureMaterialPlanItem>();

        private TextureMaterialPlan shownPlan;
        private int filterIndex;
        private int shownFilterIndex = -1;
        private Vector2 scroll;


        // ── Отрисовка ────────────────────────────────────────────────────

        public void Draw(TextureMaterialPlan plan)
        {
            filterIndex = EditorGUILayout.Popup("Показать", filterIndex, FilterLabels);

            if (plan != shownPlan || filterIndex != shownFilterIndex)
                RefilterItems(plan);

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));

            var listRect = GUILayoutUtility.GetRect(0f, filteredItems.Count * RowHeight, GUILayout.ExpandWidth(true));

            if (Event.current.type != EventType.Layout)
                DrawVisibleRows(listRect);

            EditorGUILayout.EndScrollView();
        }

        private void RefilterItems(TextureMaterialPlan plan)
        {
            shownPlan = plan;
            shownFilterIndex = filterIndex;
            filteredItems.Clear();

            if (plan == null)
                return;

            foreach (var item in plan.Items)
            {
                if (PassesFilter(item.Decision))
                    filteredItems.Add(item);
            }
        }

        private bool PassesFilter(TextureMaterialDecision decision)
        {
            switch (filterIndex)
            {
                case 1: return decision == TextureMaterialDecision.Create;
                case 2: return decision == TextureMaterialDecision.AlreadyHasMaterial;
                case 3: return decision == TextureMaterialDecision.NameTaken;
                case 4: return decision == TextureMaterialDecision.NormalMap || decision == TextureMaterialDecision.ServiceMapByName;
                default: return true;
            }
        }

        // Видимая часть — по прокрутке; высоту видимой области берём с запасом, лишние строки отсекает сам скролл.
        private void DrawVisibleRows(Rect listRect)
        {
            var first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
            var last = Mathf.Min(filteredItems.Count, first + Mathf.CeilToInt(Screen.height / RowHeight) + 1);

            for (var i = first; i < last; i++)
            {
                var rowRect = new Rect(listRect.x, listRect.y + i * RowHeight, listRect.width, RowHeight);

                DrawRow(rowRect, filteredItems[i]);
            }
        }

        private static void DrawRow(Rect rowRect, TextureMaterialPlanItem item)
        {
            var decisionRect = new Rect(rowRect.x, rowRect.y, DecisionColumnWidth, rowRect.height);
            var pathRect = new Rect(decisionRect.xMax, rowRect.y, rowRect.width - DecisionColumnWidth, rowRect.height);

            var previousColor = GUI.contentColor;
            GUI.contentColor = DecisionColor(item.Decision);
            EditorGUI.LabelField(decisionRect, DecisionLabel(item.Decision), EditorStyles.miniLabel);
            GUI.contentColor = previousColor;

            var text = item.MaterialPath == null
                ? item.TexturePath
                : $"{Path.GetFileName(item.TexturePath)}  →  {item.MaterialPath}";

            if (GUI.Button(pathRect, new GUIContent(text, item.TexturePath), EditorStyles.miniLabel))
                PingItem(item);
        }

        private static void PingItem(TextureMaterialPlanItem item)
        {
            var pingPath = item.Decision == TextureMaterialDecision.AlreadyHasMaterial ? item.MaterialPath : item.TexturePath;

            EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(pingPath));
        }


        // ── Подписи решений ──────────────────────────────────────────────

        public static string DecisionLabel(TextureMaterialDecision decision)
        {
            switch (decision)
            {
                case TextureMaterialDecision.Create: return "создать";
                case TextureMaterialDecision.AlreadyHasMaterial: return "уже есть";
                case TextureMaterialDecision.NameTaken: return "имя занято";
                case TextureMaterialDecision.NormalMap: return "карта рельефа";
                default: return "служебная карта";
            }
        }


        private static Color DecisionColor(TextureMaterialDecision decision)
        {
            switch (decision)
            {
                case TextureMaterialDecision.Create: return new Color(0.45f, 0.85f, 0.45f);
                case TextureMaterialDecision.AlreadyHasMaterial: return new Color(0.6f, 0.75f, 1f);
                case TextureMaterialDecision.NameTaken: return new Color(1f, 0.6f, 0.35f);
                default: return new Color(0.65f, 0.65f, 0.65f);
            }
        }
    }
}
