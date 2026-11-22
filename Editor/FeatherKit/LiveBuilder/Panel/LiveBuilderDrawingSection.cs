using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Секция Drawing панели Live Builder — вместо карточки выделенного, пока инструмент рисует (<see cref="LiveBuilderDrawingReport"/>).
    /// - что рисуешь и шаг жеста, живые размеры; замок — число набрано с клавиатуры, мышь его не меняет
    /// - набранное «4 × 3 × 1,5▌» и что сделают Enter и Esc
    /// Синяя полоса слева — это про новое, как строка инструмента. Только рисует: числа отдаёт жест.
    /// </summary>
    public static class LiveBuilderDrawingSection
    {
        private const float AccentWidth = 3f;
        private const float LockIconSize = 14f;

        private static GUIStyle sectionStyle;
        private static GUIStyle titleStyle;
        private static GUIStyle stepStyle;
        private static GUIStyle typedStyle;
        private static GUIStyle keysStyle;


        private static GUIStyle SectionStyle => sectionStyle ??= new GUIStyle { padding = new RectOffset(9, 4, 4, 5) };

        private static GUIStyle TitleStyle => titleStyle ??= new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = ToolColor } };

        private static GUIStyle StepStyle => stepStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };

        private static GUIStyle TypedStyle => typedStyle ??= new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = ToolColor } };

        private static GUIStyle KeysStyle => keysStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };

        private static Color ToolColor => LiveBuilderAccentColors.ColorOf(LiveBuilderAccentColor.Tool);


        public static void Draw(LiveBuilderDrawingReport report)
        {
            // Прямоугольник секции известен уже на отрисовке после разметки — фон и полоса ложатся под текст.
            var rect = EditorGUILayout.BeginVertical(SectionStyle);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, LiveBuilderAccentColors.TintOf(LiveBuilderAccentColor.Tool, 0.06f));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, AccentWidth, rect.height), ToolColor);
            }

            DrawHeader(report);

            foreach (var size in report.Sizes)
                DrawSize(size);

            DrawTyped(report.TypedText);
            DrawKeys(report);
            EditorGUILayout.EndVertical();
        }


        // «Drawing  new Box» слева, «step 2 / 2 · Height» справа; у линии точек шагов сколько угодно — только «Point 3».
        private static void DrawHeader(LiveBuilderDrawingReport report)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Drawing", "Что рисуешь сейчас; карточка выделенного вернётся, когда закончишь"), TitleStyle, GUILayout.ExpandWidth(false));
                GUILayout.Label("new " + report.Title, EditorStyles.label, GUILayout.ExpandWidth(false));
                GUILayout.FlexibleSpace();
                GUILayout.Label(StepText(report), StepStyle, GUILayout.ExpandWidth(false));
            }
        }

        private static string StepText(LiveBuilderDrawingReport report)
        {
            return report.StepCount > 0 ? "step " + report.StepNumber + " / " + report.StepCount + " · " + report.StepName : report.StepName;
        }


        // Подпись, число и замок у набранного; со знаком — только отрицательное (линейка вниз).
        private static void DrawSize(LiveBuilderDrawnSize size)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(size.Label, GUILayout.Width(EditorGUIUtility.labelWidth - 9f));
                var value = (size.Meters < 0f ? "−" : "") + LiveBuilderSceneText.FormatMeters(size.Meters) + " m";
                LiveBuilderWindowControls.ValueChip(new GUIContent(value, size.IsTyped ? "Набрано с клавиатуры — мышь это число не меняет" : "От мыши"),
                    GUILayout.MinWidth(40f));

                var lockRect = GUILayoutUtility.GetRect(LockIconSize, LockIconSize, GUILayout.Width(LockIconSize), GUILayout.Height(LockIconSize));

                if (size.IsTyped && Event.current.type == EventType.Repaint)
                    GUI.DrawTexture(lockRect, EditorGUIUtility.IconContent("InspectorLock").image, ScaleMode.ScaleToFit, true, 0f, ToolColor, 0f, 0f);
            }
        }


        // Набранное с курсором в конце, как в поле размеров SketchUp.
        private static void DrawTyped(string typedText)
        {
            if (string.IsNullOrEmpty(typedText))
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Typed", GUILayout.Width(EditorGUIUtility.labelWidth - 9f));
                GUILayout.Label(typedText.Replace("×", " × ") + "▌", TypedStyle);
            }
        }


        // «Enter — box 4 × 3 × 1,5 · Esc — clear typing»; клавиша, которая ничего не делает, не пишется.
        private static void DrawKeys(LiveBuilderDrawingReport report)
        {
            var enter = report.EnterResult != null ? "Enter — " + report.EnterResult : null;
            var escape = report.EscResult != null ? "Esc — " + report.EscResult : null;
            var text = enter != null && escape != null ? enter + "  ·  " + escape : enter ?? escape;

            if (text != null)
                GUILayout.Label(text, KeysStyle);
        }
    }
}
