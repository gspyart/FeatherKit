using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Цветные зоны окна Live Builder (<see cref="LiveBuilderWindowZone"/>, цвет — <see cref="LiveBuilderZoneColor"/>): чтобы глаз сразу видел, где общее, где выбранный инструмент,
    /// где выделенное. У зоны — цветная полоса слева, лёгкая подкраска фона и заголовок: стрелка, цветной квадрат со значком, название
    /// и серое пояснение. Клик по заголовку сворачивает зону, свёрнута или нет — помнится у каждого человека.
    /// Цвета — под светлую и тёмную тему редактора.
    /// </summary>
    public static class LiveBuilderZoneFrame
    {
        private const string ExpandedPreferencePrefix = "FeatherKit.LiveBuilder.Section.";
        private const float HeaderHeight = 24f;
        private const float BadgeSize = 18f;
        private const float StripeWidth = 3f;

        private static GUIStyle zoneStyle;
        private static GUIStyle titleStyle;
        private static GUIStyle detailStyle;


        // ── Цвета ────────────────────────────────────────────────────────

        public static Color ColorOf(LiveBuilderZoneColor zone)
        {
            var isDark = EditorGUIUtility.isProSkin;

            return zone switch
            {
                LiveBuilderZoneColor.Tool => isDark ? new Color(0.23f, 0.46f, 0.78f) : new Color(0.16f, 0.42f, 0.82f),
                LiveBuilderZoneColor.Selection => isDark ? new Color(0.8f, 0.52f, 0.17f) : new Color(0.85f, 0.5f, 0.1f),
                LiveBuilderZoneColor.Level => isDark ? new Color(0.46f, 0.4f, 0.76f) : new Color(0.42f, 0.35f, 0.75f),
                _ => isDark ? new Color(0.45f, 0.45f, 0.45f) : new Color(0.5f, 0.5f, 0.5f)
            };
        }


        // Лёгкая подкраска фона зоны её цветом.
        public static Color TintOf(LiveBuilderZoneColor zone, float alpha = 0.07f)
        {
            var color = ColorOf(zone);
            color.a = alpha;
            return color;
        }


        // ── Стили ────────────────────────────────────────────────────────

        private static GUIStyle ZoneStyle => zoneStyle ??= new GUIStyle { padding = new RectOffset(10, 8, 0, 6), margin = new RectOffset(0, 0, 4, 0) };

        private static GUIStyle TitleStyle => titleStyle ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft };

        private static GUIStyle DetailStyle => detailStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip,
            normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.4f, 0.4f, 0.4f) }
        };


        // ── Зона ─────────────────────────────────────────────────────────

        // Зона окна. Использовать в using; рисовать содержимое, только если IsExpanded.
        // key — под ним помнится, свёрнута ли зона; icon — белый значок для цветного квадрата.
        public static LiveBuilderWindowZoneScope Begin(LiveBuilderZoneColor zone, string title, string detail, Texture2D icon, string key,
                                                       bool isExpandedByDefault = true)
        {
            return new LiveBuilderWindowZoneScope(zone, title, detail, icon, key, isExpandedByDefault);
        }

        public readonly struct LiveBuilderWindowZoneScope : System.IDisposable
        {
            public LiveBuilderWindowZoneScope(LiveBuilderZoneColor zone, string title, string detail, Texture2D icon, string key, bool isExpandedByDefault)
            {
                var rect = EditorGUILayout.BeginVertical(ZoneStyle);

                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(rect, TintOf(zone));
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, StripeWidth, rect.height), ColorOf(zone));
                }

                IsExpanded = DrawHeader(zone, title, detail, icon, key, isExpandedByDefault);
            }

            // Развёрнута ли зона: рисовать ли её содержимое.
            public bool IsExpanded { get; }

            public void Dispose()
            {
                EditorGUILayout.EndVertical();
            }
        }


        // Стрелка, цветной квадрат со значком, название и пояснение; клик по всей строке — свернуть или развернуть.
        private static bool DrawHeader(LiveBuilderZoneColor zone, string title, string detail, Texture2D icon, string key, bool isExpandedByDefault)
        {
            var preferenceKey = ExpandedPreferencePrefix + key;
            var isExpanded = EditorPrefs.GetBool(preferenceKey, isExpandedByDefault);
            var rect = GUILayoutUtility.GetRect(0f, HeaderHeight, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
                DrawHeaderLook(rect, zone, title, detail, icon, isExpanded);

            if (!GUI.Button(rect, GUIContent.none, GUIStyle.none))
                return isExpanded;

            // Содержимое появится или пропадёт — этот проход окна прерываем, иначе IMGUI путается в разметке.
            EditorPrefs.SetBool(preferenceKey, !isExpanded);
            GUIUtility.ExitGUI();
            return !isExpanded;
        }

        private static void DrawHeaderLook(Rect rect, LiveBuilderZoneColor zone, string title, string detail, Texture2D icon, bool isExpanded)
        {
            if (rect.Contains(Event.current.mousePosition))
                EditorGUI.DrawRect(new Rect(rect.x - 7f, rect.y, rect.width + 15f, rect.height), TintOf(zone, 0.1f));

            var arrowRect = new Rect(rect.x, rect.y + (rect.height - 13f) / 2f, 13f, 13f);
            EditorStyles.foldout.Draw(arrowRect, GUIContent.none, false, false, isExpanded, false);

            var badge = new Rect(arrowRect.xMax + 2f, rect.y + (rect.height - BadgeSize) / 2f, BadgeSize, BadgeSize);
            GUI.DrawTexture(badge, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, ColorOf(zone), 0f, 4f);

            if (icon != null)
                GUI.DrawTexture(new Rect(badge.x + 2f, badge.y + 2f, badge.width - 4f, badge.height - 4f), icon, ScaleMode.ScaleToFit);

            var titleContent = new GUIContent(title);
            var titleWidth = TitleStyle.CalcSize(titleContent).x;
            var titleRect = new Rect(badge.xMax + 6f, rect.y, Mathf.Min(titleWidth, rect.xMax - badge.xMax - 6f), rect.height);
            TitleStyle.Draw(titleRect, titleContent, false, false, false, false);

            if (string.IsNullOrEmpty(detail))
                return;

            var detailRect = new Rect(titleRect.xMax + 6f, rect.y, rect.xMax - titleRect.xMax - 6f, rect.height);
            DetailStyle.Draw(detailRect, new GUIContent(detail), false, false, false, false);
        }
    }
}
