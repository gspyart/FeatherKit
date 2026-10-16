using System;
using System.Collections.Generic;
using System.Reflection;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Вид блоков сценария как в Scratch: цвет по категории, вложенный столбик — в цветной скобке.
    /// - цвет категории — по первой части пути блока: «Звук/…», «Управление/…»
    /// - русская подпись поля из ScenarioLabelAttribute
    /// </summary>
    internal static class ScenarioBlockStyle
    {
        private const float BackgroundAlpha = 0.16f;
        private const float ActiveBackgroundAlpha = 0.26f;
        private const float StripeWidth = 4f;
        private const float BracketWidth = 3f;
        private const float BracketCapLength = 10f;

        private static readonly Color DefaultColor = new Color(0.55f, 0.55f, 0.55f);

        // Цвета — как у похожих категорий Scratch, чтобы читались привычно.
        private static readonly Dictionary<string, Color> ColorsByCategory = new Dictionary<string, Color>
        {
            { "Управление", Hex(0xFFAB19) },
            { "Логика", Hex(0x59C059) },
            { "Переменные", Hex(0xFF8C1A) },
            { "Сигналы", Hex(0xFFD500) },
            { "Триггер", Hex(0xFFBF00) },
            { "Объект", Hex(0x5CB1D6) },
            { "Трансформ", Hex(0x4C97FF) },
            { "Свет", Hex(0xE6D02E) },
            { "Звук", Hex(0xCF63CF) },
            { "Анимация", Hex(0x9966FF) },
            { "Игрок", Hex(0x0FBD8C) },
            { "Сценарий", Hex(0xFF6680) }
        };

        private static readonly Dictionary<(Type, string), GUIContent> LabelsByField = new Dictionary<(Type, string), GUIContent>();


        // ── Цвет ─────────────────────────────────────────────────────────

        public static Color ColorOf(Type blockType)
        {
            var path = ScenarioBlockCatalog.MenuPathOf(blockType);
            var slash = path.IndexOf('/');

            return slash > 0 && ColorsByCategory.TryGetValue(path.Substring(0, slash), out var color) ? color : DefaultColor;
        }

        /// <summary>Цвет блока, внутри которого лежит это поле; не внутри блока — серый.</summary>
        public static Color OwnerColorOf(SerializedProperty property)
        {
            var path = property.propertyPath;

            for (var cut = path.LastIndexOf('.'); cut > 0; cut = path.LastIndexOf('.'))
            {
                path = path.Substring(0, cut);
                var owner = property.serializedObject.FindProperty(path);

                if (owner != null && owner.propertyType == SerializedPropertyType.ManagedReference
                                  && owner.managedReferenceValue != null)
                    return ColorOf(owner.managedReferenceValue.GetType());
            }

            return DefaultColor;
        }

        public static void DrawBlockBackground(Rect rect, Color color, bool isActive)
        {
            var inner = new Rect(rect.x, rect.y + 1f, rect.width, rect.height - 2f);
            var fill = color;
            fill.a = isActive ? ActiveBackgroundAlpha : BackgroundAlpha;

            EditorGUI.DrawRect(inner, fill);
            EditorGUI.DrawRect(new Rect(inner.x, inner.y, StripeWidth, inner.height), color);
        }

        /// <summary>Скоба слева от вложенного столбика: «C» в цвет блока-владельца.</summary>
        public static void DrawBracket(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, BracketWidth, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, BracketCapLength, BracketWidth), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - BracketWidth, BracketCapLength, BracketWidth), color);
        }


        // ── Подписи полей ────────────────────────────────────────────────

        /// <summary>Подпись поля блока: из ScenarioLabelAttribute, а без неё — имя поля, как обычно в Unity.</summary>
        public static GUIContent LabelOf(Type blockType, SerializedProperty field)
        {
            var key = (blockType, field.name);

            if (LabelsByField.TryGetValue(key, out var label))
                return label;

            var fieldInfo = FindField(blockType, field.name);
            var text = fieldInfo?.GetCustomAttribute<ScenarioLabelAttribute>()?.Text ?? field.displayName;
            var tooltip = fieldInfo?.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? field.tooltip;

            label = new GUIContent(text, tooltip);
            LabelsByField[key] = label;

            return label;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (field != null)
                    return field;
            }

            return null;
        }


        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }
    }
}
