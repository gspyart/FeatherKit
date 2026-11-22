using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Надписи инструмента «Live Builder» в окне сцены: размеры рядом с коробкой и строка-подсказка внизу — что делать сейчас,
    /// клавиши плашками, а без полной панели — этаж, сетка и прилипание справа.</summary>
    [InitializeOnLoad]
    public static class LiveBuilderSceneText
    {
        private const float HintBottomMargin = 10f;
        private const float HintSideMargin = 10f;
        private const float HintSidePadding = 10f;
        private const float HintHeight = 24f;
        private const float HintPartGap = 6f;
        private const float HintStatusGap = 14f;
        private const float KeyHeight = 17f;

        // Насколько надпись отодвинута от своей точки вправо и вверх, пикс.: не под ручкой и не под курсором — стрелка мыши уходит вправо-вниз.
        private const float LabelOffsetX = 14f;
        private const float LabelOffsetY = 8f;

        // Отступ надписи от края окна сцены, пикс.
        private const float LabelEdgeMargin = 4f;

        private static readonly Color BackgroundColor = new(0.1f, 0.1f, 0.1f, 0.9f);
        private static readonly Color KeyColor = new(0.29f, 0.29f, 0.29f, 1f);
        private static readonly Color KeyBorderColor = new(0.16f, 0.16f, 0.16f, 1f);
        private static readonly Color DividerColor = new(0.35f, 0.35f, 0.35f, 1f);
        private static readonly string[] AxisNames = { "X", "Y", "Z" };

        private static Texture2D background;
        private static GUIStyle labelStyle;
        private static GUIStyle hintTextStyle;
        private static GUIStyle hintKeyStyle;
        private static GUIStyle hintStatusStyle;


        // Подложка не сохраняется и сама не удаляется: перед перезагрузкой скриптов — удалить, иначе после каждой копится новая.
        static LiveBuilderSceneText()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                if (background != null)
                    Object.DestroyImmediate(background);
            };
        }


        // Тёмная подложка: на светлой и пёстрой сцене белый текст читается одинаково.
        private static Texture2D Background
        {
            get
            {
                if (background != null)
                    return background;

                background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                background.SetPixel(0, 0, BackgroundColor);
                background.Apply();
                return background;
            }
        }

        // Стиль пересоздаётся, если подложку убрал движок (смена сцены, сборка мусора).
        private static GUIStyle LabelStyle => labelStyle != null && labelStyle.normal.background != null ? labelStyle : labelStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(6, 6, 3, 3),
            normal = { textColor = Color.white, background = Background }
        };

        private static GUIStyle HintTextStyle => hintTextStyle ??= new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(0, 0, 0, 0),
            normal = { textColor = Color.white }
        };

        private static GUIStyle HintKeyStyle => hintKeyStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(5, 5, 0, 1),
            normal = { textColor = new Color(0.95f, 0.95f, 0.95f) }
        };

        private static GUIStyle HintStatusStyle => hintStatusStyle ??= new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(0, 0, 0, 0),
            normal = { textColor = new Color(0.67f, 0.67f, 0.73f) }
        };


        // Надпись у точки мира — размеры коробки: правее и выше точки, в пределах окна сцены. Точка за камерой — надписи нет.
        public static void DrawLabel(Vector3 worldPoint, string text)
        {
            var guiPoint = HandleUtility.WorldToGUIPointWithDepth(worldPoint);

            if (guiPoint.z < 0f)
                return;

            var content = new GUIContent(text);
            var size = LabelStyle.CalcSize(content);
            var rect = new Rect(guiPoint.x + LabelOffsetX, guiPoint.y - LabelOffsetY - size.y, size.x, size.y);

            if (Camera.current != null)
            {
                var viewSize = Camera.current.pixelRect.size / EditorGUIUtility.pixelsPerPoint;
                rect.x = Mathf.Clamp(rect.x, LabelEdgeMargin, Mathf.Max(LabelEdgeMargin, viewSize.x - size.x - LabelEdgeMargin));
                rect.y = Mathf.Clamp(rect.y, LabelEdgeMargin, Mathf.Max(LabelEdgeMargin, viewSize.y - size.y - LabelEdgeMargin));
            }

            Handles.BeginGUI();
            GUI.Label(rect, content, LabelStyle);
            Handles.EndGUI();
        }


        // ── Подсказка внизу ──────────────────────────────────────────────

        // Строка внизу по центру окна сцены: что сделать дальше и клавиши плашками (<see cref="LiveBuilderHintKeys"/>).
        // Полной панели не видно — справа этаж, сетка и прилипание: иначе их негде увидеть.
        public static void DrawHint(SceneView sceneView, string hint)
        {
            var viewSize = sceneView.camera.pixelRect.size / EditorGUIUtility.pixelsPerPoint;
            var status = IsFullPanelShown(sceneView) ? null : new GUIContent(StatusText());
            var statusWidth = status != null ? HintStatusStyle.CalcSize(status).x + HintStatusGap : 0f;
            var maxWidth = viewSize.x - HintSideMargin * 2f;
            var partsWidth = Mathf.Min(LineWidth(hint, HintTextStyle), maxWidth - statusWidth - HintSidePadding * 2f);
            var width = status != null ? maxWidth : Mathf.Min(partsWidth + HintSidePadding * 2f, maxWidth);
            var barRect = new Rect((viewSize.x - width) / 2f, viewSize.y - HintHeight - HintBottomMargin, width, HintHeight);

            Handles.BeginGUI();
            EditorGUI.DrawRect(barRect, BackgroundColor);

            var partsLeft = status != null ? barRect.x + (barRect.width - statusWidth - partsWidth) / 2f : barRect.x + HintSidePadding;
            DrawLine(new Rect(partsLeft, barRect.y, partsWidth, HintHeight), hint, HintTextStyle);

            if (status != null)
            {
                var statusRect = new Rect(barRect.xMax - statusWidth - HintSidePadding, barRect.y, statusWidth, HintHeight);
                EditorGUI.DrawRect(new Rect(statusRect.x, barRect.y + 5f, 1f, HintHeight - 10f), DividerColor);
                GUI.Label(new Rect(statusRect.x + HintStatusGap, barRect.y, statusWidth - HintStatusGap, HintHeight), status, HintStatusStyle);
            }

            Handles.EndGUI();
        }

        // Полная панель Live Builder открыта в этом окне сцены — не свёрнута и не полосой.
        private static bool IsFullPanelShown(SceneView sceneView)
        {
            return sceneView.TryGetOverlay(LiveBuilderPanelOverlay.OverlayId, out var panel) && panel is LiveBuilderPanelOverlay { IsFullPanelShown: true };
        }


        // «Storey 2 · Grid 0.5 m · Snap on» — только для чтения: меняют клавишами и в панели.
        private static string StatusText()
        {
            return "Storey " + LiveBuilderStoreys.CurrentStoreyNumber + (LiveBuilderStoreys.IsSectionEnabled ? " (section)" : "") +
                   " · Grid " + FormatMeters(LiveBuilderGrid.CurrentStep) + " m · Snap " + (LiveBuilderPointSnap.IsEnabled ? "on" : "off");
        }


        // ── Строка с плашками ────────────────────────────────────────────

        // Ширина строки с плашками клавиш (<see cref="LiveBuilderHintKeys"/>) одним рядом.
        public static float LineWidth(string line, GUIStyle textStyle)
        {
            var width = 0f;

            foreach (var part in LiveBuilderHintKeys.PartsOf(line))
                width += PartWidth(part, textStyle) + HintPartGap;

            return Mathf.Max(0f, width - HintPartGap);
        }


        // Строка с плашками одним рядом — подсказка внизу и шпаргалка. Не влезло — обрезается справа.
        public static void DrawLine(Rect area, string line, GUIStyle textStyle)
        {
            GUI.BeginClip(area);
            var x = 0f;

            foreach (var part in LiveBuilderHintKeys.PartsOf(line))
            {
                var partWidth = PartWidth(part, textStyle);

                if (part.IsKey)
                {
                    var keyRect = new Rect(x, (area.height - KeyHeight) / 2f, partWidth, KeyHeight);
                    EditorGUI.DrawRect(keyRect, KeyBorderColor);
                    EditorGUI.DrawRect(new Rect(keyRect.x + 1f, keyRect.y + 1f, keyRect.width - 2f, keyRect.height - 3f), KeyColor);
                    GUI.Label(keyRect, part.Text, HintKeyStyle);
                }
                else
                {
                    GUI.Label(new Rect(x, 0f, partWidth, area.height), part.Text, textStyle);
                }

                x += partWidth + HintPartGap;
            }

            GUI.EndClip();
        }

        private static float PartWidth((string Text, bool IsKey) part, GUIStyle textStyle)
        {
            return (part.IsKey ? HintKeyStyle : textStyle).CalcSize(new GUIContent(part.Text)).x;
        }


        // ── Числа ────────────────────────────────────────────────────────

        // Метры для надписи: «1.5» — точкой, как в английских подписях.
        public static string FormatMeters(float meters)
        {
            return Mathf.Abs(meters).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }


        // Сдвиг по осям для надписи у ручек переноса: «X +2 · Z −1 m»; без сдвига — null.
        public static string FormatOffset(Vector3 offset)
        {
            var parts = new List<string>();

            for (var axis = 0; axis < 3; axis++)
                if (!Mathf.Approximately(offset[axis], 0f))
                    parts.Add(AxisNames[axis] + " " + (offset[axis] > 0f ? "+" : "−") + FormatMeters(offset[axis]));

            return parts.Count > 0 ? string.Join(" · ", parts) + " m" : null;
        }
    }
}
