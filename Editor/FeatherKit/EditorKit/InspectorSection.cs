// ═══════════════════════════════════════════════════════════════════════════════════════
// ПАМЯТКА
//
// Сворачиваемая секция для любых редакторных инспекторов FeatherKit, не только шейдерных.
// Пользоваться так:
//
//     InspectorSection.Draw("мой.ключ", "Заголовок", accentIndex: 0, () =>
//     {
//         // рисуем содержимое обычным EditorGUILayout
//     });
//
// КЛЮЧ должен быть УНИКАЛЬНЫМ на всё приложение: по нему в EditorPrefs хранится
// состояние сворачивания. Совпадут ключи у двух разных инспекторов — они начнут
// сворачиваться вместе, и понять почему будет тяжело. Договорённость: «префикс.секция»,
// например "toon.outline".
//
// ACCENT INDEX — номер цвета из палитры InspectorStyle, берётся по кругу. Соседние секции
// разводите разными номерами, иначе полоски сливаются.
//
// Состояние в EditorPrefs, а не в поле класса, намеренно: ShaderGUI пересоздаётся при
// каждой перезагрузке домена, и всё свёрнутое раскрывалось бы обратно.
//
// Рисование идёт через FeatherEditorGUIBox — он умеет скругления без текстур, на Handles.
// ═══════════════════════════════════════════════════════════════════════════════════════

using System;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Сворачиваемая секция инспектора: скруглённая коробка, полоса заголовка и цветная
    /// метка слева.
    ///
    /// Состояние сворачивания живёт в EditorPrefs по ключу — иначе оно сбрасывалось бы
    /// при каждой перезагрузке домена, а в шейдере секций почти десяток.
    /// </summary>
    public static class InspectorSection
    {
        private const string PrefsPrefix = "FeatherKit.Section.";

        private static GUIStyle titleStyle;
        private static GUIStyle contentStyle;


        /// <summary>
        /// Нарисовать секцию. Содержимое рисуется внутри <paramref name="content"/>
        /// и только когда секция раскрыта.
        /// </summary>
        public static void Draw(string key, string title, int accentIndex, Action content, bool expandedByDefault = true)
        {
            var style = InspectorStyle.Current;
            var prefsKey = PrefsPrefix + key;
            var expanded = EditorPrefs.GetBool(prefsKey, expandedByDefault);

            GUILayout.Space(style.SectionGap);

            var box = FeatherEditorGUIBox.BoxBegin(style.Fill, style.Outline, style.OutlineThickness, style.CornerRadius);

            var header = GUILayoutUtility.GetRect(0f, style.HeaderHeight, GUILayout.ExpandWidth(true));

            DrawHeader(header, title, style, accentIndex, expanded);

            if (Event.current.type == EventType.MouseDown && header.Contains(Event.current.mousePosition))
            {
                EditorPrefs.SetBool(prefsKey, !expanded);
                Event.current.Use();
            }

            if (expanded)
            {
                // Отступ задаём стилем, а не вложенными Begin/End со Space: вложение
                // добавляет собственные поля Unity поверх наших, и слева набегает лишнее.
                EnsureContentStyle(style);

                EditorGUILayout.BeginVertical(contentStyle);

                content?.Invoke();

                EditorGUILayout.EndVertical();
            }

            FeatherEditorGUIBox.BoxEnd();
        }


        private static void DrawHeader(Rect header, string title, InspectorStyle style, int accentIndex, bool expanded)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            // Полосу заголовка рисуем с теми же скруглениями сверху, что и у коробки,
            // иначе её углы торчат за пределы секции.
            var strip = new Rect(header.x, header.y, header.width, header.height);
            FeatherEditorGUIBox.Draw(strip, style.Header, Color.clear, 0f, style.CornerRadius);

            if (style.AccentWidth > 0f)
            {
                var accent = new Rect(strip.x, strip.y + 3f, style.AccentWidth, strip.height - 6f);
                FeatherEditorGUIBox.Draw(accent, style.Accent(accentIndex), Color.clear, 0f, style.AccentWidth * 0.5f);
            }

            EnsureTitleStyle(style);

            var arrow = expanded ? "▾" : "▸";
            var labelRect = new Rect(strip.x + style.AccentWidth + 6f, strip.y, strip.width - style.AccentWidth - 10f, strip.height);

            GUI.Label(labelRect, arrow + "  " + title, titleStyle);
        }

        private static void EnsureContentStyle(InspectorStyle style)
        {
            var padding = Mathf.RoundToInt(style.Padding);

            if (contentStyle != null && contentStyle.padding.left == padding)
                return;

            contentStyle = new GUIStyle
            {
                padding = new RectOffset(padding, padding, 4, padding)
            };
        }

        private static void EnsureTitleStyle(InspectorStyle style)
        {
            if (titleStyle != null && titleStyle.fontSize == style.HeaderFontSize)
                return;

            titleStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                fontSize = style.HeaderFontSize
            };

            titleStyle.normal.textColor = style.Title;
        }
    }
}
