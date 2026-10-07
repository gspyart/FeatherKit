using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>Кнопка со своим видом через FeatherEditorGUIBox — фон свой, клик ловит обычный GUI.Button.</summary>
    public static class GUIButton
    {
        // Кэшированный стиль текста, на базе label (не button — там свои border/padding под бевел).
        private static GUIStyle textOnlyStyle;

        private static GUIStyle TextOnlyStyle
        {
            get
            {
                if (textOnlyStyle == null)
                {
                    textOnlyStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        padding = new RectOffset(0, 0, 0, 0),
                        margin = new RectOffset(4, 4, 4, 4),
                        border = new RectOffset(0, 0, 0, 0),
                    };
                }

                return textOnlyStyle;
            }
        }


        // Явный Rect — для кастомного позиционирования.
        public static bool Draw(Rect rect, string text, ButtonStyle style)
        {
            // GUI.enabled не гасит наши Handles-фигуры сам, hover/press тоже не подсвечиваются сами
            // (мы всё рисуем вручную через Handles) — считаем состояние по mousePosition и красим сами.
            var alphaScale = GUI.enabled ? 1f : 0.5f;
            var isHover = GUI.enabled && rect.Contains(Event.current.mousePosition);
            var isActive = isHover && Event.current.type == EventType.MouseDown && Event.current.button == 0;

            var background = style.BackgroundColor;
            var border = style.BorderColor;

            if (background.a <= 0.001f) // прозрачный (outline) фон — на hover/press лёгкая заливка цветом обводки
            {
                if (isActive)
                    background = new Color(border.r, border.g, border.b, 0.25f);
                else if (isHover)
                    background = new Color(border.r, border.g, border.b, 0.1f);
            }
            else if (isActive)
            {
                background = Scale(background, 0.85f);
                border = Scale(border, 0.85f);
            }
            else if (isHover)
            {
                background = Scale(background, 1.15f);
                border = Scale(border, 1.15f);
            }

            background.a *= alphaScale;
            border.a *= alphaScale;

            FeatherEditorGUIBox.Draw(rect, background, border, style.BorderThickness, style.CornerRadius);

            var textStyle = TextOnlyStyle;
            var textColor = style.TextColor;
            textColor.a *= alphaScale;
            textStyle.normal.textColor = textColor;
            textStyle.hover.textColor = textColor;
            textStyle.active.textColor = textColor;

            return GUI.Button(rect, text, textStyle);
        }

        // Удобный вариант для layout-кода: сам резервирует место через GUILayout.
        public static bool Draw(string text, ButtonStyle style, params GUILayoutOption[] options)
        {
            var rect = GUILayoutUtility.GetRect(new GUIContent(text), TextOnlyStyle, options);
            return Draw(rect, text, style);
        }

        private static Color Scale(Color color, float factor)
        {
            return new Color(Mathf.Clamp01(color.r * factor), Mathf.Clamp01(color.g * factor), Mathf.Clamp01(color.b * factor), color.a);
        }
    }
}
