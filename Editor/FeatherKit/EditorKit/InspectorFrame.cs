using System;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Плоская рамка инспектора и то, что в ней рисуют: фон по глубине вложенности, синяя подпись
    /// секции, серое пояснение. Рядом со сворачиваемой <see cref="InspectorSection"/> — это её
    /// спокойный вариант для мелких групп внутри блока, где секция с полосой была бы слишком громкой.
    ///
    /// Цвета и отступы — из <see cref="InspectorStyle"/>. Глубину считает сама: рамка внутри рамки
    /// темнее, вызывающему следить за ней не нужно.
    /// </summary>
    public static class InspectorFrame
    {
        // Шаг отступа Unity на один уровень indentLevel — у него это внутренняя константа.
        private const float IndentStepWidth = 15f;

        private const float GapAfterFrame = 4f;

        private static int nestingDepth;
        private static GUIStyle contentStyle;
        private static GUIStyle sectionTitleStyle;
        private static GUIStyle descriptionStyle;


        // ── Рамка ────────────────────────────────────────────────────────

        /// <summary>Нарисовать содержимое в плоской рамке на текущем отступе.</summary>
        public static void Draw(Action content)
        {
            DrawAtCurrentIndent(() => DrawBox(content));
            EditorGUILayout.Space(GapAfterFrame);
        }


        // finally — потому что Unity прерывает отрисовку исключением, например при выборе ассета в окне подбора.
        private static void DrawBox(Action content)
        {
            var style = InspectorStyle.Current;

            nestingDepth++;

            try
            {
                var area = EditorGUILayout.BeginVertical(ContentStyleOf(style));

                if (Event.current.type == EventType.Repaint)
                    FeatherEditorGUIBox.Draw(area, style.FrameFill(nestingDepth), style.FrameOutline);

                content?.Invoke();

                EditorGUILayout.EndVertical();
            }
            finally
            {
                nestingDepth--;
            }
        }

        private static GUIStyle ContentStyleOf(InspectorStyle style)
        {
            var padding = Mathf.RoundToInt(style.FramePadding);

            if (contentStyle == null || contentStyle.padding.left != padding)
                contentStyle = new GUIStyle { padding = new RectOffset(padding, padding, padding, padding) };

            return contentStyle;
        }


        /// <summary>
        /// Нарисовать содержимое, сдвинутое на текущий отступ полей, а внутри отступ с нуля.
        /// Рамки рисуются раскладкой, а она отступ полей не знает: без сдвига рамка вылезла бы влево,
        /// а без сброса поля в ней уехали бы вправо дважды.
        /// </summary>
        public static void DrawAtCurrentIndent(Action content)
        {
            var indentLevel = EditorGUI.indentLevel;

            if (indentLevel > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(indentLevel * IndentStepWidth);
                EditorGUILayout.BeginVertical();
            }

            EditorGUI.indentLevel = 0;

            try
            {
                content?.Invoke();
            }
            finally
            {
                EditorGUI.indentLevel = indentLevel;
            }

            if (indentLevel > 0)
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
        }


        // ── Подписи ──────────────────────────────────────────────────────

        /// <summary>Синяя подпись секции заглавными.</summary>
        public static void DrawSectionTitle(string title)
        {
            var style = InspectorStyle.Current;

            if (sectionTitleStyle == null || sectionTitleStyle.normal.textColor != style.SectionTitle)
                sectionTitleStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = style.SectionTitle } };

            EditorGUILayout.LabelField(title.ToUpperInvariant(), sectionTitleStyle);
        }


        /// <summary>Серое пояснение мелким шрифтом, с переносом строк.</summary>
        public static void DrawDescription(string text)
        {
            var style = InspectorStyle.Current;

            if (descriptionStyle == null || descriptionStyle.normal.textColor != style.Description)
                descriptionStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { normal = { textColor = style.Description } };

            EditorGUILayout.LabelField(text, descriptionStyle);
        }
    }
}
