using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Шпаргалка клавиш в углу окна сцены — только то, что работает сейчас: клавиши выбранного инструмента (<see cref="LiveBuilderTool.KeyHints"/>)
    /// и того, что выделено (точки контура, грани коробки). Общие клавиши — на значках и в подсказках панели.
    /// Клавиша ярлыка Toggle Key Hints прячет и показывает её; спрятана — в углу одна плашка, как вернуть.
    /// </summary>
    public static class LiveBuilderSceneKeys
    {
        private const string ShownPreferenceKey = "FeatherKit.LiveBuilder.SceneKeysShown";
        private const float Margin = 10f;
        private const float BottomOffset = 40f;
        private const float Width = 250f;
        private const float Padding = 6f;
        private const float TitleHeight = 18f;
        private const float LineHeight = 18f;

        private static readonly Color BackgroundColor = new(0.1f, 0.1f, 0.1f, 0.75f);

        private static GUIStyle lineStyle;
        private static GUIStyle titleStyle;


        public static bool IsShown
        {
            get => EditorPrefs.GetBool(ShownPreferenceKey, true);
            set => EditorPrefs.SetBool(ShownPreferenceKey, value);
        }

        private static GUIStyle LineStyle => lineStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(0, 0, 0, 0),
            normal = { textColor = new Color(1f, 1f, 1f, 0.9f) }
        };

        private static GUIStyle TitleStyle => titleStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(0, 0, 0, 0),
            normal = { textColor = Color.white }
        };

        private static string ToggleKey => LiveBuilderHintKeys.Bound(LiveBuilderShortcutIds.ToggleKeyHints);


        // ── Рисование ────────────────────────────────────────────────────

        // Левый нижний угол окна сцены, над строкой подсказки. isContourShown — видны точки выделенной стены или здания.
        public static void Draw(SceneView sceneView, LiveBuilderTool tool, bool isContourShown)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var viewSize = sceneView.camera.pixelRect.size / EditorGUIUtility.pixelsPerPoint;
            Handles.BeginGUI();

            if (!IsShown)
                DrawHiddenChip(viewSize);
            else
                DrawSheet(viewSize, tool, isContourShown);

            Handles.EndGUI();
        }

        private static void DrawHiddenChip(Vector2 viewSize)
        {
            var line = ToggleKey + " keys";
            var width = LiveBuilderSceneText.LineWidth(line, LineStyle) + Padding * 2f;
            var rect = new Rect(Margin, viewSize.y - BottomOffset - LineHeight - 4f, width, LineHeight + 4f);
            EditorGUI.DrawRect(rect, BackgroundColor);
            LiveBuilderSceneText.DrawLine(new Rect(rect.x + Padding, rect.y + 2f, width - Padding * 2f, LineHeight), line, LineStyle);
        }

        private static void DrawSheet(Vector2 viewSize, LiveBuilderTool tool, bool isContourShown)
        {
            var lines = LinesFor(tool, isContourShown);

            if (lines.Count == 0)
                return;

            var height = Padding + TitleHeight + lines.Count * LineHeight + Padding;
            var rect = new Rect(Margin, viewSize.y - BottomOffset - height, Width, height);
            EditorGUI.DrawRect(rect, BackgroundColor);

            var lineRect = new Rect(rect.x + Padding, rect.y + Padding, Width - Padding * 2f, TitleHeight);
            LiveBuilderSceneText.DrawLine(lineRect, tool.DisplayName + " — keys " + ToggleKey + " hide", TitleStyle);

            foreach (var line in lines)
            {
                lineRect.y += lineRect.height;
                lineRect.height = LineHeight;
                LiveBuilderSceneText.DrawLine(lineRect, line, LineStyle);
            }
        }


        // ── Строки ───────────────────────────────────────────────────────

        // Инструмент, потом выделенное. Каждый раз заново: подписи клавиш — из нынешних привязок.
        public static List<string> LinesFor(LiveBuilderTool tool, bool isContourShown)
        {
            var lines = new List<string>(tool.KeyHints);

            if (isContourShown)
                lines.AddRange(ContourLines(tool));
            else if (IsEditableBoxSelected(tool))
                lines.AddRange(BoxLines());

            return lines;
        }

        // Рамка по пустому — только где пустой клик не начинает новую линию (не у Wall и Building).
        private static IEnumerable<string> ContourLines(LiveBuilderTool tool)
        {
            yield return LiveBuilderHintKeys.Bound(LiveBuilderShortcutIds.ContourNudgeLeft) + LiveBuilderHintKeys.Bound(LiveBuilderShortcutIds.ContourNudgeForward) +
                         LiveBuilderHintKeys.Bound(LiveBuilderShortcutIds.ContourNudgeRight) + LiveBuilderHintKeys.Bound(LiveBuilderShortcutIds.ContourNudgeBack) +
                         " — nudge points one grid step";
            yield return LiveBuilderHintKeys.Key("Delete") + " — remove points " + LiveBuilderHintKeys.Key("double-click") + " — point kind";

            if (tool.PathGesture == null)
                yield return LiveBuilderHintKeys.Key("drag on empty") + " — select points, " + LiveBuilderHintKeys.Shift + " add";

            if (LiveBuilderContourSelection.Owner is SplineWall)
                yield return LiveBuilderHintKeys.Bound(LiveBuilderShortcutIds.ContourCloseOrOpen) + " — close or open the wall";
        }

        private static IEnumerable<string> BoxLines()
        {
            yield return "Face square — size " + LiveBuilderHintKeys.Alt + " both " + LiveBuilderHintKeys.Alt + LiveBuilderHintKeys.Shift + " all";
            yield return LiveBuilderHintKeys.Ctrl + " + drag a square — extrude a new box";
            yield return LiveBuilderHintKeys.Shift + " + move handles — copy";
        }

        // Выделена коробка, комната или лестница, у которой инструмент показывает квадраты граней.
        private static bool IsEditableBoxSelected(LiveBuilderTool tool)
        {
            var selected = Selection.activeGameObject;
            return selected != null && BoxModelShapeCollector.IsPiece(selected) && tool.CanEdit(selected);
        }
    }
}
